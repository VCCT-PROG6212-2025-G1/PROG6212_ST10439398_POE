using Microsoft.EntityFrameworkCore;
using CMCS.Models;

namespace CMCS.Data
{
    public class CMCSContext : DbContext
    {
        public CMCSContext(DbContextOptions<CMCSContext> options)
            : base(options)
        {
        }

        public DbSet<User> Users { get; set; }
        public DbSet<Module> Modules { get; set; }
        public DbSet<Claim> Claims { get; set; }
        public DbSet<SupportingDocument> SupportingDocuments { get; set; }
        public DbSet<ClaimStatusHistory> ClaimStatusHistories { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Configure many-to-many relationship between User and Module
            modelBuilder.Entity<User>()
                .HasMany(u => u.Modules)
                .WithMany(m => m.Lecturers)
                .UsingEntity(j => j.ToTable("UserModules"));

            // Seed initial data
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
                }
            );

            // Seed modules
            modelBuilder.Entity<Module>().HasData(
                new Module
                {
                    ModuleId = 1,
                    ModuleCode = "PROG6212",
                    ModuleName = "Programming 2B",
                    StandardHourlyRate = 350.00m,
                    IsActive = true
                },
                new Module
                {
                    ModuleId = 2,
                    ModuleCode = "CLDV6212",
                    ModuleName = "Cloud Development",
                    StandardHourlyRate = 375.00m,
                    IsActive = true
                },
                new Module
                {
                    ModuleId = 3,
                    ModuleCode = "DATA6222",
                    ModuleName = "Database Development",
                    StandardHourlyRate = 400.00m,
                    IsActive = true
                }
            );
        }
    }
}