using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NackaMatchmaking.API.Migrations
{
    /// <inheritdoc />
    public partial class AddFeedbackRequestSentAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "FeedbackRequestSentAt",
                table: "Participants",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FeedbackRequestSentAt",
                table: "Participants");
        }
    }
}
