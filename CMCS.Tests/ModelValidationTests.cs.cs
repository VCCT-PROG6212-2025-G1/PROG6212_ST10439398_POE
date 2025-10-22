using Xunit;
using CMCS.Models;
using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;
using System.Linq;

namespace CMCS.Tests
{
    public class ModelValidationTests
    {
        private IList<ValidationResult> ValidateModel(object model)
        {
            var validationResults = new List<ValidationResult>();
            var ctx = new ValidationContext(model, null, null);
            Validator.TryValidateObject(model, ctx, validationResults, true);
            return validationResults;
        }

        [Fact]
        public void Claim_ValidData_PassesValidation()
        {
            // Arrange
            var claim = new Claim
            {
                UserId = 1,
                ModuleId = 1,
                HoursWorked = 20,
                HourlyRate = 420,
                TotalAmount = 8400,
                ClaimPeriod = "2025-04"
            };

            // Act
            var results = ValidateModel(claim);

            // Assert
            Assert.Empty(results);
        }

        [Fact]
        public void User_ValidEmail_PassesValidation()
        {
            // Arrange
            var user = new User
            {
                Email = "test@example.com",
                FirstName = "Test",
                LastName = "User",
                UserRole = UserRole.Lecturer
            };

            // Act
            var results = ValidateModel(user);

            // Assert
            Assert.Empty(results);
        }

        [Fact]
        public void User_InvalidEmail_FailsValidation()
        {
            // Arrange
            var user = new User
            {
                Email = "invalid-email",
                FirstName = "Test",
                LastName = "User",
                UserRole = UserRole.Lecturer
            };

            // Act
            var results = ValidateModel(user);

            // Assert
            Assert.NotEmpty(results);
            Assert.Contains(results, r => r.MemberNames.Contains("Email"));
        }

        [Fact]
        public void Module_ValidData_PassesValidation()
        {
            // Arrange
            var module = new Module
            {
                ModuleCode = "TEST101",
                ModuleName = "Test Module",
                Description = "Test description",
                StandardHourlyRate = 420
            };

            // Act
            var results = ValidateModel(module);

            // Assert
            Assert.Empty(results);
        }

        [Fact]
        public void Module_InvalidHourlyRate_FailsValidation()
        {
            // Arrange
            var module = new Module
            {
                ModuleCode = "TEST101",
                ModuleName = "Test Module",
                Description = "Test description",
                StandardHourlyRate = -100
            };

            // Act
            var results = ValidateModel(module);

            // Assert
            Assert.NotEmpty(results);
        }
    }
}