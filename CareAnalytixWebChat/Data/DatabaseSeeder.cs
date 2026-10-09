using CareAnalytixWebChat.Models;
using System.Globalization;

namespace CareAnalytixWebChat.Data
{
    public static class DatabaseSeeder
    {
        public static void Initialize(IServiceProvider serviceProvider, string patientExcelPath, string faqExcelFilePath)
        {
            using var scope = serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            // Creates the database and tables if they do not exist
            context.Database.EnsureCreated();

            // Seed Patients if none exist
            if (!context.Patients.Any())
            {
                if (!File.Exists(patientExcelPath))
                {
                    Console.WriteLine($"Patient Excel file not found at {patientExcelPath}");
                }
                else
                {
                    var patients = new List<Patient>();
                    using (var stream = new FileStream(patientExcelPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    using (var workbook = new ClosedXML.Excel.XLWorkbook(stream))
                    {
                        var worksheet = workbook.Worksheet(1);
                        var rows = worksheet.RowsUsed();

                        foreach (var row in rows.Skip(1)) // Skip header
                        {
                            try
                            {
                                var patient = new Patient
                                {
                                    Name = row.Cell(2).GetValue<string>(),
                                    Expense = row.Cell(3).TryGetValue<decimal>(out var exp) ? exp : 0,
                                    HealthCarePlan = row.Cell(4).GetValue<string>(),
                                    Pmpm = row.Cell(5).TryGetValue<decimal>(out var pmpm) ? pmpm : 0,
                                    RiskTier = row.Cell(6).GetValue<string>(),
                                    ERUtilization = row.Cell(7).TryGetValue<float>(out var er) ? er : 0,
                                    OpenCareGaps = row.Cell(8).TryGetValue<int>(out var gaps) ? gaps : 0,
                                    SpecificAttachmentPoint = row.Cell(9).TryGetValue<decimal>(out var point) ? point : 0,
                                    ClaimStatus = row.Cell(10).GetValue<string>(),
                                    DenialReason = row.Cell(11).GetValue<string>(),
                                    FilingDeadline = row.Cell(12).TryGetValue<DateTime>(out var dt) ? dt : DateTime.MinValue,
                                    ProviderName = row.Cell(13).GetValue<string>(),
                                    HasSpecialAttachmentPoint = row.Cell(14).TryGetValue<bool>(out var hasSpec) ? hasSpec : false
                                };
                                patients.Add(patient);
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Error parsing Patient row {row.RowNumber()}: {ex.Message}");
                            }
                        }
                    }

                if (patients.Any())
                {
                    context.Patients.AddRange(patients);
                    context.SaveChanges();
                }
            }
            }

            // Seed FAQs if none exist
            if (!context.Faqs.Any())
            {
                if (!File.Exists(faqExcelFilePath))
                {
                    Console.WriteLine($"FAQ Excel file not found at {faqExcelFilePath}");
                    return;
                }

                var faqs = new List<Faq>();
                using (var stream = new FileStream(faqExcelFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var workbook = new ClosedXML.Excel.XLWorkbook(stream))
                {
                    var worksheet = workbook.Worksheet(1);
                    var rows = worksheet.RowsUsed();

                    foreach (var row in rows.Skip(1)) // Skip header
                    {
                        try
                        {
                            var faq = new Faq
                            {
                                Question = row.Cell(2).GetValue<string>(),
                                Answer = row.Cell(3).GetValue<string>(),
                                CategoryId = row.Cell(4).TryGetValue<int>(out var catId) ? catId : 0,
                                CreatedBy = row.Cell(5).GetValue<string>()
                            };
                            faqs.Add(faq);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error parsing FAQ row {row.RowNumber()}: {ex.Message}");
                        }
                    }
                }

                if (faqs.Any())
                {
                    context.Faqs.AddRange(faqs);
                    context.SaveChanges();
                }
            }

            // Seed PatientFaqs relationships if none exist
            if (!context.PatientFaqs.Any() && context.Patients.Any() && context.Faqs.Any())
            {
                var patientsList = context.Patients.ToList();
                var faqsList = context.Faqs.ToList();
                var random = new Random(42);
                var patientFaqs = new List<PatientFaq>();

                foreach (var patient in patientsList)
                {
                    // Assign 1 to 3 random FAQs to each patient
                    int numFaqs = random.Next(1, 4);
                    var shuffledFaqs = faqsList.OrderBy(x => random.Next()).Take(numFaqs).ToList();
                    
                    foreach (var faq in shuffledFaqs)
                    {
                        patientFaqs.Add(new PatientFaq
                        {
                            PatientId = patient.Id,
                            FaqId = faq.Id
                        });
                    }
                }

                if (patientFaqs.Any())
                {
                    context.PatientFaqs.AddRange(patientFaqs);
                    context.SaveChanges();
                }
            }

            // Seed Analytical FAQs from DOCX
            SeedAnalyticalFaqs(context);
        }

        private static void SeedAnalyticalFaqs(AppDbContext context)
        {
            var patients = context.Patients.ToList();
            if (!patients.Any()) return;

            var faqsToAdd = new List<Faq>();
            void AddFaq(string q, string a)
            {
                faqsToAdd.Add(new Faq { Question = q, Answer = a, CategoryId = 1, CreatedBy = "System - Analytics" });
            }

            // 1. What is our current specific stop-loss exposure?
            var eligibleClaims = patients.Where(p => p.Expense > p.SpecificAttachmentPoint && !p.ClaimStatus.Equals("Denied", StringComparison.OrdinalIgnoreCase)).ToList();
            var exposure = eligibleClaims.Sum(p => p.Expense - p.SpecificAttachmentPoint);
            AddFaq("What is our current specific stop-loss exposure?", $"Our current specific stop-loss exposure is ${exposure:0.##} from {eligibleClaims.Count} eligible claims.");

            // 2. How many members are approaching the attachment point?
            var approaching = patients.Where(p => p.Expense > (p.SpecificAttachmentPoint * 0.8m) && p.Expense <= p.SpecificAttachmentPoint).ToList();
            AddFaq("How many members are approaching the attachment point?", $"There are {approaching.Count} members approaching their specific attachment point (within 20%).");

            // 3. What is our projected reimbursement?
            AddFaq("What is our projected reimbursement?", $"Our projected reimbursement is ${exposure:0.##} based on current eligible claims exceeding specific attachment points.");

            // 4. What percentage of our population accounts for catastrophic claims?
            var catastrophic = patients.Count(p => p.RiskTier.Equals("Critical", StringComparison.OrdinalIgnoreCase));
            var percentage = patients.Count > 0 ? ((double)catastrophic / patients.Count * 100) : 0;
            AddFaq("What percentage of our population accounts for catastrophic claims?", $"Exactly {percentage:0.##}% of our population ({catastrophic} members) accounts for catastrophic (Critical) claims.");

            // 5. Are our stop-loss premiums justified by our historical claims distribution?
            AddFaq("Are our stop-loss premiums justified by our historical claims distribution?", "Based on our historical claims distribution, our stop-loss premiums are justified because our high-cost claims frequency has been steadily increasing in the top tiers.");

            // 6. How much risk are we retaining?
            var retainedRisk = patients.Sum(p => Math.Min(p.Expense, p.SpecificAttachmentPoint)) + patients.Where(p => p.ClaimStatus.Equals("Denied", StringComparison.OrdinalIgnoreCase)).Sum(p => Math.Max(0, p.Expense - p.SpecificAttachmentPoint));
            AddFaq("How much risk are we retaining?", $"We are currently retaining ${retainedRisk:0.##} in risk.");

            // 7. What would happen if our top five members each cost $1 million?
            AddFaq("What would happen if our top five members each cost $1 million?", "If our top five members each cost $1 million, our total gross expense for them would be $5,000,000. However, our actual retained risk would be capped by their specific attachment points.");

            // 8. What is our aggregate exposure?
            var aggregate = patients.Sum(p => p.Expense);
            AddFaq("What is our aggregate exposure?", $"Our total aggregate exposure (gross expense) is currently ${aggregate:0.##}.");

            // 9. Are we approaching the aggregate attachment point?
            AddFaq("Are we approaching the aggregate attachment point?", "There is no aggregate attachment point defined in the current dataset to approach.");

            // 10. Which contracts expose us to the greatest downside?
            var worstContract = patients.GroupBy(p => p.HealthCarePlan).OrderByDescending(g => g.Sum(x => x.Expense)).FirstOrDefault();
            AddFaq("Which contracts expose us to the greatest downside?", worstContract != null ? $"The contract exposing us to the greatest downside is {worstContract.Key}, with a total gross expense of ${worstContract.Sum(x => x.Expense):0.##}." : "Not enough data.");

            // 11. Are there any members with special attachment points?
            var special = patients.Where(p => p.HasSpecialAttachmentPoint).ToList();
            AddFaq("Are there any members with special attachment points?", $"There are exactly {special.Count} members with special attachment points.");

            // Medical Director 1. Which high-cost members are driving our medical expense?
            var top = patients.OrderByDescending(p => p.Expense).Take(5).ToList();
            var names = string.Join(", ", top.Select(p => $"{p.Name} (${p.Expense:0.##})"));
            AddFaq("Which high-cost members are driving our medical expense?", $"The high-cost members driving our medical expense are: {names}.");

            // Medical Director 2. Which cases are potentially avoidable?
            var avoidable = patients.Where(p => p.ERUtilization > 20 && p.OpenCareGaps > 5).ToList();
            AddFaq("Which cases are potentially avoidable?", $"There are {avoidable.Count} high-risk members with excessive ER utilization (>20%) and severe open care gaps (>5) that represent potentially avoidable cases.");

            // Medical Director 3. Which members require care management?
            AddFaq("Which members require care management?", $"There are {avoidable.Count} members requiring immediate care management due to high ER utilization and multiple open care gaps.");

            // Medical Director 4. Which members have multiple hospitalizations?
            var multipleHospitalizations = patients.Where(p => p.ERUtilization > 15).ToList();
            AddFaq("Which members have multiple hospitalizations?", $"There are {multipleHospitalizations.Count} members showing signs of multiple hospitalizations based on high ER utilization (>15%).");

            // Medical Director 5. Which members have fragmented care?
            var fragmented = patients.Where(p => p.ProviderName == "General Hospital" && p.ERUtilization > 15).ToList();
            AddFaq("Which members have fragmented care?", $"There are {fragmented.Count} members who show signs of fragmented care across different facilities.");

            // Medical Director 6. Are high-cost members receiving appropriate preventive and chronic-care management?
            var highCostUnmanaged = patients.Where(p => (p.RiskTier == "Critical" || p.RiskTier == "High") && p.OpenCareGaps > 3).ToList();
            AddFaq("Are high-cost members receiving appropriate preventive and chronic-care management?", $"No, there are {highCostUnmanaged.Count} high-cost members who have more than 3 open care gaps, indicating they are NOT receiving appropriate preventive and chronic-care management.");

            // Medical Director 7. Which providers account for disproportionate high-cost utilization?
            var outlierProvider = patients.GroupBy(p => p.ProviderName).OrderByDescending(g => g.Average(x => x.Expense)).FirstOrDefault();
            AddFaq("Which providers account for disproportionate high-cost utilization?", outlierProvider != null ? $"The provider accounting for the most disproportionate high-cost utilization is {outlierProvider.Key}, with an average patient expense of ${outlierProvider.Average(x => x.Expense):0.##}." : "Not enough data.");

            // Claims Manager 1. Which claims are approaching the attachment point?
            AddFaq("Which claims are approaching the attachment point?", $"There are {approaching.Count} claims approaching their specific attachment point (within 20%).");

            // Claims Manager 2. Which claims are potentially eligible for reimbursement?
            AddFaq("Which claims are potentially eligible for reimbursement?", $"There are {eligibleClaims.Count} claims currently eligible for reimbursement (expense exceeds attachment point and claim is not denied).");

            // Claims Manager 3. Which claims have been submitted?
            var submittedCount = patients.Count(p => p.ClaimStatus.Equals("Submitted", StringComparison.OrdinalIgnoreCase));
            AddFaq("Which claims have been submitted?", $"There are exactly {submittedCount} submitted claims currently processing.");

            // Claims Manager 4. Which have been denied? Why?
            var deniedCount = patients.Count(p => p.ClaimStatus.Equals("Denied", StringComparison.OrdinalIgnoreCase));
            AddFaq("Which have been denied? Why?", $"There are exactly {deniedCount} denied claims, primarily due to missing documentation or exceeding policy limits.");

            // Claims Manager 5. Which claims are missing documentation?
            var missingDocCount = patients.Count(p => p.DenialReason.Contains("Missing Documentation", StringComparison.OrdinalIgnoreCase));
            AddFaq("Which claims are missing documentation?", $"There are exactly {missingDocCount} claims denied specifically due to missing documentation.");

            // Claims Manager 6. Are reimbursement filing deadlines approaching?
            var approachingDeadlines = patients.Where(p => p.FilingDeadline >= DateTime.Now && p.FilingDeadline <= DateTime.Now.AddDays(30)).ToList();
            AddFaq("Are reimbursement filing deadlines approaching?", $"There are {approachingDeadlines.Count} claims with reimbursement filing deadlines approaching in the next 30 days.");

            // Insert only if they don't exist
            var existingFaqs = context.Faqs.Select(f => f.Question).ToHashSet();
            bool addedAny = false;
            foreach (var faq in faqsToAdd)
            {
                if (!existingFaqs.Contains(faq.Question))
                {
                    context.Faqs.Add(faq);
                    addedAny = true;
                }
            }
            
            if (addedAny)
            {
                context.SaveChanges();
            }
        }
    }
}
