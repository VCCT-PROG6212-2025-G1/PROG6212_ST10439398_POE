using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CMCS.Migrations
{
    /// <inheritdoc />
    public partial class AddMissingUserColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Modules_Users_UserId",
                table: "Modules");

            migrationBuilder.DropIndex(
                name: "IX_Modules_UserId",
                table: "Modules");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "Modules");

            migrationBuilder.AlterColumn<string>(
                name: "PhoneNumber",
                table: "Users",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20);

            migrationBuilder.AddColumn<string>(
                name: "Campus",
                table: "Users",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Department",
                table: "Users",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Faculty",
                table: "Users",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "HourlyRate",
                table: "Users",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastModified",
                table: "Users",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PasswordHash",
                table: "Users",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: false,
                defaultValue: "");

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "UserId",
                keyValue: 1,
                columns: new[] { "Campus", "Department", "Faculty", "HourlyRate", "LastModified", "PasswordHash" },
                values: new object[] { null, null, null, 450.00m, null, "$2a$11$1Fu7XrWCp1e2zJSCKOJH0..GAMCb4Qbq02qkWLmugIOoyuJZ8V9Ma" });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "UserId",
                keyValue: 2,
                columns: new[] { "Campus", "Department", "Faculty", "HourlyRate", "LastModified", "PasswordHash" },
                values: new object[] { null, null, null, 0m, null, "$2a$11$1Fu7XrWCp1e2zJSCKOJH0..GAMCb4Qbq02qkWLmugIOoyuJZ8V9Ma" });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "UserId",
                keyValue: 3,
                columns: new[] { "Campus", "Department", "Faculty", "HourlyRate", "LastModified", "PasswordHash" },
                values: new object[] { null, null, null, 0m, null, "$2a$11$1Fu7XrWCp1e2zJSCKOJH0..GAMCb4Qbq02qkWLmugIOoyuJZ8V9Ma" });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "UserId",
                keyValue: 4,
                columns: new[] { "Campus", "Department", "Faculty", "HourlyRate", "LastModified", "PasswordHash" },
                values: new object[] { null, null, null, 420.00m, null, "$2a$11$1Fu7XrWCp1e2zJSCKOJH0..GAMCb4Qbq02qkWLmugIOoyuJZ8V9Ma" });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "UserId",
                keyValue: 5,
                columns: new[] { "Campus", "Department", "Faculty", "HourlyRate", "LastModified", "PasswordHash" },
                values: new object[] { null, null, null, 0m, null, "$2a$11$1Fu7XrWCp1e2zJSCKOJH0..GAMCb4Qbq02qkWLmugIOoyuJZ8V9Ma" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Campus",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Department",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Faculty",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "HourlyRate",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "LastModified",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "PasswordHash",
                table: "Users");

            migrationBuilder.AlterColumn<string>(
                name: "PhoneNumber",
                table: "Users",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20,
                oldNullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UserId",
                table: "Modules",
                type: "int",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "Modules",
                keyColumn: "ModuleId",
                keyValue: 1,
                column: "UserId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Modules",
                keyColumn: "ModuleId",
                keyValue: 2,
                column: "UserId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Modules",
                keyColumn: "ModuleId",
                keyValue: 3,
                column: "UserId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Modules",
                keyColumn: "ModuleId",
                keyValue: 4,
                column: "UserId",
                value: null);

            migrationBuilder.CreateIndex(
                name: "IX_Modules_UserId",
                table: "Modules",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Modules_Users_UserId",
                table: "Modules",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "UserId");
        }
    }
}
