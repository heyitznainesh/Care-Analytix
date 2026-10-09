using System.Data;
using System.Text.Json;
using Dapper;
using Microsoft.Data.SqlClient;

namespace CareAnalytixWebChat.Services;

public class DatabaseQueryService
{
    private readonly string _connectionString;

    public DatabaseQueryService(IConfiguration config)
    {
        _connectionString = config.GetConnectionString("DefaultConnection") ?? "";
    }

    /// <summary>
    /// Executes a dynamic SQL query and returns the results as a JSON string.
    /// WARNING: This should only be used with SELECT queries.
    /// </summary>
    public async Task<string> ExecuteQueryAsJsonAsync(string sqlQuery)
    {
        try
        {
            // Security check: Only allow SELECT queries
            if (string.IsNullOrWhiteSpace(sqlQuery) || 
                !sqlQuery.TrimStart().StartsWith("SELECT", StringComparison.OrdinalIgnoreCase))
            {
                return "Error: Only SELECT queries are permitted for security reasons.";
            }

            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var results = await connection.QueryAsync(sqlQuery);
            
            if (results == null || !results.Any())
            {
                return "The query returned no results.";
            }

            // Serialize the dynamic results to a JSON string for the LLM
            // DapperRow requires casting to IDictionary to be serialized by System.Text.Json
            var dictResults = results.Cast<IDictionary<string, object>>();
            return JsonSerializer.Serialize(dictResults, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return $"Error executing SQL query: {ex.Message}";
        }
    }

    /// <summary>
    /// Intelligently searches the Faqs table for the best matching question based on keyword overlap.
    /// Bypasses the slow AI model if a good match is found.
    /// </summary>
    public async Task<string?> SearchFaqAsync(string userPrompt)
    {
        try
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();
            var faqs = await connection.QueryAsync<CareAnalytixWebChat.Models.Faq>("SELECT Question, Answer FROM Faqs");

            if (faqs == null || !faqs.Any()) return null;

            // Database Query bypass: If the prompt contains common DB query words, skip FAQ check
            var dbKeywords = new[] { "how many", "count", "average", "highest", "lowest", "list", "patient", "patients", "pmpm", "id ", " id", "expense", "total" };
            string lowerPrompt = userPrompt.ToLower();
            if (dbKeywords.Any(k => lowerPrompt.Contains(k)))
            {
                return null;
            }

            // Simple stop words to ignore
            var stopWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "what", "is", "the", "a", "an", "how", "do", "you", "are", "we", "can", "tell", "me", "about", "describe", "to", "for", "of", "in", "on", "and", "or", "does", "it"
            };

            var cleanPrompt = new string(userPrompt.Where(c => char.IsLetterOrDigit(c) || char.IsWhiteSpace(c)).ToArray());
            var promptWords = cleanPrompt.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                                         .Where(w => !stopWords.Contains(w))
                                         .Select(w => w.ToLower())
                                         .ToList();

            if (!promptWords.Any()) return null;

            int bestScore = 0;
            double bestMatchPercentage = 0;
            string? bestAnswer = null;

            foreach (var faq in faqs)
            {
                if (string.IsNullOrWhiteSpace(faq.Question)) continue;

                var cleanQuestion = new string(faq.Question.Where(c => char.IsLetterOrDigit(c) || char.IsWhiteSpace(c)).ToArray());
                var questionWords = cleanQuestion.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                                                 .Select(w => w.ToLower())
                                                 .ToHashSet();

                int score = promptWords.Count(w => questionWords.Contains(w));

                foreach (var pw in promptWords)
                {
                    if (!questionWords.Contains(pw) && faq.Question.ToLower().Contains(pw))
                    {
                        score++;
                    }
                }

                double matchPercentage = (double)score / promptWords.Count;

                if (score > bestScore)
                {
                    bestScore = score;
                    bestMatchPercentage = matchPercentage;
                    bestAnswer = faq.Answer;
                }
            }

            // Threshold: Require at least 40% of keywords to match, OR a score of 2+ (a few strong keywords)
            if (bestAnswer != null && (bestMatchPercentage >= 0.40 || bestScore >= 2))
            {
                // Clean HTML tags from the Excel answer
                string cleanAnswer = System.Text.RegularExpressions.Regex.Replace(bestAnswer, "<.*?>", string.Empty);
                cleanAnswer = cleanAnswer.Replace("&nbsp;", " ");
                cleanAnswer = System.Net.WebUtility.HtmlDecode(cleanAnswer);
                
                // Also clean up multiple consecutive spaces or newlines if any
                cleanAnswer = System.Text.RegularExpressions.Regex.Replace(cleanAnswer, @"\s{2,}", " ");
                
                return cleanAnswer.Trim();
            }

            return null;
        }
        catch
        {
            return null;
        }
    }
}
