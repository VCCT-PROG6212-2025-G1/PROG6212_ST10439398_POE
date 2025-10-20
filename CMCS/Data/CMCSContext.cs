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
                    IsActive = true
                },
                new User
                {
                    UserId = 2,
                    FirstName = "Jane",
                    LastName = "Smith",
                    Email = "jane.smith@iie.ac.za",
                    PhoneNumber = "+27 11 123 4568",
                    UserRole = UserRole.Coordinator,
                    IsActive = true
                },
                new User
                {
                    UserId = 3,
                    FirstName = "Mike",
                    LastName = "Wilson",
                    Email = "mike.wilson@iie.ac.za",
                    PhoneNumber = "+27 11 123 4569",
                    UserRole = UserRole.Manager,
                    IsActive = true
                }
            );

            modelBuilder.Entity<Module>().HasData(
                new Module
                {
                    ModuleId = 1,
                    ModuleCode = "PROG6212",
                    ModuleName = "Programming 2B",
                    StandardHourlyRate = 450.00m,
                    IsActive = true
                },
                new Module
                {
                    ModuleId = 2,
                    ModuleCode = "HCIN6212",
                    ModuleName = "Human Computer Interaction",
                    StandardHourlyRate = 450.00m,
                    IsActive = true
                },
                new Module
                {
                    ModuleId = 3,
                    ModuleCode = "PROG5112",
                    ModuleName = "Programming 1B",
                    StandardHourlyRate = 450.00m,
                    IsActive = true
                },
                new Module
                {
                    ModuleId = 4,
                    ModuleCode = "WEDE5020",
                    ModuleName = "Web Development",
                    StandardHourlyRate = 450.00m,
                    IsActive = true
                }
            );
        }
    }
}