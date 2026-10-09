using System;

namespace CareAnalytixWebChat.Models
{
    public class Faq
    {
        public int Id { get; set; }
        public string Question { get; set; } = string.Empty;
        public string Answer { get; set; } = string.Empty;
        public int CategoryId { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
        public string CreatedDate { get; set; } = string.Empty;
        public string ChangedBy { get; set; } = string.Empty;
        public string ChangedDate { get; set; } = string.Empty;
        public string DeletedBy { get; set; } = string.Empty;
        public string IsDeleted { get; set; } = string.Empty;
        public string IsActive { get; set; } = string.Empty;
        public string CreatedUserType { get; set; } = string.Empty;
        public string ChangedUserType { get; set; } = string.Empty;
        public string IsTopFaq { get; set; } = string.Empty;
        public string OrderNo { get; set; } = string.Empty;
        public string TopOrderNo { get; set; } = string.Empty;
        
        public ICollection<PatientFaq> PatientFaqs { get; set; } = new List<PatientFaq>();
    }
}
