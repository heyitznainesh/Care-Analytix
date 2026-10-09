using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using Microsoft.ML.OnnxRuntimeGenAI;
using CareAnalytixWebChat.Services;
using System.Threading.Tasks;
using System.Linq;

namespace CareAnalytixWebChat.Services
{
    public class FoundryPhiService : IDisposable
    {
        private Model? _model;
        private Tokenizer? _tokenizer;
        private bool _isLoaded = false;
        private readonly string _modelPath;
        private readonly HttpClient _httpClient;
        private Task? _downloadTask;
        
        // Progress tracking for the UI
        public string DownloadProgress { get; private set; } = "";
        public bool IsDownloading { get; private set; } = false;

        public FoundryPhiService()
        {
            _modelPath = Path.Combine(Directory.GetCurrentDirectory(), "Phi4Model");
            _httpClient = new HttpClient { Timeout = TimeSpan.FromHours(1) };
            
            // Start loading/downloading in background immediately
            _downloadTask = EnsureLoadedAsync();
        }

        private async Task EnsureLoadedAsync()
        {
            if (_isLoaded) return;
            
            if (!Directory.Exists(_modelPath))
            {
                Directory.CreateDirectory(_modelPath);
            }

            string[] filesToDownload = new[]
            {
                "added_tokens.json",
                "config.json",
                "configuration_phi3.py",
                "genai_config.json",
                "merges.txt",
                "model.onnx",
                "model.onnx.data",
                "special_tokens_map.json",
                "tokenizer.json",
                "tokenizer_config.json",
                "vocab.json"
            };

            string baseUrl = "https://huggingface.co/microsoft/Phi-4-mini-instruct-onnx/resolve/main/cpu_and_mobile/cpu-int4-rtn-block-32-acc-level-4/";

            bool needsDownload = filesToDownload.Any(f => !File.Exists(Path.Combine(_modelPath, f)));

            if (needsDownload)
            {
                IsDownloading = true;
                foreach (var file in filesToDownload)
                {
                    string destPath = Path.Combine(_modelPath, file);
                    if (!File.Exists(destPath))
                    {
                        DownloadProgress = $"Downloading {file}... (This may take a while)";
                        Console.WriteLine(DownloadProgress);
                        
                        var response = await _httpClient.GetAsync(baseUrl + file, HttpCompletionOption.ResponseHeadersRead);
                        response.EnsureSuccessStatusCode();
                        
                        using var fs = new FileStream(destPath, FileMode.Create, FileAccess.Write, FileShare.None);
                        await response.Content.CopyToAsync(fs);
                    }
                }
                IsDownloading = false;
            }

            if (!_isLoaded)
            {
                DownloadProgress = "Loading Microsoft Foundry Phi-4 ONNX Model into memory...";
                Console.WriteLine(DownloadProgress);
                await Task.Run(() => 
                {
                    _model = new Model(_modelPath);
                    _tokenizer = new Tokenizer(_model);
                });
                _isLoaded = true;
                DownloadProgress = "Phi-4 Model loaded successfully!";
                Console.WriteLine(DownloadProgress);
            }
        }

