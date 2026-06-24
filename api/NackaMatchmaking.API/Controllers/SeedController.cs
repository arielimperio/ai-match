using Microsoft.AspNetCore.Mvc;
using NackaMatchmaking.API.Data;
using NackaMatchmaking.API.Models;
using Microsoft.EntityFrameworkCore;

namespace NackaMatchmaking.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Microsoft.AspNetCore.Authorization.Authorize]
    public class SeedController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public SeedController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpPost]
        public async Task<IActionResult> Seed()
        {
            var companyIdString = User.FindFirst("companyId")?.Value;
            if (string.IsNullOrEmpty(companyIdString) || !Guid.TryParse(companyIdString, out Guid companyId)) return Unauthorized();

            // 1. Clear existing data for THIS company
            var companyMatches = await _context.Matches.Where(m => m.User1.CompanyId == companyId).ToListAsync();
            var matchIds = companyMatches.Select(m => m.Id).ToList();
            
            _context.ChatMessages.RemoveRange(_context.ChatMessages.Where(c => matchIds.Contains(c.MatchId)));
            _context.Matches.RemoveRange(companyMatches);
            _context.UserAnswers.RemoveRange(_context.UserAnswers.Where(ua => ua.CompanyId == companyId));
            _context.Participants.RemoveRange(_context.Participants.Where(p => p.CompanyId == companyId));
            _context.Registrations.RemoveRange(_context.Registrations.Where(r => r.CompanyId == companyId));
            
            await _context.SaveChangesAsync();

            var participants = new List<Participant>();
            var registrations = new List<Registration>();

            // 2. Create realistic personas
            // Group 1: Tech & Innovation
            participants.Add(CreateParticipant(
                companyId,
                "Erik", "Johansson", "CEO", "TechNova AB", "erik@technova.se",
                "https://images.unsplash.com/photo-1560250097-0b93528c311a?auto=format&fit=crop&q=80&w=200",
                "Vi bygger nästa generations AI-lösningar för e-handel.",
                "tech,strat", "", 
                "leads",
                "circular,growth",
                "Innovativa AI-lösningar för modern handel."
            ));

             participants.Add(CreateParticipant(
                companyId,
                "Anna", "Lindberg", "Marketing Director", "Creative Pulse", "anna@creativepulse.se",
                "https://images.unsplash.com/photo-1573496359142-b8d87734a5a2?auto=format&fit=crop&q=80&w=200",
                "Hjälper tech-bolag att synas och växa genom storytelling.",
                "sales", "",
                "partners", 
                "ai,growth",
                "Digital marknadsföringsbyrå med fokus på tech."
            ));

              participants.Add(CreateParticipant(
                companyId,
                "Ariel", "Imperio", "Lead Developer", "IT-Maskinen", "ariel@itmaskinen.se",
                "https://images.unsplash.com/photo-1519085360753-af0119f7cbe7?auto=format&fit=crop&q=80&w=200",
                "Passionerad utvecklare som älskar att bygga smarta lösningar med Angular och .NET.",
                "tech", "",
                "partners", 
                "ai,growth,rome",
                "Systemutveckling och IT-konsulting sedan 25 år."
            ));

            // Group 2: Sustainability & Logistics
            participants.Add(CreateParticipant(
                companyId,
                "Lars", "Bergström", "Supply Chain Manager", "GreenLogistics", "lars@greenlogistics.se",
                "https://images.unsplash.com/photo-1472099645785-5658abf4ff4e?auto=format&fit=crop&q=80&w=200",
                "Vi optimerar logistikkedjor för minskat klimatavtryck.",
                "sust", "",
                "facility",
                "local,circular",
                "Grön logistik och hållbara transporter."
            ));

            participants.Add(CreateParticipant(
                companyId,
                "Maria", "Andersson", "Digital Strategist", "Future Systems", "maria@futuresystems.se",
                "https://images.unsplash.com/photo-1580489944761-15a19d654956?auto=format&fit=crop&q=80&w=200",
                "Expert på att digitalisera traditionella industrier.",
                "tech", "",
                "partners",
                "circular,local",
                "Digital strategi och affärsutveckling."
            ));

            // Group 3: Leadership & HR
            participants.Add(CreateParticipant(
                companyId,
                "Karin", "Ek", "HR Chef", "PeopleFirst", "karin@peoplefirst.se",
                "https://images.unsplash.com/photo-1598550874175-4d7112ee7f43?auto=format&fit=crop&q=80&w=200",
                "Vi hjälper företag att bygga starka team och behålla talanger.",
                "strat", "",
                "partners",
                "growth,future",
                "HR-tjänster och talent management."
            ));

            participants.Add(CreateParticipant(
                companyId,
                "Per", "Olsson", "Bolagsjurist", "Lag & Rätt AB", "per@lagratt.se",
                "https://images.unsplash.com/photo-1519085360753-af0119f7cbe7?auto=format&fit=crop&q=80&w=200",
                "Specialiserad på arbetsrätt och företagsavtal.",
                "strat", "",
                "partners",
                "rome,future",
                "Juridisk rådgivning för småföretagare."
            ));

             // Group 4: Finance & Growth
            participants.Add(CreateParticipant(
                companyId,
                "Sofia", "Nylund", "CFO", "Growth Capital", "sofia@growthcap.se",
                "https://images.unsplash.com/photo-1551836022-d5d88e9218df?auto=format&fit=crop&q=80&w=200",
                "Investerar i lovande startups i Nacka-området.",
                "invest", "",
                "funding",
                "growth,circular",
                "Venture capital och tillväxtfinansiering."
            ));

            participants.Add(CreateParticipant(
                companyId,
                "Johan", "Svensson", "Sustainability Consultant", "EcoVision", "johan@ecovision.se",
                "https://images.unsplash.com/photo-1507003211169-0a1dd7228f2d?auto=format&fit=crop&q=80&w=200",
                "Hjälper företag att ställa om till grön energi.",
                "sust", "",
                "partners",
                "circular,ai",
                "Hållbarhetsrådgivning för små och stora företag."
            ));

            // Random Fillers
             participants.Add(CreateParticipant(
                companyId,
                "Mikael", "Persson", "Säljchef", "SalesPro", "mikael@salespro.se",
                "https://images.unsplash.com/photo-1500648767791-00dcc994a43e?auto=format&fit=crop&q=80&w=200",
                "Expert på B2B-försäljning och mötesbokning.",
                "sales", "",
                "leads",
                "growth,future",
                "Specialister på B2B försäljningsprocesser."
            ));

            participants.Add(CreateParticipant(
                companyId,
                "Linda", "Karlsson", "VD", "Nacka Bygg", "linda@nackabygg.se",
                "https://images.unsplash.com/photo-1544005313-94ddf0286df2?auto=format&fit=crop&q=80&w=200",
                "Bygger framtidens bostäder i Nacka.",
                "strat", "",
                "facility",
                "local,growth",
                "Bygg- och fastighetsutveckling i Nacka."
            ));

            // 3. Add 110+ random participants for stress testing AI matching
            var random = new Random();
            var firstNames = new[] { "Lars", "Erik", "Karl", "Anders", "Johan", "Per", "Nils", "Sven", "Lennart", "Hans", "Anna", "Eva", "Maria", "Karin", "Kristina", "Lena", "Sara", "Ulla", "Ingrid", "Birgitta" };
            var lastNames = new[] { "Andersson", "Johansson", "Karlsson", "Nilsson", "Eriksson", "Larsson", "Olsson", "Persson", "Svensson", "Gustafsson", "Pettersson", "Jonsson", "Jansson", "Hansson", "Bengtsson", "Jönsson", "Lindberg", "Jakobsson", "Magnusson", "Lindström" };
            var titles = new[] { "CEO", "Founder", "Product Manager", "Developer", "Marketing Lead", "Sales Manager", "HR Specialist", "Consultant", "Project Manager", "Architect" };
            var orgs = new[] { "Nacka Tech", "Stockholm Innovate", "Future Systems", "Green Solutions", "Blue Horizon", "Creative Hub", "Global Logistics", "Finance Pros", "Health Plus", "EduGrowth" };
            var superpowers = new[] { "sales", "tech", "strat", "sust", "invest", "health", "facility" };
            var challenges = new[] { "leads", "partners", "talent", "funding", "facility", "sales" }; 
            var topicsPool = new[] { "ai", "local", "future", "circular", "growth", "rome" };
            var companyDescriptions = new[] { 
                "Vi driver digital transformation i Nacka.",
                "Hållbara transportlösningar för alla.",
                "Innovativ produktutveckling inom tech.",
                "Finansiell rådgivning för tillväxtbolag.",
                "Rekrytering av framtidens talanger.",
                "Skapar magiska upplevelser genom design.",
                "Smidig logistik för den globala marknaden.",
                "Sjukvårdstjänster med patienten i fokus.",
                "Utbildning och kompetensutveckling.",
                "Fastighetsförvaltning med hjärta."
            };

            for (int i = 0; i < 110; i++)
            {
                var first = firstNames[random.Next(firstNames.Length)];
                var last = lastNames[random.Next(lastNames.Length)];
                var title = titles[random.Next(titles.Length)];
                var org = orgs[random.Next(orgs.Length)];
                var email = $"{first.ToLower()}.{last.ToLower()}.{i}@example.com";
                
                // Shuffle and pick 1-2 superpowers
                var selectedSuperpowers = superpowers.OrderBy(x => random.Next()).Take(random.Next(1, 3)).ToList();
                
                // Shuffle topics
                var selectedTopics = topicsPool.OrderBy(x => random.Next()).Take(2).ToList();
                
                 participants.Add(CreateParticipant(
                    companyId,
                    first, last, title, org, email,
                    $"https://i.pravatar.cc/200?u={email}",
                    $"Jag är intresserad av {selectedTopics[0]} och söker samarbeten inom {org}.",
                    string.Join(",", selectedSuperpowers), "",
                    challenges[random.Next(challenges.Length)],
                    string.Join(",", selectedTopics),
                    companyDescriptions[random.Next(companyDescriptions.Length)]
                ));
            }

            _context.Participants.AddRange(participants);
            
            // 3. Save Participants first to get IDs
            await _context.SaveChangesAsync();

            registrations = new List<Registration>();
            var answers = new List<UserAnswer>();

            foreach (var p in participants)
            {
                // Create Registrations
                registrations.Add(new Registration
                {
                    Firstname = p.Firstname,
                    Lastname = p.Lastname,
                    Email = p.Email,
                    Organization = p.Organization,
                    Title = p.Title,
                    CompanyId = companyId,
                    CreatedAt = p.CreatedAt
                });

                // Create UserAnswers (q1-q5) to align with dynamic AI matching
                answers.Add(new UserAnswer { CompanyId = companyId, ParticipantId = p.Id, QuestionId = "q1", AnswerValue = p.Superpower ?? "" });
                answers.Add(new UserAnswer { CompanyId = companyId, ParticipantId = p.Id, QuestionId = "q2", AnswerValue = p.Challenge ?? "" });
                answers.Add(new UserAnswer { CompanyId = companyId, ParticipantId = p.Id, QuestionId = "q3", AnswerValue = p.Topics ?? "" });
                answers.Add(new UserAnswer { CompanyId = companyId, ParticipantId = p.Id, QuestionId = "q4", AnswerValue = p.Bio ?? "" });
                answers.Add(new UserAnswer { CompanyId = companyId, ParticipantId = p.Id, QuestionId = "q5", AnswerValue = p.CompanyDescription ?? "" });
            }
            
            _context.Registrations.AddRange(registrations);
            _context.UserAnswers.AddRange(answers);

            await _context.SaveChangesAsync();

            return Ok(new { message = $"Seeded {participants.Count} participants, registrations and dynamic answers." });
        }

        private Participant CreateParticipant(
            Guid companyId,
            string first, string last, string title, string org, string email, string photo, 
            string bio, string super, string superOther, string challenge, string topics,
            string companyDescription)
        {
            return new Participant
            {
                CompanyId = companyId,
                Firstname = first,
                Lastname = last,
                Title = title,
                Organization = org,
                Email = email,
                Photo = photo,
                Bio = bio,
                Superpower = super,
                SuperpowerOther = superOther,
                Challenge = challenge,
                Topics = topics,
                TopicsOther = "",
                CompanyDescription = companyDescription,
                CreatedAt = DateTime.UtcNow
            };
        }
    }
}
