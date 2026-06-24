using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NackaMatchmaking.API.Migrations
{
    /// <inheritdoc />
    public partial class AddMeetingSlots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MatchmakingEvent",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    EventStartTime = table.Column<TimeSpan>(type: "time", nullable: true),
                    EventEndTime = table.Column<TimeSpan>(type: "time", nullable: true),
                    SlotDurationMinutes = table.Column<int>(type: "int", nullable: false),
                    BreakStartTime = table.Column<TimeSpan>(type: "time", nullable: true),
                    BreakEndTime = table.Column<TimeSpan>(type: "time", nullable: true),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchmakingEvent", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MatchmakingEvent_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MeetingSlots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MatchmakingEventId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StartTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsAvailable = table.Column<bool>(type: "bit", nullable: false),
                    AssignedStudentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    StudentCheckedIn = table.Column<bool>(type: "bit", nullable: false),
                    StudentDeclined = table.Column<bool>(type: "bit", nullable: false),
                    CompanyMarkedNoShow = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MeetingSlots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MeetingSlots_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MeetingSlots_MatchmakingEvent_MatchmakingEventId",
                        column: x => x.MatchmakingEventId,
                        principalTable: "MatchmakingEvent",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MeetingSlots_Participants_AssignedStudentId",
                        column: x => x.AssignedStudentId,
                        principalTable: "Participants",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_MatchmakingEvent_CompanyId",
                table: "MatchmakingEvent",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_MeetingSlots_AssignedStudentId",
                table: "MeetingSlots",
                column: "AssignedStudentId");

            migrationBuilder.CreateIndex(
                name: "IX_MeetingSlots_CompanyId",
                table: "MeetingSlots",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_MeetingSlots_MatchmakingEventId",
                table: "MeetingSlots",
                column: "MatchmakingEventId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MeetingSlots");

            migrationBuilder.DropTable(
                name: "MatchmakingEvent");
        }
    }
}
