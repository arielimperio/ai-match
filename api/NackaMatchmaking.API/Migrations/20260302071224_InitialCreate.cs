using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace NackaMatchmaking.API.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AdminUsers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Username = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdminUsers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ChatMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MatchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SenderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatMessages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Participants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    HasAcceptedTerms = table.Column<bool>(type: "bit", nullable: false),
                    Firstname = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Lastname = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Organization = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Bio = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Photo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Superpower = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SuperpowerOther = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Challenge = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ChallengeOther = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Topics = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TopicsOther = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyDescription = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Participants", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Questions",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Placeholder = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MaxLength = table.Column<int>(type: "int", nullable: true),
                    IsHidden = table.Column<bool>(type: "bit", nullable: false),
                    Order = table.Column<int>(type: "int", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Questions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Registrations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Firstname = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Lastname = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Organization = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    HasAcceptedTerms = table.Column<bool>(type: "bit", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Registrations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Settings",
                columns: table => new
                {
                    Key = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Settings", x => x.Key);
                });

            migrationBuilder.CreateTable(
                name: "UserAnswers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ParticipantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    QuestionId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AnswerValue = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OtherValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserAnswers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Matches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    User1Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    User2Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    User1Interested = table.Column<bool>(type: "bit", nullable: false),
                    User2Interested = table.Column<bool>(type: "bit", nullable: false),
                    User1Feedback = table.Column<int>(type: "int", nullable: true),
                    User1FeedbackReason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    User2Feedback = table.Column<int>(type: "int", nullable: true),
                    User2FeedbackReason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Score = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    MatchedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Matches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Matches_Participants_User1Id",
                        column: x => x.User1Id,
                        principalTable: "Participants",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Matches_Participants_User2Id",
                        column: x => x.User2Id,
                        principalTable: "Participants",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "QuestionOptions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    QuestionId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Icon = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Order = table.Column<int>(type: "int", nullable: false),
                    IsHidden = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuestionOptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QuestionOptions_Questions_QuestionId",
                        column: x => x.QuestionId,
                        principalTable: "Questions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "AdminUsers",
                columns: new[] { "Id", "PasswordHash", "Username" },
                values: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), "password123", "admin" });

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
                    { "SurveyOpen", "true" },
                    { "WelcomeButton", "Starta matchningen" },
                    { "WelcomeDescription", "Välkommen till årets matchmaking-event!" },
                    { "WelcomeLogo", "assets/logo.png" },
                    { "WelcomeTagline", "Matchmaking 2026" },
                    { "WelcomeTitle", "Välkommen!" }
                });

            migrationBuilder.InsertData(
                table: "QuestionOptions",
                columns: new[] { "Id", "Description", "Icon", "IsHidden", "Order", "QuestionId", "Title", "Value" },
                values: new object[,]
                {
                    { 1, "Hjälper andra att växa och sälja mer.", "🚀", false, 1, "q1", "Försäljning & BD", "sales" },
                    { 2, "Expert på digitala lösningar och AI.", "💻", false, 2, "q1", "Teknik & IT", "tech" },
                    { 3, "Bygger organisationer och team.", "🎯", false, 3, "q1", "Strategi & Ledarskap", "strat" },
                    { 4, "Drivkraft för grön omställning.", "🌿", false, 4, "q1", "Hållbarhet", "sust" },
                    { 5, "Skriv din egen superkraft.", "✨", false, 5, "q1", "Annat...", "other" },
                    { 6, "Söker leads och nya marknader.", "📈", false, 1, "q2", "Hitta nya kunder", "leads" },
                    { 7, "Söker strategiska samarbeten.", "🤝", false, 2, "q2", "Nätverk & Partners", "partners" },
                    { 8, "Behöver stärka upp teamet.", "🌟", false, 3, "q2", "Rekrytera talang", "talent" },
                    { 9, "Söker investering eller stöd.", "💰", false, 4, "q2", "Kapital & Finansiering", "funding" },
                    { 10, "Berätta om din utmaning.", "🏢", false, 5, "q2", "Annat...", "other" },
                    { 11, "Möjligheter och hot.", "🤖", false, 1, "q3", "AI-revolutionen", "ai" },
                    { 12, "Lokal tillväxt och miljö.", "📍", false, 2, "q3", "Nackas framtid", "local" },
                    { 13, "Hybridarbete och kultur.", "🛸", false, 3, "q3", "Framtidens jobb", "future" },
                    { 14, "Vad brinner du för?", "☕", false, 4, "q3", "Annat...", "other" },
                    { 15, "Hittar rätt bolag och ser till att de ökar i värde", "💰", false, 6, "q1", "Kapital & Investering", "invest" },
                    { 16, "Hjälper människor att må bättre och prestera mer.", "🧘", false, 7, "q1", "Hälsa & Friskvård", "health" },
                    { 17, "Skapar arbetsplatser där människor trivs och presterar bättre.", "🏢", false, 8, "q1", "Lokaler & Arbetsplatser", "facility" },
                    { 18, "Att hitta rätt mark och lokaler för att din verksamhet ska kunna växa.", "📍", false, 6, "q2", "Lokal & Mark", "facility" },
                    { 19, "Att nå fram i bruset och förvandla kontakter till betalande kunder.", "📣", false, 7, "q2", "Sälj & Marknadsföring", "sales" },
                    { 20, "Maximera resursutnyttjandet.", "♻️", false, 5, "q3", "Cirkulär ekonomi", "circular" },
                    { 21, "Hitta nästa växel snabbt.", "🌱", false, 6, "q3", "Tillväxt", "growth" },
                    { 22, "Skala upp hållbart imperium.", "🏛️", false, 7, "q3", "”Romarriket”", "rome" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Matches_Status",
                table: "Matches",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Matches_User1Feedback",
                table: "Matches",
                column: "User1Feedback");

            migrationBuilder.CreateIndex(
                name: "IX_Matches_User1Id",
                table: "Matches",
                column: "User1Id");

            migrationBuilder.CreateIndex(
                name: "IX_Matches_User2Feedback",
                table: "Matches",
                column: "User2Feedback");

            migrationBuilder.CreateIndex(
                name: "IX_Matches_User2Id",
                table: "Matches",
                column: "User2Id");

            migrationBuilder.CreateIndex(
                name: "IX_Participants_Email",
                table: "Participants",
                column: "Email");

            migrationBuilder.CreateIndex(
                name: "IX_QuestionOptions_QuestionId",
                table: "QuestionOptions",
                column: "QuestionId");

            migrationBuilder.CreateIndex(
                name: "IX_Registrations_Email",
                table: "Registrations",
                column: "Email");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdminUsers");

            migrationBuilder.DropTable(
                name: "ChatMessages");

            migrationBuilder.DropTable(
                name: "Matches");

            migrationBuilder.DropTable(
                name: "QuestionOptions");

            migrationBuilder.DropTable(
                name: "Registrations");

            migrationBuilder.DropTable(
                name: "Settings");

            migrationBuilder.DropTable(
                name: "UserAnswers");

            migrationBuilder.DropTable(
                name: "Participants");

            migrationBuilder.DropTable(
                name: "Questions");
        }
    }
}
