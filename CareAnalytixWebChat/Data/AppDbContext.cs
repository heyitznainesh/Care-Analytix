using Microsoft.EntityFrameworkCore;

namespace CareAnalytixWebChat.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<Models.Patient> Patients { get; set; }
        public DbSet<Models.Faq> Faqs { get; set; }
        public DbSet<Models.PatientFaq> PatientFaqs { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Models.Patient>()
                .Property(p => p.Expense)
                .HasPrecision(18, 2);

            modelBuilder.Entity<Models.Patient>()
                .Property(p => p.Pmpm)
                .HasPrecision(18, 2);

            modelBuilder.Entity<Models.Patient>()
                .Property(p => p.SpecificAttachmentPoint)
                .HasPrecision(18, 2);

            modelBuilder.Entity<Models.PatientFaq>()
                .HasKey(pf => new { pf.PatientId, pf.FaqId });

            modelBuilder.Entity<Models.PatientFaq>()
                .HasOne(pf => pf.Patient)
                .WithMany(p => p.PatientFaqs)
                .HasForeignKey(pf => pf.PatientId);

            modelBuilder.Entity<Models.PatientFaq>()
                .HasOne(pf => pf.Faq)
                .WithMany(f => f.PatientFaqs)
                .HasForeignKey(pf => pf.FaqId);

            base.OnModelCreating(modelBuilder);
        }
    }
}
