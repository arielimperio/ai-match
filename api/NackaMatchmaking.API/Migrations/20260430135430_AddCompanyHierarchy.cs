using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NackaMatchmaking.API.Migrations
{
    /// <inheritdoc />
    public partial class AddCompanyHierarchy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ParentId",
                table: "Companies",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "Companies",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000002"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Companies",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111111"),
                column: "ParentId",
                value: null);

            migrationBuilder.CreateIndex(
                name: "IX_Companies_ParentId",
                table: "Companies",
                column: "ParentId");

            migrationBuilder.AddForeignKey(
                name: "FK_Companies_Companies_ParentId",
                table: "Companies",
                column: "ParentId",
                principalTable: "Companies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Companies_Companies_ParentId",
                table: "Companies");

            migrationBuilder.DropIndex(
                name: "IX_Companies_ParentId",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "ParentId",
                table: "Companies");
        }
    }
}
