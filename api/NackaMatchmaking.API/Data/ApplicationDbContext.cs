using Microsoft.EntityFrameworkCore;
using NackaMatchmaking.API.Models;

namespace NackaMatchmaking.API.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Registration> Registrations { get; set; }
        public DbSet<Participant> Participants { get; set; }
        public DbSet<AdminUser> AdminUsers { get; set; }
        public DbSet<Company> Companies { get; set; }
        public DbSet<Question> Questions { get; set; }
        public DbSet<QuestionOption> QuestionOptions { get; set; }
        public DbSet<UserAnswer> UserAnswers { get; set; }
        public DbSet<UserMatch> Matches { get; set; }
        public DbSet<ChatMessage> ChatMessages { get; set; }
        public DbSet<SiteSetting> Settings { get; set; }
        public DbSet<UserCompany> UserCompanies { get; set; }
        public DbSet<MeetingSlot> MeetingSlots { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Hierarchy configuration
            modelBuilder.Entity<Company>()
                .HasOne(c => c.Parent)
                .WithMany(c => c.SubEvents)
                .HasForeignKey(c => c.ParentId)
                .OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<SiteSetting>().HasKey(s => new { s.CompanyId, s.Key });
            modelBuilder.Entity<Question>().HasKey(q => new { q.CompanyId, q.Id });

            // Relationships for composite keys
            modelBuilder.Entity<QuestionOption>()
                .HasOne(o => o.Question)
                .WithMany(q => q.Options)
                .HasForeignKey(o => new { o.CompanyId, o.QuestionId })
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<UserAnswer>()
                .HasOne(ua => ua.Question)
                .WithMany()
                .HasForeignKey(ua => new { ua.CompanyId, ua.QuestionId })
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Registration>()
                .HasOne(r => r.Company)
                .WithMany()
                .HasForeignKey(r => r.CompanyId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Participant>()
                .HasOne(p => p.Company)
                .WithMany()
                .HasForeignKey(p => p.CompanyId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<MeetingSlot>()
                .HasOne(s => s.CompanyParticipant)
                .WithMany()
                .HasForeignKey(s => s.CompanyParticipantId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<MeetingSlot>()
                .HasOne(s => s.AssignedStudent)
                .WithMany()
                .HasForeignKey(s => s.AssignedStudentId)
                .OnDelete(DeleteBehavior.SetNull);

            // Configure Company relationships for AdminUser
            modelBuilder.Entity<AdminUser>()
                .HasOne(a => a.Company)
                .WithMany(c => c.AdminUsers)
                .HasForeignKey(a => a.CompanyId)
                .OnDelete(DeleteBehavior.SetNull);

            // Configure Many-to-Many via UserCompany
            modelBuilder.Entity<UserCompany>()
                .HasKey(uc => new { uc.UserId, uc.CompanyId });

            modelBuilder.Entity<UserCompany>()
                .HasOne(uc => uc.User)
                .WithMany(u => u.UserCompanies)
                .HasForeignKey(uc => uc.UserId);

            modelBuilder.Entity<UserCompany>()
                .HasOne(uc => uc.Company)
                .WithMany(c => c.UserCompanies)
                .HasForeignKey(uc => uc.CompanyId);

            // Seed Local Test Company
            var seedCompanyId = Guid.Parse("00000000-0000-0000-0000-000000000002");
            var systemId = Guid.Parse("11111111-1111-1111-1111-111111111111");

            modelBuilder.Entity<Company>().HasData(
                new Company { Id = systemId, Name = "System Settings", CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
                new Company { Id = seedCompanyId, Name = "Nacka Företagarträff (Default)", CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc) }
            );

            // Seed Admin Users
            modelBuilder.Entity<AdminUser>().HasData(
                new AdminUser 
                { 
                    Id = Guid.Parse("00000000-0000-0000-0000-000000000010"), 
                    Username = "superadmin", 
                    PasswordHash = "TinTin%26",
                    FirstName = "System",
                    LastName = "SuperAdmin",
                    Email = "superadmin@itmaskinen.se",
                    CompanyId = null,
                    Role = "SuperAdmin"
                },
                new AdminUser 
                { 
                    Id = Guid.Parse("00000000-0000-0000-0000-000000000001"), 
                    Username = "admin", 
                    PasswordHash = "password123",
                    FirstName = "System",
                    LastName = "Admin",
                    Email = "admin@example.com",
                    CompanyId = seedCompanyId,
                    Role = "Admin"
                }
            );

            // Seed UserCompanies (Join Table)
            modelBuilder.Entity<UserCompany>().HasData(
                new UserCompany { UserId = Guid.Parse("00000000-0000-0000-0000-000000000001"), CompanyId = seedCompanyId, Role = "Admin", JoinedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc) }
            );

            // Restore Seed Data assigned to Default Company
            modelBuilder.Entity<SiteSetting>().HasData(
                new SiteSetting { CompanyId = seedCompanyId, Key = "ProfileDescription", Value = "Kontrollera att dina uppgifter stämmer så att andra kan hitta dig på mässan." },
                new SiteSetting { CompanyId = seedCompanyId, Key = "ProfileTitle", Value = "Mina uppgifter" },
                new SiteSetting { CompanyId = seedCompanyId, Key = "SuccessMessage", Value = "Vi skickar ett mejl med dina personliga matchningar senast 48 timmar innan företagarträffen öppnar." },
                new SiteSetting { CompanyId = seedCompanyId, Key = "SurveyOpen", Value = "true" },
                new SiteSetting { CompanyId = seedCompanyId, Key = "WelcomeButton", Value = "Starta matchningen" },
                new SiteSetting { CompanyId = seedCompanyId, Key = "WelcomeDescription", Value = "Välkommen till årets matchmaking-event!" },
                new SiteSetting { CompanyId = seedCompanyId, Key = "WelcomeLogo", Value = "" },
                new SiteSetting { CompanyId = seedCompanyId, Key = "WelcomeTagline", Value = "Matchmaking 2026" },
                new SiteSetting { CompanyId = seedCompanyId, Key = "WelcomeTitle", Value = "Välkommen!" },
                new SiteSetting { CompanyId = seedCompanyId, Key = "AiProvider", Value = "OpenAI" },
                new SiteSetting { CompanyId = seedCompanyId, Key = "AiModel", Value = "gpt-4o" },
                new SiteSetting { CompanyId = seedCompanyId, Key = "AiApiKey", Value = "" },
                
                // Seed Global System Settings
                new SiteSetting { CompanyId = systemId, Key = "AiProvider", Value = "OpenAI" },
                new SiteSetting { CompanyId = systemId, Key = "AiModel", Value = "gpt-4o" },
                new SiteSetting { CompanyId = systemId, Key = "AiApiKey", Value = "" },
                new SiteSetting { CompanyId = systemId, Key = "SendGridApiKey", Value = "" },
                new SiteSetting { CompanyId = systemId, Key = "SendGridFromEmail", Value = "ariel@itmaskinen.se" },
                new SiteSetting { CompanyId = systemId, Key = "EmailFromName", Value = "Nacka Företagarträff" }
            );

            modelBuilder.Entity<Question>().HasData(
                new Question { CompanyId = seedCompanyId, Id = "q1", Description = "Inom vilket område kan du bidra med mest värde till andra?", IsHidden = false, Order = 1, Title = "Min Superkraft", Type = "MultipleChoice" },
                new Question { CompanyId = seedCompanyId, Id = "q2", Description = "Vad är den största utmaningen du vill lösa just nu?", IsHidden = false, Order = 2, Title = "Min Utmaning", Type = "Choice" },
                new Question { CompanyId = seedCompanyId, Id = "q3", Description = "Vad snackar du helst om vid kaffemaskinen?", IsHidden = false, Order = 3, Title = "Samtalsämnen", Type = "MultipleChoice" },
                new Question { CompanyId = seedCompanyId, Id = "q4", Description = "Beskriv ditt mål för i år med en mening. AI:n använder detta för att hitta dolda kopplingar.", IsHidden = false, MaxLength = 200, Order = 4, Placeholder = "T.ex. Jag vill expandera min konsultverksamhet till norra Europa...", Title = "Kort om dig", Type = "Text" },
                new Question { CompanyId = seedCompanyId, Id = "q5", Description = "Beskriv företaget där du jobbar, max en mening 50 tkn.", IsHidden = false, MaxLength = 50, Order = 5, Title = "Kort om företaget", Type = "Text" }
            );

            modelBuilder.Entity<QuestionOption>().HasData(
                new QuestionOption { CompanyId = seedCompanyId, Id = 1, Description = "Hjälper andra att växa och sälja mer.", Icon = "🚀", IsHidden = false, Order = 1, QuestionId = "q1", Title = "Försäljning & BD", Value = "sales" },
                new QuestionOption { CompanyId = seedCompanyId, Id = 2, Description = "Expert på digitala lösningar och AI.", Icon = "💻", IsHidden = false, Order = 2, QuestionId = "q1", Title = "Teknik & IT", Value = "tech" },
                new QuestionOption { CompanyId = seedCompanyId, Id = 3, Description = "Bygger organisationer och team.", Icon = "🎯", IsHidden = false, Order = 3, QuestionId = "q1", Title = "Strategi & Ledarskap", Value = "strat" },
                new QuestionOption { CompanyId = seedCompanyId, Id = 4, Description = "Drivkraft för grön omställning.", Icon = "🌿", IsHidden = false, Order = 4, QuestionId = "q1", Title = "Hållbarhet", Value = "sust" },
                new QuestionOption { CompanyId = seedCompanyId, Id = 5, Description = "Skriv din egen superkraft.", Icon = "✨", IsHidden = false, Order = 5, QuestionId = "q1", Title = "Annat...", Value = "other" },
                new QuestionOption { CompanyId = seedCompanyId, Id = 6, Description = "Söker leads och nya marknader.", Icon = "📈", IsHidden = false, Order = 1, QuestionId = "q2", Title = "Hitta nya kunder", Value = "leads" },
                new QuestionOption { CompanyId = seedCompanyId, Id = 7, Description = "Söker strategiska samarbeten.", Icon = "🤝", IsHidden = false, Order = 2, QuestionId = "q2", Title = "Nätverk & Partners", Value = "partners" },
                new QuestionOption { CompanyId = seedCompanyId, Id = 8, Description = "Behöver stärka upp teamet.", Icon = "🌟", IsHidden = false, Order = 3, QuestionId = "q2", Title = "Rekrytera talang", Value = "talent" },
                new QuestionOption { CompanyId = seedCompanyId, Id = 9, Description = "Söker investering eller stöd.", Icon = "💰", IsHidden = false, Order = 4, QuestionId = "q2", Title = "Kapital & Finansiering", Value = "funding" },
                new QuestionOption { CompanyId = seedCompanyId, Id = 10, Description = "Berätta om din utmaning.", Icon = "🏢", IsHidden = false, Order = 5, QuestionId = "q2", Title = "Annat...", Value = "other" },
                new QuestionOption { CompanyId = seedCompanyId, Id = 11, Description = "Möjligheter och hot.", Icon = "🤖", IsHidden = false, Order = 1, QuestionId = "q3", Title = "AI-revolutionen", Value = "ai" },
                new QuestionOption { CompanyId = seedCompanyId, Id = 12, Description = "Lokal tillväxt och miljö.", Icon = "📍", IsHidden = false, Order = 2, QuestionId = "q3", Title = "Nackas framtid", Value = "local" },
                new QuestionOption { CompanyId = seedCompanyId, Id = 13, Description = "Hybridarbete och kultur.", Icon = "🛸", IsHidden = false, Order = 3, QuestionId = "q3", Title = "Framtidens jobb", Value = "future" },
                new QuestionOption { CompanyId = seedCompanyId, Id = 14, Description = "Vad brinner du för?", Icon = "☕", IsHidden = false, Order = 4, QuestionId = "q3", Title = "Annat...", Value = "other" },
                new QuestionOption { CompanyId = seedCompanyId, Id = 15, Description = "Hittar rätt bolag och ser till att de ökar i värde", Icon = "💰", IsHidden = false, Order = 6, QuestionId = "q1", Title = "Kapital & Investering", Value = "invest" },
                new QuestionOption { CompanyId = seedCompanyId, Id = 16, Description = "Hjälper människor att må bättre och prestera mer.", Icon = "🧘", IsHidden = false, Order = 7, QuestionId = "q1", Title = "Hälsa & Friskvård", Value = "health" },
                new QuestionOption { CompanyId = seedCompanyId, Id = 17, Description = "Skapar arbetsplatser där människor trivs och presterar bättre.", Icon = "🏢", IsHidden = false, Order = 8, QuestionId = "q1", Title = "Lokaler & Arbetsplatser", Value = "facility" },
                new QuestionOption { CompanyId = seedCompanyId, Id = 18, Description = "Att hitta rätt mark och lokaler för att din verksamhet ska kunna växa.", Icon = "📍", IsHidden = false, Order = 6, QuestionId = "q2", Title = "Lokal & Mark", Value = "facility" },
                new QuestionOption { CompanyId = seedCompanyId, Id = 19, Description = "Att nå fram i bruset och förvandla kontakter till betalande kunder.", Icon = "📣", IsHidden = false, Order = 7, QuestionId = "q2", Title = "Sälj & Marknadsföring", Value = "sales" },
                new QuestionOption { CompanyId = seedCompanyId, Id = 20, Description = "Maximera resursutnyttjandet.", Icon = "♻️", IsHidden = false, Order = 5, QuestionId = "q3", Title = "Cirkulär ekonomi", Value = "circular" },
                new QuestionOption { CompanyId = seedCompanyId, Id = 21, Description = "Hitta nästa växel snabbt.", Icon = "🌱", IsHidden = false, Order = 6, QuestionId = "q3", Title = "Tillväxt", Value = "growth" },
                new QuestionOption { CompanyId = seedCompanyId, Id = 22, Description = "Skala upp hållbart imperium.", Icon = "🏛️", IsHidden = false, Order = 7, QuestionId = "q3", Title = "”Romarriket”", Value = "rome" }
            );

            // Configure UserMatch to avoid multiple cascade paths
            modelBuilder.Entity<UserMatch>()
                .HasOne(m => m.User1)
                .WithMany()
                .HasForeignKey(m => m.User1Id)
                .OnDelete(DeleteBehavior.ClientCascade);

            modelBuilder.Entity<MeetingSlot>()
                .HasOne(ms => ms.MatchmakingEvent)
                .WithMany()
                .HasForeignKey(ms => ms.MatchmakingEventId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<UserMatch>()
                .HasOne(m => m.User2)
                .WithMany()
                .HasForeignKey(m => m.User2Id)
                .OnDelete(DeleteBehavior.ClientCascade);

            // Performance Indexes
            modelBuilder.Entity<UserMatch>().HasIndex(m => m.User1Id);
            modelBuilder.Entity<UserMatch>().HasIndex(m => m.User2Id);
            modelBuilder.Entity<UserMatch>().HasIndex(m => m.Status);
            modelBuilder.Entity<UserMatch>().HasIndex(m => m.User1Feedback);
            modelBuilder.Entity<UserMatch>().HasIndex(m => m.User2Feedback);

            modelBuilder.Entity<Registration>().HasIndex(r => r.Email);
            modelBuilder.Entity<Participant>().HasIndex(p => p.Email);
        }
    }
}
