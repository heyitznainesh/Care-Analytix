using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CareAnalytixWebChat.Models
{
    public class Patient
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        
        public string Name { get; set; } = string.Empty;
        public decimal Expense { get; set; }
        public string HealthCarePlan { get; set; } = string.Empty;
        public decimal Pmpm { get; set; }
        public string RiskTier { get; set; } = string.Empty;
        public double ERUtilization { get; set; }
        public int OpenCareGaps { get; set; }
        public decimal SpecificAttachmentPoint { get; set; }
        public string ClaimStatus { get; set; } = string.Empty;
        public string DenialReason { get; set; } = string.Empty;
        public DateTime FilingDeadline { get; set; }
        public string ProviderName { get; set; } = string.Empty;
        public bool HasSpecialAttachmentPoint { get; set; }

        public ICollection<PatientFaq> PatientFaqs { get; set; } = new List<PatientFaq>();
    }
}
