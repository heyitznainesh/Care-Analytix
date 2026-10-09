using System;

namespace CareAnalytixWebChat.Models
{
    public class PatientFaq
    {
        public int PatientId { get; set; }
        public Patient Patient { get; set; } = null!;

        public int FaqId { get; set; }
        public Faq Faq { get; set; } = null!;
    }
}
