using Microsoft.EntityFrameworkCore;
using CMCS.Models;

namespace CMCS.Data
{
    public class CMCSContext : DbContext
    {
        public CMCSContext(DbContextOptions<CMCSContext> options) : base(options)
        {
        }

        public DbSet<User> Users { get; set; }
        public DbSet<Module> Modules { get; set; }
        public DbSet<Claim> Claims { get; set; }
        public DbSet<ClaimStatusHistory> ClaimStatusHistories { get; set; }
        public DbSet<SupportingDocument> SupportingDocuments { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // User Configuration
            modelBuilder.Entity<User>(entity =>
            {
                entity.HasKey(u => u.UserId);
                entity.Property(u => u.FirstName).IsRequired().HasMaxLength(100);
                entity.Property(u => u.LastName).IsRequired().HasMaxLength(100);
                entity.Property(u => u.Email).IsRequired().HasMaxLength(255);
                entity.Property(u => u.PhoneNumber).HasMaxLength(20);
                entity.HasIndex(u => u.Email).IsUnique();
            });

            // Module Configuration
            modelBuilder.Entity<Module>(entity =>
            {
                entity.HasKey(m => m.ModuleId);
                entity.Property(m => m.ModuleCode).IsRequired().HasMaxLength(20);
                entity.Property(m => m.ModuleName).IsRequired().HasMaxLength(200);
                entity.Property(m => m.Description).IsRequired().HasMaxLength(500);
                entity.Property(m => m.StandardHourlyRate).HasColumnType("decimal(18,2)");
                entity.HasIndex(m => m.ModuleCode).IsUnique();
            });

            // Claim Configuration
            modelBuilder.Entity<Claim>(entity =>
            {
                entity.HasKey(c => c.ClaimId);
                entity.Property(c => c.HoursWorked).HasColumnType("decimal(18,2)");
                entity.Property(c => c.HourlyRate).HasColumnType("decimal(18,2)");
                entity.Property(c => c.TotalAmount).HasColumnType("decimal(18,2)");
                entity.Property(c => c.ClaimPeriod).IsRequired().HasMaxLength(20);

                // Relationships
                entity.HasOne(c => c.User)
                    .WithMany(u => u.Claims)
                    .HasForeignKey(c => c.UserId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(c => c.Module)
                    .WithMany(m => m.Claims)
                    .HasForeignKey(c => c.ModuleId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // ClaimStatusHistory Configuration
            modelBuilder.Entity<ClaimStatusHistory>(entity =>
            {
                entity.HasKey(csh => csh.StatusId);

                entity.HasOne(csh => csh.Claim)
                    .WithMany(c => c.StatusHistory)
                    .HasForeignKey(csh => csh.ClaimId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(csh => csh.User)
                    .WithMany()
                    .HasForeignKey(csh => csh.ChangedBy)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // SupportingDocument Configuration
            modelBuilder.Entity<SupportingDocument>(entity =>
            {
                entity.HasKey(sd => sd.DocumentId);
                entity.Property(sd => sd.FileName).IsRequired().HasMaxLength(255);
                entity.Property(sd => sd.FilePath).IsRequired().HasMaxLength(500);
                entity.Property(sd => sd.FileType).HasMaxLength(50);

                entity.HasOne(sd => sd.Claim)
                    .WithMany(c => c.SupportingDocuments)
                    .HasForeignKey(sd => sd.ClaimId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Seed Data
            SeedData(modelBuilder);
        }

        private void SeedData(ModelBuilder modelBuilder)
        {
            // Seed Users
            modelBuilder.Entity<User>().HasData(
                new User
                {
                    UserId = 1,
                    FirstName = "John",
                    LastName = "Lecturer",
                    Email = "john.lecturer@iie.ac.za",
                    PhoneNumber = "+27 11 123 4567",
                    UserRole = UserRole.Lecturer,
                    IsActive = true,
                    CreatedDate = DateTime.Now
                },
                new User
                {
                    UserId = 2,
                    FirstName = "Jane",
                    LastName = "Coordinator",
                    Email = "jane.coordinator@iie.ac.za",
                    PhoneNumber = "+27 11 234 5678",
                    UserRole = UserRole.Coordinator,
                    IsActive = true,
                    CreatedDate = DateTime.Now
                },
                new User
                {
                    UserId = 3,
                    FirstName = "Mike",
                    LastName = "Manager",
                    Email = "mike.manager@iie.ac.za",
                    PhoneNumber = "+27 11 345 6789",
                    UserRole = UserRole.Manager,
                    IsActive = true,
                    CreatedDate = DateTime.Now
                },
                new User
                {
                    UserId = 4,
                    FirstName = "Sarah",
                    LastName = "Davis",
                    Email = "sarah.davis@iie.ac.za",
                    PhoneNumber = "+27 11 456 7890",
                    UserRole = UserRole.Lecturer,
                    IsActive = true,
                    CreatedDate = DateTime.Now
                },
                new User
                {
                    UserId = 5,
                    FirstName = "Emily",
                    LastName = "HR",
                    Email = "emily.hr@iie.ac.za",
                    PhoneNumber = "+27 11 567 8901",
                    UserRole = UserRole.HR,
                    IsActive = true,
                    CreatedDate = DateTime.Now
                }
            );

            // Seed Modules
            modelBuilder.Entity<Module>().HasData(
                new Module
                {
                    ModuleId = 1,
                    ModuleCode = "PROG6212",
                    ModuleName = "Programming 2B",
                    Description = "Advanced programming concepts in C# and .NET development",
                    StandardHourlyRate = 450.00m,
                    IsActive = true,
                    CreatedDate = DateTime.Now
                },
                new Module
                {
                    ModuleId = 2,
                    ModuleCode = "HCIN6212",
                    ModuleName = "Human Computer Interaction",
                    Description = "User interface design and usability principles",
                    StandardHourlyRate = 420.00m,
                    IsActive = true,
                    CreatedDate = DateTime.Now
                },
                new Module
                {
                    ModuleId = 3,
                    ModuleCode = "PROG5112",
                    ModuleName = "Programming 1B",
                    Description = "Introduction to programming fundamentals and logic",
                    StandardHourlyRate = 400.00m,
                    IsActive = true,
                    CreatedDate = DateTime.Now
                },
                new Module
                {
                    ModuleId = 4,
                    ModuleCode = "WEDE5020",
                    ModuleName = "Web Development",
                    Description = "Modern web development with HTML, CSS, and JavaScript",
                    StandardHourlyRate = 430.00m,
                    IsActive = true,
                    CreatedDate = DateTime.Now
                }
            );

            // Seed Some Sample Claims (Optional - for demo purposes)
            modelBuilder.Entity<Claim>().HasData(
                new Claim
                {
                    ClaimId = 1,
                    UserId = 1,
                    ModuleId = 1,
                    HoursWorked = 25.5m,
                    HourlyRate = 450.00m,
                    TotalAmount = 11475.00m,
                    ClaimPeriod = "2024-10",
                    CurrentStatus = ClaimStatus.Submitted,
                    SubmissionDate = DateTime.Now.AddDays(-6),
                    AdditionalNotes = "October 2024 teaching hours for Programming 2B"
                },
                new Claim
                {
                    ClaimId = 2,
                    UserId = 1,
                    ModuleId = 2,
                    HoursWorked = 18.0m,
                    HourlyRate = 420.00m,
                    TotalAmount = 7560.00m,
                    ClaimPeriod = "2024-10",
                    CurrentStatus = ClaimStatus.Approved,
                    SubmissionDate = DateTime.Now.AddDays(-13),
                    LastModified = DateTime.Now.AddDays(-5),
                    AdditionalNotes = "October 2024 teaching hours for HCI"
                },
                new Claim
                {
                    ClaimId = 3,
                    UserId = 4,
                    ModuleId = 3,
                    HoursWorked = 22.5m,
                    HourlyRate = 400.00m,
                    TotalAmount = 9000.00m,
                    ClaimPeriod = "2024-10",
                    CurrentStatus = ClaimStatus.Approved,
                    SubmissionDate = DateTime.Now.AddDays(-20),
                    LastModified = DateTime.Now.AddDays(-12),
                    AdditionalNotes = "October 2024 teaching hours for Programming 1B"
                }
            );

            // Seed Status History
            modelBuilder.Entity<ClaimStatusHistory>().HasData(
                new ClaimStatusHistory
                {
                    StatusId = 1,
                    ClaimId = 1,
                    ChangedBy = 1,
                    PreviousStatus = ClaimStatus.Draft,
                    NewStatus = ClaimStatus.Submitted,
                    ChangeDate = DateTime.Now.AddDays(-6),
                    Comments = "Claim submitted by lecturer"
                },
                new ClaimStatusHistory
                {
                    StatusId = 2,
                    ClaimId = 2,
                    ChangedBy = 1,
                    PreviousStatus = ClaimStatus.Draft,
                    NewStatus = ClaimStatus.Submitted,
                    ChangeDate = DateTime.Now.AddDays(-13),
                    Comments = "Claim submitted by lecturer"
                },
                new ClaimStatusHistory
                {
                    StatusId = 3,
                    ClaimId = 2,
                    ChangedBy = 2,
                    PreviousStatus = ClaimStatus.Submitted,
                    NewStatus = ClaimStatus.Approved,
                    ChangeDate = DateTime.Now.AddDays(-5),
                    Comments = "Claim approved by Programme Coordinator"
                },
                new ClaimStatusHistory
                {
                    StatusId = 4,
                    ClaimId = 3,
                    ChangedBy = 4,
                    PreviousStatus = ClaimStatus.Draft,
                    NewStatus = ClaimStatus.Submitted,
                    ChangeDate = DateTime.Now.AddDays(-20),
                    Comments = "Claim submitted by lecturer"
                },
                new ClaimStatusHistory
                {
                    StatusId = 5,
                    ClaimId = 3,
                    ChangedBy = 2,
                    PreviousStatus = ClaimStatus.Submitted,
                    NewStatus = ClaimStatus.Approved,
                    ChangeDate = DateTime.Now.AddDays(-12),
                    Comments = "Claim approved by Programme Coordinator"
                }
            );
        }
    }
}