using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace CMCS.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Modules",
                columns: table => new
                {
                    ModuleId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ModuleCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ModuleName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    StandardHourlyRate = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModified = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Modules", x => x.ModuleId);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FirstName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    LastName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    UserRole = table.Column<int>(type: "int", nullable: false),
                    PhoneNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Department = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Faculty = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Campus = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    HourlyRate = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModified = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.UserId);
                });

            migrationBuilder.CreateTable(
                name: "Claims",
                columns: table => new
                {
                    ClaimId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    ModuleId = table.Column<int>(type: "int", nullable: false),
                    HoursWorked = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    HourlyRate = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ClaimPeriod = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    AdditionalNotes = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CurrentStatus = table.Column<int>(type: "int", nullable: false),
                    SubmissionDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModified = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Claims", x => x.ClaimId);
                    table.ForeignKey(
                        name: "FK_Claims_Modules_ModuleId",
                        column: x => x.ModuleId,
                        principalTable: "Modules",
                        principalColumn: "ModuleId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Claims_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ClaimStatusHistories",
                columns: table => new
                {
                    StatusId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ClaimId = table.Column<int>(type: "int", nullable: false),
                    ChangedBy = table.Column<int>(type: "int", nullable: true),
                    PreviousStatus = table.Column<int>(type: "int", nullable: false),
                    NewStatus = table.Column<int>(type: "int", nullable: false),
                    ChangeDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Comments = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClaimStatusHistories", x => x.StatusId);
                    table.ForeignKey(
                        name: "FK_ClaimStatusHistories_Claims_ClaimId",
                        column: x => x.ClaimId,
                        principalTable: "Claims",
                        principalColumn: "ClaimId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ClaimStatusHistories_Users_ChangedBy",
                        column: x => x.ChangedBy,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SupportingDocuments",
                columns: table => new
                {
                    DocumentId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ClaimId = table.Column<int>(type: "int", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    FilePath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    FileSize = table.Column<long>(type: "bigint", nullable: false),
                    FileType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupportingDocuments", x => x.DocumentId);
                    table.ForeignKey(
                        name: "FK_SupportingDocuments_Claims_ClaimId",
                        column: x => x.ClaimId,
                        principalTable: "Claims",
                        principalColumn: "ClaimId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Modules",
                columns: new[] { "ModuleId", "CreatedDate", "Description", "IsActive", "LastModified", "ModuleCode", "ModuleName", "StandardHourlyRate" },
                values: new object[,]
                {
                    { 1, new DateTime(2024, 10, 1, 9, 0, 0, 0, DateTimeKind.Unspecified), "Advanced programming concepts in C# and .NET development", true, null, "PROG6212", "Programming 2B", 450.00m },
                    { 2, new DateTime(2024, 10, 1, 9, 0, 0, 0, DateTimeKind.Unspecified), "User interface design and usability principles", true, null, "HCIN6212", "Human Computer Interaction", 420.00m },
                    { 3, new DateTime(2024, 10, 1, 9, 0, 0, 0, DateTimeKind.Unspecified), "Introduction to programming fundamentals and logic", true, null, "PROG5112", "Programming 1B", 400.00m },
                    { 4, new DateTime(2024, 10, 1, 9, 0, 0, 0, DateTimeKind.Unspecified), "Modern web development with HTML, CSS, and JavaScript", true, null, "WEDE5020", "Web Development", 430.00m }
                });

            migrationBuilder.InsertData(
                table: "Users",
                columns: new[] { "UserId", "Campus", "CreatedDate", "Department", "Email", "Faculty", "FirstName", "HourlyRate", "IsActive", "LastModified", "LastName", "PasswordHash", "PhoneNumber", "UserRole" },
                values: new object[,]
                {
                    { 1, null, new DateTime(2024, 10, 1, 9, 0, 0, 0, DateTimeKind.Unspecified), null, "john.lecturer@iie.ac.za", null, "John", 450.00m, true, null, "Lecturer", "$2a$11$2D61u8DrV007EQa2ITzb2eNgRRntqXl0JkkEXSi5fm2et0gdCywsy", "+27 11 123 4567", 0 },
                    { 2, null, new DateTime(2024, 10, 1, 9, 0, 0, 0, DateTimeKind.Unspecified), null, "jane.coordinator@iie.ac.za", null, "Jane", 0m, true, null, "Coordinator", "$2a$11$2D61u8DrV007EQa2ITzb2eNgRRntqXl0JkkEXSi5fm2et0gdCywsy", "+27 11 234 5678", 1 },
                    { 3, null, new DateTime(2024, 10, 1, 9, 0, 0, 0, DateTimeKind.Unspecified), null, "mike.manager@iie.ac.za", null, "Mike", 0m, true, null, "Manager", "$2a$11$2D61u8DrV007EQa2ITzb2eNgRRntqXl0JkkEXSi5fm2et0gdCywsy", "+27 11 345 6789", 2 },
                    { 4, null, new DateTime(2024, 10, 1, 9, 0, 0, 0, DateTimeKind.Unspecified), null, "sarah.davis@iie.ac.za", null, "Sarah", 420.00m, true, null, "Davis", "$2a$11$2D61u8DrV007EQa2ITzb2eNgRRntqXl0JkkEXSi5fm2et0gdCywsy", "+27 11 456 7890", 0 },
                    { 5, null, new DateTime(2024, 10, 1, 9, 0, 0, 0, DateTimeKind.Unspecified), null, "emily.hr@iie.ac.za", null, "Emily", 0m, true, null, "HR", "$2a$11$2D61u8DrV007EQa2ITzb2eNgRRntqXl0JkkEXSi5fm2et0gdCywsy", "+27 11 567 8901", 3 }
                });

            migrationBuilder.InsertData(
                table: "Claims",
                columns: new[] { "ClaimId", "AdditionalNotes", "ClaimPeriod", "CurrentStatus", "HourlyRate", "HoursWorked", "LastModified", "ModuleId", "SubmissionDate", "TotalAmount", "UserId" },
                values: new object[,]
                {
                    { 1, "October 2024 teaching hours for Programming 2B", "2024-10", 1, 450.00m, 25.5m, null, 1, new DateTime(2024, 9, 25, 9, 0, 0, 0, DateTimeKind.Unspecified), 11475.00m, 1 },
                    { 2, "October 2024 teaching hours for HCI", "2024-10", 3, 420.00m, 18.0m, new DateTime(2024, 9, 26, 9, 0, 0, 0, DateTimeKind.Unspecified), 2, new DateTime(2024, 9, 18, 9, 0, 0, 0, DateTimeKind.Unspecified), 7560.00m, 1 }
                });

            migrationBuilder.InsertData(
                table: "ClaimStatusHistories",
                columns: new[] { "StatusId", "ChangeDate", "ChangedBy", "ClaimId", "Comments", "NewStatus", "PreviousStatus" },
                values: new object[,]
                {
                    { 1, new DateTime(2024, 9, 25, 9, 0, 0, 0, DateTimeKind.Unspecified), 1, 1, "Claim submitted by lecturer", 1, 0 },
                    { 2, new DateTime(2024, 9, 18, 9, 0, 0, 0, DateTimeKind.Unspecified), 1, 2, "Claim submitted by lecturer", 1, 0 },
                    { 3, new DateTime(2024, 9, 26, 9, 0, 0, 0, DateTimeKind.Unspecified), 2, 2, "Claim approved by Programme Coordinator", 3, 1 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Claims_ModuleId",
                table: "Claims",
                column: "ModuleId");

            migrationBuilder.CreateIndex(
                name: "IX_Claims_UserId",
                table: "Claims",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ClaimStatusHistories_ChangedBy",
                table: "ClaimStatusHistories",
                column: "ChangedBy");

            migrationBuilder.CreateIndex(
                name: "IX_ClaimStatusHistories_ClaimId",
                table: "ClaimStatusHistories",
                column: "ClaimId");

            migrationBuilder.CreateIndex(
                name: "IX_Modules_ModuleCode",
                table: "Modules",
                column: "ModuleCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SupportingDocuments_ClaimId",
                table: "SupportingDocuments",
                column: "ClaimId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClaimStatusHistories");

            migrationBuilder.DropTable(
                name: "SupportingDocuments");

            migrationBuilder.DropTable(
                name: "Claims");

            migrationBuilder.DropTable(
                name: "Modules");

            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}
