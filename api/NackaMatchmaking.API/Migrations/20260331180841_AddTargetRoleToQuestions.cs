using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NackaMatchmaking.API.Migrations
{
    /// <inheritdoc />
    public partial class AddTargetRoleToQuestions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TargetRole",
                table: "Questions",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.UpdateData(
                table: "Questions",
                keyColumns: new[] { "CompanyId", "Id" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), "q1" },
                column: "TargetRole",
                value: "All");

            migrationBuilder.UpdateData(
                table: "Questions",
                keyColumns: new[] { "CompanyId", "Id" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), "q2" },
                column: "TargetRole",
                value: "All");

            migrationBuilder.UpdateData(
                table: "Questions",
                keyColumns: new[] { "CompanyId", "Id" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), "q3" },
                column: "TargetRole",
                value: "All");

            migrationBuilder.UpdateData(
                table: "Questions",
                keyColumns: new[] { "CompanyId", "Id" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), "q4" },
                column: "TargetRole",
                value: "All");

            migrationBuilder.UpdateData(
                table: "Questions",
                keyColumns: new[] { "CompanyId", "Id" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), "q5" },
                column: "TargetRole",
                value: "All");

            migrationBuilder.UpdateData(
                table: "Settings",
                keyColumns: new[] { "CompanyId", "Key" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), "WelcomeLogo" },
                column: "Value",
                value: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TargetRole",
                table: "Questions");

            migrationBuilder.UpdateData(
                table: "Settings",
                keyColumns: new[] { "CompanyId", "Key" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), "WelcomeLogo" },
                column: "Value",
                value: "assets/logo.png");
        }
    }
}
