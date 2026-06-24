using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NackaMatchmaking.API.Data;
using NackaMatchmaking.API.Models;

namespace NackaMatchmaking.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SettingsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly NackaMatchmaking.API.Services.IEmailService _emailService;
        private readonly IConfiguration _configuration;

        public SettingsController(ApplicationDbContext context, NackaMatchmaking.API.Services.IEmailService emailService, IConfiguration configuration)
        {
            _context = context;
            _emailService = emailService;
            _configuration = configuration;
        }

        [HttpGet("{key}")]
        public async Task<ActionResult<SiteSetting>> GetSetting(string key, [FromQuery] Guid companyId)
        {
            // Allowing Guid.Empty for system-wide settings.

            var setting = await _context.Settings.FindAsync(companyId, key);
            if (setting == null)
            {
                // Return a 200 OK with a null value instead of 404 to avoid console errors
                // for optional settings in a multi-tenant environment.
                return Ok(new SiteSetting { CompanyId = companyId, Key = key, Value = (string?)null });
            }
            return setting;
        }

        [HttpPut("{key}")]
        [Microsoft.AspNetCore.Authorization.Authorize] // Only admin
        public async Task<IActionResult> UpdateSetting(string key, [FromBody] SiteSetting model)
        {
            if (key != model.Key)
            {
                return BadRequest();
            }

            // Determine target company ID
            Guid targetCompanyId;
            var companyIdString = User.FindFirst("companyId")?.Value;

            var systemId = Guid.Parse("11111111-1111-1111-1111-111111111111");

            if (model.CompanyId == systemId && User.IsInRole("SuperAdmin"))
            {
                targetCompanyId = systemId;
            }
            else
            {
                if (string.IsNullOrEmpty(companyIdString) || !Guid.TryParse(companyIdString, out targetCompanyId))
                {
                    return Unauthorized("Company ID not found in token for tenant isolation.");
                }
            }

            var setting = await _context.Settings.FindAsync(targetCompanyId, key);
            if (setting == null)
            {
                setting = new SiteSetting { CompanyId = targetCompanyId, Key = key, Value = model.Value };
                _context.Settings.Add(setting);
            }
            else
            {
                setting.Value = model.Value;
                _context.Entry(setting).State = EntityState.Modified;
            }

            try
            {
                await _context.SaveChangesAsync();
                return NoContent();
            }
            catch (Exception ex)
            {
                var message = ex.InnerException?.Message ?? ex.Message;
                return StatusCode(500, $"Database error: {message}");
            }
        }

        [HttpPost("test-email")]
        [Microsoft.AspNetCore.Authorization.Authorize] // Only admin
        public async Task<IActionResult> SendTestEmail([FromBody] TestEmailDto model)
        {
            if (string.IsNullOrEmpty(model.Email) || string.IsNullOrEmpty(model.Template))
            {
                return BadRequest("Email and Template are required.");
            }

            string subject = "[TEST] ";
            if (model.Type == "InvitationEmailTemplate") {
                subject += "Maxa ditt nätverkande på Nacka Företagarträff!";
            } else if (model.Type == "VerificationEmail") {
                subject += "Verify your account";
            } else {
                subject += "Dina AI-matchningar för Nacka Företagarträff är här!";
            }

            var frontendUrl = _configuration["FrontendUrl"] ?? "https://matchmaking.itmaskinen.se";

            // Dummy data for test email
            var participantName = "Anna Andersson (Test)";
            var companyName = "Tech Innovators AB";
            var dummyCompanyId = Guid.NewGuid();
            var matchmakingLink = $"{frontendUrl}?companyId={dummyCompanyId}&id=dummy123";
            var resultsTable = $"{frontendUrl}/matches?companyId={dummyCompanyId}&id=dummy123";
            var verificationLink = $"{frontendUrl}/login?verifyToken=test_token_123";

            var body = model.Template
                .Replace("{{ParticipantName}}", participantName)
                .Replace("{{CompanyName}}", companyName)
                .Replace("{{MatchmakingLink}}", matchmakingLink)
                .Replace("{{ResultsTable}}", resultsTable)
                .Replace("{{VerificationLink}}", verificationLink);

            try
            {
                await _emailService.SendEmailAsync(model.Email, subject, body);
                return Ok(new { message = "Test email sent successfully." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error sending test email: {ex.Message}");
            }
        }
    }

    public class TestEmailDto 
    {
        public string Type { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Template { get; set; } = string.Empty;
    }
}