        public async IAsyncEnumerable<string> GenerateSqlResponseStreamAsync(string prompt, DatabaseQueryService dbService)
        {
            if (_downloadTask != null && !_downloadTask.IsCompleted)
            {
                await _downloadTask;
            }

            // 1. LIGHTNING FAST FAQ ROUTING
            // We search the FAQs using C# keyword overlap. If it's an FAQ, this returns the answer in 0.01 seconds.
            var instantFaqAnswer = await dbService.SearchFaqAsync(prompt);
            if (!string.IsNullOrWhiteSpace(instantFaqAnswer))
            {
                yield return instantFaqAnswer;
                yield break;
            }

            // 2. DYNAMIC DATABASE QUERY ROUTING
            // If it's not an FAQ, it's a dynamic data question (like "Who has the highest PMPM?").
            // We use the Phi-4 AI to write a SQL query for the Patients table.
            
            // Give the user instant feedback that the app is alive and working
            yield return "let me think?\n\n";

            string generatedSql = "";
            string lowerPrompt = prompt.ToLower();
            
            // Fast, deterministic Intent-to-SQL Routing
            var nameMatch = System.Text.RegularExpressions.Regex.Match(lowerPrompt, @"patient_\d+");
            if (nameMatch.Success)
            {
                string patientName = nameMatch.Value;
                generatedSql = $"SELECT * FROM Patients WHERE LOWER(Name) = '{patientName}'";
            }
            else if (lowerPrompt.Contains("expensive") || lowerPrompt.Contains("costly"))
            {
                string order = lowerPrompt.Contains("least") ? "ASC" : "DESC";
                generatedSql = $"SELECT TOP 5 Name, Expense, HealthCarePlan, RiskTier FROM Patients ORDER BY Expense {order}";
            }
            else if (lowerPrompt.Contains("list") || lowerPrompt.Contains("show") && lowerPrompt.Contains("member"))
            {
                if (lowerPrompt.Contains("medicare"))
                    generatedSql = "SELECT TOP 5 Name, HealthCarePlan, RiskTier FROM Patients WHERE LOWER(HealthCarePlan) LIKE '%medicare%'";
                else if (lowerPrompt.Contains("medicaid"))
                    generatedSql = "SELECT TOP 5 Name, HealthCarePlan, RiskTier FROM Patients WHERE LOWER(HealthCarePlan) LIKE '%medicaid%'";
                else if (lowerPrompt.Contains("commercial"))
                    generatedSql = "SELECT TOP 5 Name, HealthCarePlan, RiskTier FROM Patients WHERE LOWER(HealthCarePlan) LIKE '%commercial%'";
                else if (lowerPrompt.Contains("high") || lowerPrompt.Contains("critical"))
                    generatedSql = "SELECT TOP 5 Name, RiskTier FROM Patients WHERE LOWER(RiskTier) IN ('high', 'critical')";
                else
                    generatedSql = "SELECT TOP 10 Name, HealthCarePlan, RiskTier FROM Patients";
            }
            else if (lowerPrompt.Contains("how many") || lowerPrompt.Contains("count"))
            {
                generatedSql = "SELECT COUNT(*) AS TotalPatients FROM Patients";
            }
            else if (lowerPrompt.Contains("pmpm") && (lowerPrompt.Contains("highest") || lowerPrompt.Contains("top") || lowerPrompt.Contains("max")))
            {
                generatedSql = "SELECT TOP 5 Name, HealthCarePlan, Pmpm FROM Patients ORDER BY Pmpm DESC";
            }
            else if (lowerPrompt.Contains("pmpm") && lowerPrompt.Contains("average"))
            {
                generatedSql = "SELECT HealthCarePlan, AVG(Pmpm) AS AveragePMPM FROM Patients GROUP BY HealthCarePlan";
            }
            else if (lowerPrompt.Contains("er utilization") || lowerPrompt.Contains("er") || lowerPrompt.Contains("emergency"))
            {
                generatedSql = "SELECT TOP 5 Name, ERUtilization FROM Patients ORDER BY ERUtilization DESC";
            }
            else if (lowerPrompt.Contains("risk tier") || lowerPrompt.Contains("risk"))
            {
                generatedSql = "SELECT RiskTier, COUNT(*) AS PatientCount FROM Patients GROUP BY RiskTier";
            }
            else if (lowerPrompt.Contains("care gap") || lowerPrompt.Contains("gaps"))
            {
                generatedSql = "SELECT TOP 5 Name, OpenCareGaps FROM Patients ORDER BY OpenCareGaps DESC";
            }
            else
            {
                generatedSql = "SELECT TOP 5 Name, HealthCarePlan, Pmpm, RiskTier FROM Patients";
            }

            string queryResultsJson = "";
            string dbError = null;

            try 
            {
                queryResultsJson = await dbService.ExecuteQueryAsJsonAsync(generatedSql);
            }
            catch(Exception ex)
            {
                dbError = ex.Message;
            }

            if (dbError != null)
            {
                yield return $"**Database Error:** {dbError}";
                yield break;
            }

            if (queryResultsJson != null && queryResultsJson.Length > 2000)
            {
                queryResultsJson = queryResultsJson.Substring(0, 2000) + "...[truncated]";
            }

            // Debug output removed per user request

            // 3. FAST C# FORMATTER (Instant execution, replaces the 15s AI summarization)
            if (queryResultsJson == "The query returned no results.")
            {
                yield return "I do not have that data. No matching records were found in the database.";
                yield break;
            }

            string formattedAnswer = "";
            try
            {
                if (!string.IsNullOrWhiteSpace(queryResultsJson))
                {
                    using (JsonDocument doc = JsonDocument.Parse(queryResultsJson))
                    {
                        if (doc.RootElement.ValueKind == JsonValueKind.Array)
                        {
                            int resultCount = doc.RootElement.GetArrayLength();
                            if (resultCount == 1)
                            {
                                formattedAnswer += "Result:\n\n";
                            }
                            else
                            {
                                formattedAnswer += $"Found {resultCount} matching records:\n\n";
                            }

                            foreach (JsonElement element in doc.RootElement.EnumerateArray())
                            {
                                formattedAnswer += "- ";
                                if (element.ValueKind == JsonValueKind.Object)
                                {
                                    List<string> properties = new List<string>();
                                    foreach (JsonProperty prop in element.EnumerateObject())
                                    {
                                        properties.Add($"{prop.Name}: {prop.Value}");
                                    }
                                    formattedAnswer += string.Join(" | ", properties) + "\n";
                                }
                                else
                                {
                                    formattedAnswer += element.ToString() + "\n";
                                }
                            }
                        }
                        else
                        {
                            formattedAnswer = "Result:\n\n" + doc.RootElement.ToString();
                        }
                    }
                }
            }
            catch
            {
                // Fallback if parsing fails
                formattedAnswer = "Result (Raw):\n\n" + queryResultsJson + "\n";
            }

            if (string.IsNullOrWhiteSpace(formattedAnswer))
            {
                formattedAnswer = "No answer could be generated.";
            }

            yield return "[CLEAR]" + formattedAnswer;
        }

        public void Dispose()
        {
            _tokenizer?.Dispose();
            _model?.Dispose();
            _httpClient?.Dispose();
        }
    }
}
