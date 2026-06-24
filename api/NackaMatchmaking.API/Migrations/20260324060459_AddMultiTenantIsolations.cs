using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace NackaMatchmaking.API.Migrations
{
    /// <inheritdoc />
    public partial class AddMultiTenantIsolations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_QuestionOptions_Questions_QuestionId",
                table: "QuestionOptions");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Settings",
                table: "Settings");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Questions",
                table: "Questions");

            migrationBuilder.DropIndex(
                name: "IX_QuestionOptions_QuestionId",
                table: "QuestionOptions");

            migrationBuilder.DeleteData(
                table: "Questions",
                keyColumn: "Id",
                keyValue: "q1");

            migrationBuilder.DeleteData(
                table: "Questions",
                keyColumn: "Id",
                keyValue: "q2");

            migrationBuilder.DeleteData(
                table: "Questions",
                keyColumn: "Id",
                keyValue: "q3");

            migrationBuilder.DeleteData(
                table: "Questions",
                keyColumn: "Id",
                keyValue: "q4");

            migrationBuilder.DeleteData(
                table: "Questions",
                keyColumn: "Id",
                keyValue: "q5");

            migrationBuilder.DeleteData(
                table: "Settings",
                keyColumn: "Key",
                keyValue: "ProfileDescription");

            migrationBuilder.DeleteData(
                table: "Settings",
                keyColumn: "Key",
                keyValue: "ProfileTitle");

            migrationBuilder.DeleteData(
                table: "Settings",
                keyColumn: "Key",
                keyValue: "SuccessMessage");

            migrationBuilder.DeleteData(
                table: "Settings",
                keyColumn: "Key",
                keyValue: "SurveyOpen");

            migrationBuilder.DeleteData(
                table: "Settings",
                keyColumn: "Key",
                keyValue: "WelcomeButton");

            migrationBuilder.DeleteData(
                table: "Settings",
                keyColumn: "Key",
                keyValue: "WelcomeDescription");

            migrationBuilder.DeleteData(
                table: "Settings",
                keyColumn: "Key",
                keyValue: "WelcomeLogo");

            migrationBuilder.DeleteData(
                table: "Settings",
                keyColumn: "Key",
                keyValue: "WelcomeTagline");

            migrationBuilder.DeleteData(
                table: "Settings",
                keyColumn: "Key",
                keyValue: "WelcomeTitle");

            migrationBuilder.DeleteData(
                table: "Settings",
                keyColumn: "Key",
                keyValue: "AiApiKey");

            migrationBuilder.DeleteData(
                table: "Settings",
                keyColumn: "Key",
                keyValue: "AiModel");

            migrationBuilder.DeleteData(
                table: "Settings",
                keyColumn: "Key",
                keyValue: "AiProvider");

            migrationBuilder.AlterColumn<string>(
                name: "QuestionId",
                table: "UserAnswers",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<Guid>(
                name: "CompanyId",
                table: "UserAnswers",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000002"));

            migrationBuilder.AddColumn<Guid>(
                name: "CompanyId",
                table: "Settings",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000002"));

            migrationBuilder.AddColumn<Guid>(
                name: "CompanyId",
                table: "Registrations",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000002"));

            migrationBuilder.AddColumn<Guid>(
                name: "CompanyId",
                table: "Questions",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000002"));

            migrationBuilder.AddColumn<Guid>(
                name: "CompanyId",
                table: "QuestionOptions",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000002"));

            migrationBuilder.AddColumn<Guid>(
                name: "CompanyId",
                table: "Participants",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000002"));

            migrationBuilder.AddPrimaryKey(
                name: "PK_Settings",
                table: "Settings",
                columns: new[] { "CompanyId", "Key" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_Questions",
                table: "Questions",
                columns: new[] { "CompanyId", "Id" });

            migrationBuilder.InsertData(
                table: "Companies",
                columns: new[] { "Id", "CreatedAt", "Name" },
                values: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Nacka Företagarträff (Default)" });

            migrationBuilder.UpdateData(
                table: "AdminUsers",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "CompanyId",
                value: new Guid("00000000-0000-0000-0000-000000000002"));

            migrationBuilder.UpdateData(
                table: "QuestionOptions",
                keyColumn: "Id",
                keyValue: 1,
                column: "CompanyId",
                value: new Guid("00000000-0000-0000-0000-000000000002"));

            migrationBuilder.UpdateData(
                table: "QuestionOptions",
                keyColumn: "Id",
                keyValue: 2,
                column: "CompanyId",
                value: new Guid("00000000-0000-0000-0000-000000000002"));

            migrationBuilder.UpdateData(
                table: "QuestionOptions",
                keyColumn: "Id",
                keyValue: 3,
                column: "CompanyId",
                value: new Guid("00000000-0000-0000-0000-000000000002"));

            migrationBuilder.UpdateData(
                table: "QuestionOptions",
                keyColumn: "Id",
                keyValue: 4,
                column: "CompanyId",
                value: new Guid("00000000-0000-0000-0000-000000000002"));

            migrationBuilder.UpdateData(
                table: "QuestionOptions",
                keyColumn: "Id",
                keyValue: 5,
                column: "CompanyId",
                value: new Guid("00000000-0000-0000-0000-000000000002"));

            migrationBuilder.UpdateData(
                table: "QuestionOptions",
                keyColumn: "Id",
                keyValue: 6,
                column: "CompanyId",
                value: new Guid("00000000-0000-0000-0000-000000000002"));

            migrationBuilder.UpdateData(
                table: "QuestionOptions",
                keyColumn: "Id",
                keyValue: 7,
                column: "CompanyId",
                value: new Guid("00000000-0000-0000-0000-000000000002"));

            migrationBuilder.UpdateData(
                table: "QuestionOptions",
                keyColumn: "Id",
                keyValue: 8,
                column: "CompanyId",
                value: new Guid("00000000-0000-0000-0000-000000000002"));

            migrationBuilder.UpdateData(
                table: "QuestionOptions",
                keyColumn: "Id",
                keyValue: 9,
                column: "CompanyId",
                value: new Guid("00000000-0000-0000-0000-000000000002"));

            migrationBuilder.UpdateData(
                table: "QuestionOptions",
                keyColumn: "Id",
                keyValue: 10,
                column: "CompanyId",
                value: new Guid("00000000-0000-0000-0000-000000000002"));

            migrationBuilder.UpdateData(
                table: "QuestionOptions",
                keyColumn: "Id",
                keyValue: 11,
                column: "CompanyId",
                value: new Guid("00000000-0000-0000-0000-000000000002"));

            migrationBuilder.UpdateData(
                table: "QuestionOptions",
                keyColumn: "Id",
                keyValue: 12,
                column: "CompanyId",
                value: new Guid("00000000-0000-0000-0000-000000000002"));

            migrationBuilder.UpdateData(
                table: "QuestionOptions",
                keyColumn: "Id",
                keyValue: 13,
                column: "CompanyId",
                value: new Guid("00000000-0000-0000-0000-000000000002"));

            migrationBuilder.UpdateData(
                table: "QuestionOptions",
                keyColumn: "Id",
                keyValue: 14,
                column: "CompanyId",
                value: new Guid("00000000-0000-0000-0000-000000000002"));

            migrationBuilder.UpdateData(
                table: "QuestionOptions",
                keyColumn: "Id",
                keyValue: 15,
                column: "CompanyId",
                value: new Guid("00000000-0000-0000-0000-000000000002"));

            migrationBuilder.UpdateData(
                table: "QuestionOptions",
                keyColumn: "Id",
                keyValue: 16,
                column: "CompanyId",
                value: new Guid("00000000-0000-0000-0000-000000000002"));

            migrationBuilder.UpdateData(
                table: "QuestionOptions",
                keyColumn: "Id",
                keyValue: 17,
                column: "CompanyId",
                value: new Guid("00000000-0000-0000-0000-000000000002"));

            migrationBuilder.UpdateData(
                table: "QuestionOptions",
                keyColumn: "Id",
                keyValue: 18,
                column: "CompanyId",
                value: new Guid("00000000-0000-0000-0000-000000000002"));

            migrationBuilder.UpdateData(
                table: "QuestionOptions",
                keyColumn: "Id",
                keyValue: 19,
                column: "CompanyId",
                value: new Guid("00000000-0000-0000-0000-000000000002"));

            migrationBuilder.UpdateData(
                table: "QuestionOptions",
                keyColumn: "Id",
                keyValue: 20,
                column: "CompanyId",
                value: new Guid("00000000-0000-0000-0000-000000000002"));

            migrationBuilder.UpdateData(
                table: "QuestionOptions",
                keyColumn: "Id",
                keyValue: 21,
                column: "CompanyId",
                value: new Guid("00000000-0000-0000-0000-000000000002"));

            migrationBuilder.UpdateData(
                table: "QuestionOptions",
                keyColumn: "Id",
                keyValue: 22,
                column: "CompanyId",
                value: new Guid("00000000-0000-0000-0000-000000000002"));

            migrationBuilder.InsertData(
                table: "Questions",
                columns: new[] { "CompanyId", "Id", "Description", "IsHidden", "MaxLength", "Order", "Placeholder", "Title", "Type" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-0000-000000000002"), "q1", "Inom vilket område kan du bidra med mest värde till andra?", false, null, 1, null, "Min Superkraft", "MultipleChoice" },
                    { new Guid("00000000-0000-0000-0000-000000000002"), "q2", "Vad är den största utmaningen du vill lösa just nu?", false, null, 2, null, "Min Utmaning", "Choice" },
                    { new Guid("00000000-0000-0000-0000-000000000002"), "q3", "Vad snackar du helst om vid kaffemaskinen?", false, null, 3, null, "Samtalsämnen", "MultipleChoice" },
                    { new Guid("00000000-0000-0000-0000-000000000002"), "q4", "Beskriv ditt mål för i år med en mening. AI:n använder detta för att hitta dolda kopplingar.", false, 200, 4, "T.ex. Jag vill expandera min konsultverksamhet till norra Europa...", "Kort om dig", "Text" },
                    { new Guid("00000000-0000-0000-0000-000000000002"), "q5", "Beskriv företaget där du jobbar, max en mening 50 tkn.", false, 50, 5, null, "Kort om företaget", "Text" }
                });

            migrationBuilder.InsertData(
                table: "Settings",
                columns: new[] { "CompanyId", "Key", "Value" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-0000-000000000002"), "AiApiKey", "" },
                    { new Guid("00000000-0000-0000-0000-000000000002"), "AiModel", "gpt-4o" },
                    { new Guid("00000000-0000-0000-0000-000000000002"), "AiProvider", "OpenAI" },
                    { new Guid("00000000-0000-0000-0000-000000000002"), "ProfileDescription", "Kontrollera att dina uppgifter stämmer så att andra kan hitta dig på mässan." },
                    { new Guid("00000000-0000-0000-0000-000000000002"), "ProfileTitle", "Mina uppgifter" },
                    { new Guid("00000000-0000-0000-0000-000000000002"), "SuccessMessage", "Vi skickar ett mejl med dina personliga matchningar senast 48 timmar innan företagarträffen öppnar." },
                    { new Guid("00000000-0000-0000-0000-000000000002"), "SurveyOpen", "true" },
                    { new Guid("00000000-0000-0000-0000-000000000002"), "WelcomeButton", "Starta matchningen" },
                    { new Guid("00000000-0000-0000-0000-000000000002"), "WelcomeDescription", "Välkommen till årets matchmaking-event!" },
                    { new Guid("00000000-0000-0000-0000-000000000002"), "WelcomeLogo", "assets/logo.png" },
                    { new Guid("00000000-0000-0000-0000-000000000002"), "WelcomeTagline", "Matchmaking 2026" },
                    { new Guid("00000000-0000-0000-0000-000000000002"), "WelcomeTitle", "Välkommen!" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserAnswers_CompanyId_QuestionId",
                table: "UserAnswers",
                columns: new[] { "CompanyId", "QuestionId" });

            migrationBuilder.CreateIndex(
                name: "IX_UserAnswers_ParticipantId",
                table: "UserAnswers",
                column: "ParticipantId");

            migrationBuilder.CreateIndex(
                name: "IX_Registrations_CompanyId",
                table: "Registrations",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_QuestionOptions_CompanyId_QuestionId",
                table: "QuestionOptions",
                columns: new[] { "CompanyId", "QuestionId" });

            migrationBuilder.CreateIndex(
                name: "IX_Participants_CompanyId",
                table: "Participants",
                column: "CompanyId");

            migrationBuilder.AddForeignKey(
                name: "FK_Participants_Companies_CompanyId",
                table: "Participants",
                column: "CompanyId",
                principalTable: "Companies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_QuestionOptions_Questions_CompanyId_QuestionId",
                table: "QuestionOptions",
                columns: new[] { "CompanyId", "QuestionId" },
                principalTable: "Questions",
                principalColumns: new[] { "CompanyId", "Id" },
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Questions_Companies_CompanyId",
                table: "Questions",
                column: "CompanyId",
                principalTable: "Companies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Registrations_Companies_CompanyId",
                table: "Registrations",
                column: "CompanyId",
                principalTable: "Companies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Settings_Companies_CompanyId",
                table: "Settings",
                column: "CompanyId",
                principalTable: "Companies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.Sql("DELETE FROM [UserAnswers] WHERE [ParticipantId] NOT IN (SELECT [Id] FROM [Participants])");

            migrationBuilder.AddForeignKey(
                name: "FK_UserAnswers_Participants_ParticipantId",
                table: "UserAnswers",
                column: "ParticipantId",
                principalTable: "Participants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_UserAnswers_Questions_CompanyId_QuestionId",
                table: "UserAnswers",
                columns: new[] { "CompanyId", "QuestionId" },
                principalTable: "Questions",
                principalColumns: new[] { "CompanyId", "Id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Participants_Companies_CompanyId",
                table: "Participants");

            migrationBuilder.DropForeignKey(
                name: "FK_QuestionOptions_Questions_CompanyId_QuestionId",
                table: "QuestionOptions");

            migrationBuilder.DropForeignKey(
                name: "FK_Questions_Companies_CompanyId",
                table: "Questions");

            migrationBuilder.DropForeignKey(
                name: "FK_Registrations_Companies_CompanyId",
                table: "Registrations");

            migrationBuilder.DropForeignKey(
                name: "FK_Settings_Companies_CompanyId",
                table: "Settings");

            migrationBuilder.DropForeignKey(
                name: "FK_UserAnswers_Participants_ParticipantId",
                table: "UserAnswers");

            migrationBuilder.DropForeignKey(
                name: "FK_UserAnswers_Questions_CompanyId_QuestionId",
                table: "UserAnswers");

            migrationBuilder.DropIndex(
                name: "IX_UserAnswers_CompanyId_QuestionId",
                table: "UserAnswers");

            migrationBuilder.DropIndex(
                name: "IX_UserAnswers_ParticipantId",
                table: "UserAnswers");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Settings",
                table: "Settings");

            migrationBuilder.DropIndex(
                name: "IX_Registrations_CompanyId",
                table: "Registrations");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Questions",
                table: "Questions");

            migrationBuilder.DropIndex(
                name: "IX_QuestionOptions_CompanyId_QuestionId",
                table: "QuestionOptions");

            migrationBuilder.DropIndex(
                name: "IX_Participants_CompanyId",
                table: "Participants");

            migrationBuilder.DeleteData(
                table: "Questions",
                keyColumns: new[] { "CompanyId", "Id" },
                keyColumnTypes: new[] { "uniqueidentifier", "nvarchar(450)" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), "q1" });

            migrationBuilder.DeleteData(
                table: "Questions",
                keyColumns: new[] { "CompanyId", "Id" },
                keyColumnTypes: new[] { "uniqueidentifier", "nvarchar(450)" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), "q2" });

            migrationBuilder.DeleteData(
                table: "Questions",
                keyColumns: new[] { "CompanyId", "Id" },
                keyColumnTypes: new[] { "uniqueidentifier", "nvarchar(450)" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), "q3" });

            migrationBuilder.DeleteData(
                table: "Questions",
                keyColumns: new[] { "CompanyId", "Id" },
                keyColumnTypes: new[] { "uniqueidentifier", "nvarchar(450)" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), "q4" });

            migrationBuilder.DeleteData(
                table: "Questions",
                keyColumns: new[] { "CompanyId", "Id" },
                keyColumnTypes: new[] { "uniqueidentifier", "nvarchar(450)" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), "q5" });

            migrationBuilder.DeleteData(
                table: "Settings",
                keyColumns: new[] { "CompanyId", "Key" },
                keyColumnTypes: new[] { "uniqueidentifier", "nvarchar(450)" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), "AiApiKey" });

            migrationBuilder.DeleteData(
                table: "Settings",
                keyColumns: new[] { "CompanyId", "Key" },
                keyColumnTypes: new[] { "uniqueidentifier", "nvarchar(450)" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), "AiModel" });

            migrationBuilder.DeleteData(
                table: "Settings",
                keyColumns: new[] { "CompanyId", "Key" },
                keyColumnTypes: new[] { "uniqueidentifier", "nvarchar(450)" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), "AiProvider" });

            migrationBuilder.DeleteData(
                table: "Settings",
                keyColumns: new[] { "CompanyId", "Key" },
                keyColumnTypes: new[] { "uniqueidentifier", "nvarchar(450)" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), "ProfileDescription" });

            migrationBuilder.DeleteData(
                table: "Settings",
                keyColumns: new[] { "CompanyId", "Key" },
                keyColumnTypes: new[] { "uniqueidentifier", "nvarchar(450)" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), "ProfileTitle" });

            migrationBuilder.DeleteData(
                table: "Settings",
                keyColumns: new[] { "CompanyId", "Key" },
                keyColumnTypes: new[] { "uniqueidentifier", "nvarchar(450)" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), "SuccessMessage" });

            migrationBuilder.DeleteData(
                table: "Settings",
                keyColumns: new[] { "CompanyId", "Key" },
                keyColumnTypes: new[] { "uniqueidentifier", "nvarchar(450)" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), "SurveyOpen" });

            migrationBuilder.DeleteData(
                table: "Settings",
                keyColumns: new[] { "CompanyId", "Key" },
                keyColumnTypes: new[] { "uniqueidentifier", "nvarchar(450)" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), "WelcomeButton" });

            migrationBuilder.DeleteData(
                table: "Settings",
                keyColumns: new[] { "CompanyId", "Key" },
                keyColumnTypes: new[] { "uniqueidentifier", "nvarchar(450)" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), "WelcomeDescription" });

            migrationBuilder.DeleteData(
                table: "Settings",
                keyColumns: new[] { "CompanyId", "Key" },
                keyColumnTypes: new[] { "uniqueidentifier", "nvarchar(450)" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), "WelcomeLogo" });

            migrationBuilder.DeleteData(
                table: "Settings",
                keyColumns: new[] { "CompanyId", "Key" },
                keyColumnTypes: new[] { "uniqueidentifier", "nvarchar(450)" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), "WelcomeTagline" });

            migrationBuilder.DeleteData(
                table: "Settings",
                keyColumns: new[] { "CompanyId", "Key" },
                keyColumnTypes: new[] { "uniqueidentifier", "nvarchar(450)" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), "WelcomeTitle" });

            migrationBuilder.DeleteData(
                table: "Companies",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000002"));

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "UserAnswers");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "Registrations");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "Questions");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "QuestionOptions");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "Participants");

            migrationBuilder.AlterColumn<string>(
                name: "QuestionId",
                table: "UserAnswers",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Settings",
                table: "Settings",
                column: "Key");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Questions",
                table: "Questions",
                column: "Id");

            migrationBuilder.UpdateData(
                table: "AdminUsers",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "CompanyId",
                value: null);

            migrationBuilder.InsertData(
                table: "Questions",
                columns: new[] { "Id", "Description", "IsHidden", "MaxLength", "Order", "Placeholder", "Title", "Type" },
                values: new object[,]
                {
                    { "q1", "Inom vilket område kan du bidra med mest värde till andra?", false, null, 1, null, "Min Superkraft", "MultipleChoice" },
                    { "q2", "Vad är den största utmaningen du vill lösa just nu?", false, null, 2, null, "Min Utmaning", "Choice" },
                    { "q3", "Vad snackar du helst om vid kaffemaskinen?", false, null, 3, null, "Samtalsämnen", "MultipleChoice" },
                    { "q4", "Beskriv ditt mål för i år med en mening. AI:n använder detta för att hitta dolda kopplingar.", false, 200, 4, "T.ex. Jag vill expandera min konsultverksamhet till norra Europa...", "Kort om dig", "Text" },
                    { "q5", "Beskriv företaget där du jobbar, max en mening 50 tkn.", false, 50, 5, null, "Kort om företaget", "Text" }
                });

            migrationBuilder.InsertData(
                table: "Settings",
                columns: new[] { "Key", "Value" },
                values: new object[,]
                {
                    { "ProfileDescription", "Kontrollera att dina uppgifter stämmer så att andra kan hitta dig på mässan." },
                    { "ProfileTitle", "Mina uppgifter" },
                    { "SuccessMessage", "Vi skickar ett mejl med dina personliga matchningar senast 48 timmar innan företagarträffen öppnar." },
                    { "SurveyOpen", "true" },
                    { "WelcomeButton", "Starta matchningen" },
                    { "WelcomeDescription", "Välkommen till årets matchmaking-event!" },
                    { "WelcomeLogo", "assets/logo.png" },
                    { "WelcomeTagline", "Matchmaking 2026" },
                    { "WelcomeTitle", "Välkommen!" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_QuestionOptions_QuestionId",
                table: "QuestionOptions",
                column: "QuestionId");

            migrationBuilder.AddForeignKey(
                name: "FK_QuestionOptions_Questions_QuestionId",
                table: "QuestionOptions",
                column: "QuestionId",
                principalTable: "Questions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
