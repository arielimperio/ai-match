using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NackaMatchmaking.API.Migrations
{
    /// <inheritdoc />
    public partial class UpdateMeetingSlotCompanyToParticipant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MeetingSlots_Companies_CompanyId",
                table: "MeetingSlots");

            migrationBuilder.DropForeignKey(
                name: "FK_MeetingSlots_Participants_AssignedStudentId",
                table: "MeetingSlots");

            migrationBuilder.RenameColumn(
                name: "CompanyId",
                table: "MeetingSlots",
                newName: "CompanyParticipantId");

            migrationBuilder.RenameIndex(
                name: "IX_MeetingSlots_CompanyId",
                table: "MeetingSlots",
                newName: "IX_MeetingSlots_CompanyParticipantId");

            migrationBuilder.AddForeignKey(
                name: "FK_MeetingSlots_Participants_AssignedStudentId",
                table: "MeetingSlots",
                column: "AssignedStudentId",
                principalTable: "Participants",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_MeetingSlots_Participants_CompanyParticipantId",
                table: "MeetingSlots",
                column: "CompanyParticipantId",
                principalTable: "Participants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MeetingSlots_Participants_AssignedStudentId",
                table: "MeetingSlots");

            migrationBuilder.DropForeignKey(
                name: "FK_MeetingSlots_Participants_CompanyParticipantId",
                table: "MeetingSlots");

            migrationBuilder.RenameColumn(
                name: "CompanyParticipantId",
                table: "MeetingSlots",
                newName: "CompanyId");

            migrationBuilder.RenameIndex(
                name: "IX_MeetingSlots_CompanyParticipantId",
                table: "MeetingSlots",
                newName: "IX_MeetingSlots_CompanyId");

            migrationBuilder.AddForeignKey(
                name: "FK_MeetingSlots_Companies_CompanyId",
                table: "MeetingSlots",
                column: "CompanyId",
                principalTable: "Companies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MeetingSlots_Participants_AssignedStudentId",
                table: "MeetingSlots",
                column: "AssignedStudentId",
                principalTable: "Participants",
                principalColumn: "Id");
        }
    }
}
