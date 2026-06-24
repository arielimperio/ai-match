using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using NackaMatchmaking.API.Data;
using NackaMatchmaking.API.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;

namespace NackaMatchmaking.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Microsoft.AspNetCore.Authorization.Authorize]
    public class AuthController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly NackaMatchmaking.API.Services.IEmailService _emailService;
        private readonly IHttpClientFactory _httpClientFactory;

        public AuthController(ApplicationDbContext context, IConfiguration configuration, NackaMatchmaking.API.Services.IEmailService emailService, IHttpClientFactory httpClientFactory)
        {
            _context = context;
            _configuration = configuration;
            _emailService = emailService;
            _httpClientFactory = httpClientFactory;
        }
        
        [Microsoft.AspNetCore.Authorization.AllowAnonymous]
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginModel model)
        {
            var user = await _context.AdminUsers
                .Include(u => u.UserCompanies)
                .ThenInclude(uc => uc.Company)
                .FirstOrDefaultAsync(u => u.Username == model.Username);
            
            // In a real app, use a proper password hasher!
            if (user == null || user.PasswordHash != model.Password)
            {
                return Unauthorized("Invalid credentials");
            }

            if (!user.IsVerified)
            {
                if (user.VerificationToken == null) 
                {
                    return Unauthorized("Account has been deactivated. Please contact support.");
                }
                return Unauthorized("Email hasn't been verified. Please check your inbox.");
            }

            // Legacy Backfill: If user has a primary CompanyId but no record in UserCompanies, create it now
            if (user.CompanyId.HasValue && !user.UserCompanies.Any(uc => uc.CompanyId == user.CompanyId.Value))
            {
                var legacyLink = new UserCompany
                {
                    UserId = user.Id,
                    CompanyId = user.CompanyId.Value,
                    Role = user.Role == "SuperAdmin" ? "SuperAdmin" : "Admin",
                    JoinedAt = DateTime.UtcNow
                };
                _context.UserCompanies.Add(legacyLink);
                await _context.SaveChangesAsync();
                
                // Refresh companies list
                var company = await _context.Companies.FindAsync(user.CompanyId.Value);
                user.UserCompanies.Add(new UserCompany { CompanyId = user.CompanyId.Value, Company = company });
            }

            var companies = user.UserCompanies.Select(uc => new {
                uc.CompanyId,
                CompanyName = uc.Company?.Name
            }).ToList();

            var token = GenerateJwtToken(user);
            return Ok(new { token, companies });
        }

        [Microsoft.AspNetCore.Authorization.AllowAnonymous]
        [HttpGet("verify-email")]
        public async Task<IActionResult> VerifyEmail([FromQuery] string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return BadRequest(new { message = "Invalid token." });
            }

            var user = await _context.AdminUsers.FirstOrDefaultAsync(u => u.VerificationToken == token);
            if (user == null)
            {
                return BadRequest(new { message = "Invalid or expired token." });
            }

            user.IsVerified = true;
            user.VerificationToken = null;
            await _context.SaveChangesAsync();

            return Ok(new { message = "Email verified successfully." });
        }

        [HttpPatch("update-password")]
        public async Task<IActionResult> UpdatePassword([FromBody] UpdatePasswordModel model)
        {
            var userIdString = User.FindFirst("id")?.Value;
            if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out Guid userId))
            {
                return Unauthorized("User ID not found in token");
            }

            var user = await _context.AdminUsers.FindAsync(userId);
            if (user == null)
            {
                return NotFound("User not found");
            }

            // In a real app, use a proper password hasher!
            if (user.PasswordHash != model.CurrentPassword)
            {
                return BadRequest("Nuvarande lösenord är felaktigt.");
            }

            user.PasswordHash = model.NewPassword;
            await _context.SaveChangesAsync();

            return Ok(new { message = "Password updated" });
        }

        private string GenerateJwtToken(AdminUser user)
        {
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]!));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Username),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim("id", user.Id.ToString()),
                new Claim(ClaimTypes.Role, user.Role)
            };

            if (user.CompanyId.HasValue)
            {
                claims.Add(new Claim("companyId", user.CompanyId.Value.ToString()));
            }

            var token = new JwtSecurityToken(
                issuer: _configuration["Jwt:Issuer"],
                audience: _configuration["Jwt:Audience"],
                claims: claims,
                expires: DateTime.Now.AddHours(24),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        // Helper to seed a user if none exists (for dev purposes)
        [Microsoft.AspNetCore.Authorization.AllowAnonymous]
        [HttpPost("seed")]
        public async Task<IActionResult> SeedAdmin()
        {
            if (!await _context.AdminUsers.AnyAsync())
            {
                _context.AdminUsers.Add(new AdminUser { Username = "admin", PasswordHash = "password123" });
                await _context.SaveChangesAsync();
                return Ok("Admin user created");
            }
            return Ok("Admin user already exists");
        }

        [Microsoft.AspNetCore.Authorization.AllowAnonymous]
        [HttpPost("setup")]
        public async Task<IActionResult> SetupSystem([FromBody] SetupRequestModel model)
        {
            if (string.IsNullOrWhiteSpace(model.ProjectName))
            {
                model.ProjectName = !string.IsNullOrWhiteSpace(model.SuperAdmin.FirstName)
                    ? $"{model.SuperAdmin.FirstName} {model.SuperAdmin.LastName}".Trim()
                    : model.SuperAdmin.Email;

                if (string.IsNullOrWhiteSpace(model.ProjectName))
                {
                    model.ProjectName = "Default Workspace";
                }
            }

            if (string.IsNullOrWhiteSpace(model.City))
            {
                model.City = "-";
            }

            if (string.IsNullOrWhiteSpace(model.ProjectName) || 
                string.IsNullOrWhiteSpace(model.SuperAdmin.Email) || 
                string.IsNullOrWhiteSpace(model.SuperAdmin.Password))
            {
                return BadRequest("Missing required fields.");
            }

            // Check if email/username already exists
            var existingUser = await _context.AdminUsers.FirstOrDefaultAsync(u => u.Username == model.SuperAdmin.Email || u.Email == model.SuperAdmin.Email);
            
            AdminUser adminUser;
            if (existingUser != null)
            {
                // Verify password for security before linking
                if (existingUser.PasswordHash != model.SuperAdmin.Password)
                {
                    return BadRequest("An account with this email already exists. Please provide the correct password to link a new company.");
                }
                adminUser = existingUser;
            }
            else
            {
                // Create new AdminUser
                adminUser = new AdminUser
                {
                    Id = Guid.NewGuid(),
                    Username = model.SuperAdmin.Email, // Use email as username for login
                    PasswordHash = model.SuperAdmin.Password, // In a real app, hash this!
                    FirstName = model.SuperAdmin.FirstName,
                    LastName = model.SuperAdmin.LastName,
                    Email = model.SuperAdmin.Email,
                    Role = !await _context.AdminUsers.AnyAsync() ? "SuperAdmin" : "Admin",
                    IsVerified = false,
                    VerificationToken = Guid.NewGuid().ToString("N")
                };
                _context.AdminUsers.Add(adminUser);
            }

            if (existingUser == null)
            {
                await _context.SaveChangesAsync();

                if (!adminUser.IsVerified)
                {
                    var frontendUrl = _configuration["FrontendUrl"] ?? "http://localhost:4200";
                    var verificationLink = $"{frontendUrl}/login?verifyToken={adminUser.VerificationToken}";

                    var systemId = Guid.Parse("11111111-1111-1111-1111-111111111111");
                    var customSubject = await _context.Settings.FirstOrDefaultAsync(s => s.CompanyId == systemId && s.Key == "VerificationEmailSubject");
                    var customBody = await _context.Settings.FirstOrDefaultAsync(s => s.CompanyId == systemId && s.Key == "VerificationEmailHtmlBody");

                    string subject = customSubject?.Value ?? "Verify your account";
                    string emailHtml = customBody?.Value ?? $@"
                        <div style='font-family: sans-serif; max-width: 600px; margin: 0 auto; color: #333;'>
                            <h2>Welcome to Nacka Matchmaking</h2>
                            <p>Thank you for registering. Please click the link below to verify your account so you can log in:</p>
                            <p>
                                <a href='{verificationLink}' style='display: inline-block; padding: 10px 20px; background-color: #3b82f6; color: #fff; text-decoration: none; border-radius: 5px; font-weight: bold;'>Verify Account</a>
                            </p>
                            <p style='color: #666; font-size: 12px;'>If you did not request this, please ignore this email.</p>
                        </div>
                    ";

                    if (customBody != null && !string.IsNullOrEmpty(emailHtml))
                    {
                        emailHtml = emailHtml.Replace("{{VerificationLink}}", verificationLink);
                    }

                    await _emailService.SendEmailAsync(adminUser.Email, subject, emailHtml);
                }

                return Ok(new { 
                    message = "System setup complete.", 
                    requiresVerification = !adminUser.IsVerified 
                });
            }

            var company = new Company
            {
                Id = Guid.NewGuid(),
                Name = model.ProjectName,
                City = model.City,
                ParentId = model.ParentId,
                CreatedAt = DateTime.UtcNow
            };
            _context.Companies.Add(company);

            // Ensure old company is in the join table if this is an existing user (Legacy Backfill)
            if (existingUser != null && existingUser.CompanyId.HasValue)
            {
                var alreadyLinked = await _context.UserCompanies.AnyAsync(uc => uc.UserId == existingUser.Id && uc.CompanyId == existingUser.CompanyId.Value);
                if (!alreadyLinked)
                {
                    _context.UserCompanies.Add(new UserCompany
                    {
                        UserId = existingUser.Id,
                        CompanyId = existingUser.CompanyId.Value,
                        Role = existingUser.Role == "SuperAdmin" ? "SuperAdmin" : "Admin",
                        JoinedAt = DateTime.UtcNow
                    });
                }
            }

            // Link User to NEW Company
            var userCompany = new UserCompany
            {
                UserId = adminUser.Id,
                CompanyId = company.Id,
                Role = "Admin",
                JoinedAt = DateTime.UtcNow
            };
            _context.UserCompanies.Add(userCompany);

            // Set as active company
            adminUser.CompanyId = company.Id;
            // Seed Default Settings for the configured company
            var defaultSettings = new List<SiteSetting>
            {
                new SiteSetting { CompanyId = company.Id, Key = "SurveyOpen", Value = "true" },
                new SiteSetting { CompanyId = company.Id, Key = "WelcomeTitle", Value = "Welcome!" },
                new SiteSetting { CompanyId = company.Id, Key = "WelcomeTagline", Value = "Matchmaking 2026" },
                new SiteSetting { CompanyId = company.Id, Key = "WelcomeDescription", Value = "Welcome to this year's matchmaking event! Find your best connections with our AI-powered tool." },
                new SiteSetting { CompanyId = company.Id, Key = "WelcomeButton", Value = "Start Matchmaking" },
                new SiteSetting { CompanyId = company.Id, Key = "WelcomeLogo", Value = "" },
                new SiteSetting { CompanyId = company.Id, Key = "ProfileTitle", Value = "My Details" },
                new SiteSetting { CompanyId = company.Id, Key = "ProfileDescription", Value = "Please verify that your details are correct so others can find you." },
                new SiteSetting { CompanyId = company.Id, Key = "SuccessMessage", Value = "We will send an email with your personal matches at least 48 hours before the event starts." },
                
                // Invitation Email
                new SiteSetting { CompanyId = company.Id, Key = "InvitationEmailSubject", Value = "Maximize your networking - try our new AI matchmaking!" },
                new SiteSetting { CompanyId = company.Id, Key = "InvitationEmailTemplate", Value = @"
                    <div style='font-family: sans-serif; max-width: 600px; line-height: 1.6;'>
                        <p>Hi {{ParticipantName}}!</p>
                        <p>Join our AI-based matchmaking service to find the best connections at our event.</p>
                        <p>👉 <a href='{{MatchmakingLink}}' style='color: #6366f1; font-weight: bold; text-decoration: none;'>Yes, I want to find the right contacts via AI!</a></p>
                        <p>Best regards,<br>The Team</p>
                    </div>" },

                // Result Email
                new SiteSetting { CompanyId = company.Id, Key = "ResultEmailSubject", Value = "Your AI matches are here!" },
                new SiteSetting { CompanyId = company.Id, Key = "ResultEmailTemplate", Value = @"
                    <div style='font-family: sans-serif; max-width: 600px; line-height: 1.6;'>
                        <p>Hi {{ParticipantName}}!</p>
                        <p>Our AI has finished finding your best matches. Review them now and show interest to start booking meetings!</p>
                        <p>👉 <a href='{{ResultsTable}}' style='color: #6366f1; font-weight: bold; text-decoration: none;'>See my AI matches here</a></p>
                        <p>Warm regards,<br>The Team</p>
                    </div>" },

                // Feedback Email
                new SiteSetting { CompanyId = company.Id, Key = "FeedbackEmailSubject", Value = "How was your matchmaking experience?" },
                new SiteSetting { CompanyId = company.Id, Key = "FeedbackEmailTemplate", Value = @"
                    <div style='font-family: sans-serif; line-height: 1.6;'>
                        <h2>Hi {{ParticipantName}}!</h2>
                        <p>We hope you had productive meetings!</p>
                        <p>We would love to hear your feedback. Please click the link below to see your results and leave feedback on your matches:</p>
                        <p style='margin: 20px 0;'>
                            <a href='{{ResultsLink}}' style='color: #6366f1; text-decoration: none; font-weight: bold;'>See results & leave feedback</a>
                        </p>
                        <p>Thank you!<br>The Team</p>
                    </div>" },


                new SiteSetting { CompanyId = company.Id, Key = "BrandingPrimaryColor", Value = "#1e293b" },
                new SiteSetting { CompanyId = company.Id, Key = "BrandingSecondaryColor", Value = "#6366f1" }
            };

            _context.Settings.AddRange(defaultSettings);

            await _context.SaveChangesAsync();

            // Send Verification Email only if NOT already verified
            if (!adminUser.IsVerified)
            {
                var frontendUrl = _configuration["FrontendUrl"] ?? "http://localhost:4200";
                var verificationLink = $"{frontendUrl}/login?verifyToken={adminUser.VerificationToken}";

                var systemId = Guid.Parse("11111111-1111-1111-1111-111111111111");
                var customSubject = await _context.Settings.FirstOrDefaultAsync(s => s.CompanyId == systemId && s.Key == "VerificationEmailSubject");
                var customBody = await _context.Settings.FirstOrDefaultAsync(s => s.CompanyId == systemId && s.Key == "VerificationEmailHtmlBody");

                string subject = customSubject?.Value ?? "Verify your account";
                string emailHtml = customBody?.Value ?? $@"
                    <div style='font-family: sans-serif; max-width: 600px; margin: 0 auto; color: #333;'>
                        <h2>Welcome to Nacka Matchmaking</h2>
                        <p>Thank you for registering. Please click the link below to verify your account so you can log in:</p>
                        <p>
                            <a href='{verificationLink}' style='display: inline-block; padding: 10px 20px; background-color: #3b82f6; color: #fff; text-decoration: none; border-radius: 5px; font-weight: bold;'>Verify Account</a>
                        </p>
                        <p style='color: #666; font-size: 12px;'>If you did not request this, please ignore this email.</p>
                    </div>
                ";

                if (customBody != null && !string.IsNullOrEmpty(emailHtml))
                {
                    emailHtml = emailHtml.Replace("{{VerificationLink}}", verificationLink);
                }

                await _emailService.SendEmailAsync(adminUser.Email, subject, emailHtml);
            }

            return Ok(new { 
                message = "System setup complete.", 
                requiresVerification = !adminUser.IsVerified 
            });
        }

        [HttpPost("manual-verify/{id}")]
        public async Task<IActionResult> ManualVerifyAccount(Guid id)
        {
            var userIdString = User.FindFirst("id")?.Value;
            if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out Guid currentUserId))
            {
                return Unauthorized();
            }

            var currentUser = await _context.AdminUsers.FindAsync(currentUserId);
            if (currentUser == null || currentUser.Role != "SuperAdmin")
            {
                return StatusCode(403, new { message = "Only SuperAdmins can manually verify accounts." });
            }

            var userToVerify = await _context.AdminUsers.FindAsync(id);
            if (userToVerify == null)
            {
                return NotFound("Account not found.");
            }

            if (userToVerify.IsVerified)
            {
                return BadRequest("Account is already verified.");
            }

            userToVerify.IsVerified = true;
            await _context.SaveChangesAsync();

            return Ok(new { message = "Account successfully verified." });
        }

        [HttpPost("deactivate/{id}")]
        public async Task<IActionResult> DeactivateAccount(Guid id)
        {
            var userIdString = User.FindFirst("id")?.Value;
            if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out Guid currentUserId))
            {
                return Unauthorized();
            }

            var currentUser = await _context.AdminUsers.FindAsync(currentUserId);
            if (currentUser == null || currentUser.Role != "SuperAdmin")
            {
                return StatusCode(403, new { message = "Only SuperAdmins can deactivate accounts." });
            }

            var userToDeactivate = await _context.AdminUsers.FindAsync(id);
            if (userToDeactivate == null)
            {
                return NotFound("Account not found.");
            }

            if (!userToDeactivate.IsVerified && userToDeactivate.VerificationToken == null)
            {
                return BadRequest("Account is already deactivated.");
            }

            userToDeactivate.IsVerified = false;
            // Setting VerificationToken to null helps differentiate between 'Deactivated' and 'Pending Verification'
            userToDeactivate.VerificationToken = null; 
            await _context.SaveChangesAsync();

            return Ok(new { message = "Account successfully deactivated." });
        }

        [HttpPost("resend-verification/{id}")]
        [Microsoft.AspNetCore.Authorization.Authorize(Roles = "SuperAdmin")]
        public async Task<IActionResult> ResendVerification(Guid id)
        {
            var user = await _context.AdminUsers.FindAsync(id);
            if (user == null) return NotFound("User not found.");
            if (user.IsVerified) return BadRequest("User is already verified.");

            if (string.IsNullOrEmpty(user.VerificationToken))
            {
                user.VerificationToken = Guid.NewGuid().ToString("N");
                await _context.SaveChangesAsync();
            }

            var frontendUrl = _configuration["FrontendUrl"] ?? "http://localhost:4200";
            var verificationLink = $"{frontendUrl}/login?verifyToken={user.VerificationToken}";

            var systemId = Guid.Parse("11111111-1111-1111-1111-111111111111");
            var customSubject = await _context.Settings.FirstOrDefaultAsync(s => s.CompanyId == systemId && s.Key == "VerificationEmailSubject");
            var customBody = await _context.Settings.FirstOrDefaultAsync(s => s.CompanyId == systemId && s.Key == "VerificationEmailHtmlBody");

            string subject = customSubject?.Value ?? "Verify your account";
            string emailHtml = customBody?.Value ?? $@"
                <div style='font-family: sans-serif; max-width: 600px; margin: 0 auto; color: #333;'>
                    <h2>Account Verification</h2>
                    <p>Please click the link below to verify your account so you can log in:</p>
                    <p>
                        <a href='{verificationLink}' style='display: inline-block; padding: 10px 20px; background-color: #3b82f6; color: #fff; text-decoration: none; border-radius: 5px; font-weight: bold;'>Verify Account</a>
                    </p>
                    <p style='color: #666; font-size: 12px;'>If you did not request this, please ignore this email.</p>
                </div>";

            if (customBody != null && !string.IsNullOrEmpty(emailHtml))
            {
                emailHtml = emailHtml.Replace("{{VerificationLink}}", verificationLink);
            }

            await _emailService.SendEmailAsync(user.Email, subject, emailHtml);

            return Ok(new { message = "Verification email sent." });
        }

        [Microsoft.AspNetCore.Authorization.AllowAnonymous]
        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordModel model)
        {
            var user = await _context.AdminUsers.FirstOrDefaultAsync(u => u.Email == model.Email);
            if (user == null)
            {
                // To avoid email enumeration, we return Ok even if user not found.
                return Ok(new { message = "If your email is registered, you will receive a password reset link shortly." });
            }

            user.PasswordResetToken = Guid.NewGuid().ToString("N");
            user.PasswordResetTokenExpiry = DateTime.UtcNow.AddHours(1);
            await _context.SaveChangesAsync();

            var frontendUrl = _configuration["FrontendUrl"] ?? "http://localhost:4200";
            var resetLink = $"{frontendUrl}/reset-password?token={user.PasswordResetToken}&email={user.Email}";

            var systemId = Guid.Parse("11111111-1111-1111-1111-111111111111");
            var customSubject = await _context.Settings.FirstOrDefaultAsync(s => s.CompanyId == systemId && s.Key == "PasswordResetEmailSubject");
            var customBody = await _context.Settings.FirstOrDefaultAsync(s => s.CompanyId == systemId && s.Key == "PasswordResetEmailHtmlBody");

            string subject = customSubject?.Value ?? "Password Reset Request";
            string emailHtml = customBody?.Value ?? $@"
                <div style='font-family: sans-serif; color: #333; line-height: 1.6; max-width: 600px;'>
                    <h2 style='color: #1e293b; margin-top: 0;'>Password Reset Request</h2>
                    <p>We received a request to reset your password. Click the link below to choose a new one:</p>
                    <p style='margin: 20px 0;'>
                        <a href='{resetLink}' style='color: #3b82f6; font-weight: bold; text-decoration: underline;'>Reset Password</a>
                    </p>
                    <p>If you did not request this, you can safely ignore this email.</p>
                    <p style='word-break: break-all; color: #64748b; font-size: 13px;'>{resetLink}</p>
                </div>
            ";

            if (customBody != null && !string.IsNullOrEmpty(emailHtml))
            {
                emailHtml = emailHtml.Replace("{{ResetLink}}", resetLink);
            }

            await _emailService.SendEmailAsync(user.Email, subject, emailHtml);

            return Ok(new { message = "If your email is registered, you will receive a password reset link shortly." });
        }

        [Microsoft.AspNetCore.Authorization.AllowAnonymous]
        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordModel model)
        {
            var user = await _context.AdminUsers.FirstOrDefaultAsync(u => u.Email == model.Email && u.PasswordResetToken == model.Token);
            
            if (user == null || user.PasswordResetTokenExpiry < DateTime.UtcNow)
            {
                return BadRequest(new { message = "Invalid or expired reset token." });
            }

            // Update password
            user.PasswordHash = model.NewPassword;
            user.PasswordResetToken = null;
            user.PasswordResetTokenExpiry = null;
            
            await _context.SaveChangesAsync();

            return Ok(new { message = "Password reset successfully. You can now log in with your new password." });
        }

        public class RecommendEventSetupModel
        {
            public string Description { get; set; } = string.Empty;
        }

        public class CreateEventRequestModel
        {
            public string Name { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
            public string City { get; set; } = string.Empty;
            public Guid? ParentId { get; set; }
            public bool RoleSelectionEnabled { get; set; }
            public List<RoleDto> Roles { get; set; } = new();
            public List<QuestionDto> Questions { get; set; } = new();
            public string? EmailTemplate { get; set; }
            public string? ResultEmailTemplate { get; set; }
            public string? FeedbackEmailTemplate { get; set; }
            public string? BrandingPrimaryColor { get; set; }
            public string? BrandingSecondaryColor { get; set; }
            public string? BrandingBackgroundType { get; set; }
            public string? BrandingBackgroundColor { get; set; }
            public string? WelcomeTitle { get; set; }
            public string? WelcomeTagline { get; set; }
            public string? WelcomeDescription { get; set; }
            public string? WelcomeButton { get; set; }
        }

        public class RoleDto
        {
            public string Name { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
        }

        public class QuestionDto
        {
            public string Title { get; set; } = string.Empty;
            public string Type { get; set; } = string.Empty; // Choice, MultipleChoice, Text
            public List<string> Options { get; set; } = new();
            public bool IsHidden { get; set; }
        }

        [HttpPost("recommend-event-setup")]
        public async Task<IActionResult> RecommendEventSetup([FromBody] RecommendEventSetupModel model)
        {
            var systemId = Guid.Parse("11111111-1111-1111-1111-111111111111");
            var apiKeySetting = await _context.Settings.FirstOrDefaultAsync(s => s.CompanyId == systemId && s.Key == "AiApiKey");
            var providerSetting = await _context.Settings.FirstOrDefaultAsync(s => s.CompanyId == systemId && s.Key == "AiProvider");
            var modelSetting = await _context.Settings.FirstOrDefaultAsync(s => s.CompanyId == systemId && s.Key == "AiModel");

            string apiKey = apiKeySetting?.Value ?? "";
            string provider = (providerSetting?.Value ?? "gemini").ToLowerInvariant();
            string aiModel = modelSetting?.Value ?? (provider == "gemini" ? "gemini-1.5-flash" : "gpt-4o");

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                return BadRequest(new { message = "AI Engine is not configured. Please check the settings." });
            }

            string prompt = @$"
You are a professional event setup AI for a matchmaking platform.
Analyze the following event description and recommend the following things:
1. Role Selection: Is role selection recommended for this event? (e.g., Startup/Investor, Mentor/Mentee).
   If yes, return true and a list of recommended roles with names and descriptions.
   If no, return false and an empty list of roles.
2. Recommended Questions: Generate 3-5 tailored questions for participants of this event.
   Each question should have:
   - title (string)
   - type (string - ""Choice"", ""MultipleChoice"", or ""Text"")
   - options (array of strings, only if type is Choice or MultipleChoice)
   - isHidden (boolean, default false)
3. Email Templates: Generate three distinct, suggested email templates (HTML format):
   - Invitation Email Template: Include placeholders `{{{{MatchmakingLink}}}}` and `{{{{ParticipantName}}}}`.
   - Result Email Template: Include placeholders `{{{{ResultsTable}}}}` and `{{{{ParticipantName}}}}`.
   - Feedback Email Template: Include placeholders `{{{{ResultsLink}}}}` and `{{{{ParticipantName}}}}`.
4. Design & Branding Settings:
   - `brandingPrimaryColor`: A beautiful HEX color matching the event's vibe.
   - `brandingSecondaryColor`: A beautiful complementary HEX color matching the event's vibe.
   - `brandingBackgroundType`: Recommend ""color"" or ""gradient"".
   - `brandingBackgroundColor`: A beautiful background HEX color matching the event's vibe.
5. Welcome / Edit Page Text Settings:
   - `welcomeTitle`: Clear welcome title matching the event description.
   - `welcomeTagline`: Engaging tagline matching the event description.
   - `welcomeDescription`: Compelling description matching the event description.
   - `welcomeButton`: Text for start button.

Event Description:
{model.Description}

You MUST return exactly the following valid JSON structure and nothing else. Output raw JSON object directly:
{{
  ""roleSelectionEnabled"": true,
  ""roles"": [
    {{ ""name"": ""Startup"", ""description"": ""Looking for funding and enterprise clients"" }},
    {{ ""name"": ""Investor"", ""description"": ""Looking for high-potential startups to invest in"" }}
  ],
  ""questions"": [
    {{ ""title"": ""What is your primary goal at this event?"", ""type"": ""Choice"", ""options"": [""Fundraising"", ""Networking"", ""Hiring""], ""isHidden"": false }}
  ],
  ""emailTemplate"": ""<p>Hi {{{{ParticipantName}}}}!</p><p>Join our event matchmaking platform here: <a href='{{{{MatchmakingLink}}}}'>Matchmaking Link</a></p>"",
  ""resultEmailTemplate"": ""<p>Hi {{{{ParticipantName}}}}!</p><p>Review your AI-generated event matches here: <a href='{{{{ResultsTable}}}}'>View Matches</a></p>"",
  ""feedbackEmailTemplate"": ""<p>Hi {{{{ParticipantName}}}}!</p><p>How was your matchmaking experience? Give us your feedback: <a href='{{{{ResultsLink}}}}'>Feedback Link</a></p>"",
  ""brandingPrimaryColor"": ""#31a2ae"",
  ""brandingSecondaryColor"": ""#a60053"",
  ""brandingBackgroundType"": ""color"",
  ""brandingBackgroundColor"": ""#002a37"",
  ""welcomeTitle"": ""Welcome to our Matchmaking Event!"",
  ""welcomeTagline"": ""Connect with like-minded professionals."",
  ""welcomeDescription"": ""Answer a few questions and our AI will suggest the best connections for your goals and interests."",
  ""welcomeButton"": ""Get Started""
}}
";

            var client = _httpClientFactory.CreateClient();
            string contentString = "";

            try
            {
                if (provider == "gemini")
                {
                    var requestBody = new
                    {
                        contents = new[]
                        {
                            new
                            {
                                parts = new[]
                                {
                                    new { text = prompt }
                                }
                            }
                        },
                        generationConfig = new
                        {
                            responseMimeType = "application/json",
                            maxOutputTokens = 8000
                        }
                    };

                    var request = new HttpRequestMessage(HttpMethod.Post, $"https://generativelanguage.googleapis.com/v1beta/models/{aiModel}:generateContent?key={apiKey}");
                    request.Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

                    var response = await client.SendAsync(request);
                    if (!response.IsSuccessStatusCode)
                    {
                        var errStr = await response.Content.ReadAsStringAsync();
                        return BadRequest(new { message = $"Gemini API Error: {errStr}" });
                    }

                    var jsonResponse = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(jsonResponse);
                    var candidates = doc.RootElement.GetProperty("candidates");
                    if (candidates.GetArrayLength() > 0)
                    {
                        contentString = candidates[0].GetProperty("content").GetProperty("parts")[0].GetProperty("text").GetString() ?? "";
                    }
                }
                else
                {
                    var requestBody = new
                    {
                        model = aiModel,
                        messages = new[]
                        {
                            new { role = "system", content = "You are a helpful assistant that outputs valid JSON only." },
                            new { role = "user", content = prompt }
                        },
                        response_format = new { type = "json_object" },
                        max_tokens = 4000
                    };

                    var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions");
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
                    request.Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

                    var response = await client.SendAsync(request);
                    if (!response.IsSuccessStatusCode)
                    {
                        var errStr = await response.Content.ReadAsStringAsync();
                        return BadRequest(new { message = $"OpenAI API Error: {errStr}" });
                    }

                    var jsonResponse = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(jsonResponse);
                    var choice = doc.RootElement.GetProperty("choices")[0];
                    contentString = choice.GetProperty("message").GetProperty("content").GetString() ?? "";
                }

                if (string.IsNullOrEmpty(contentString))
                {
                    return BadRequest(new { message = "The AI returned an empty response." });
                }

                contentString = contentString.Trim();
                if (contentString.StartsWith("```json", StringComparison.OrdinalIgnoreCase)) contentString = contentString.Substring(7);
                else if (contentString.StartsWith("```")) contentString = contentString.Substring(3);
                if (contentString.EndsWith("```")) contentString = contentString.Substring(0, contentString.Length - 3);
                contentString = contentString.Trim();

                using var resultDoc = JsonDocument.Parse(contentString);
                return Ok(JsonSerializer.Deserialize<object>(contentString));
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"AI processing failed: {ex.Message}" });
            }
        }

        [HttpPost("create-event")]
        public async Task<IActionResult> CreateEvent([FromBody] CreateEventRequestModel model)
        {
            if (string.IsNullOrWhiteSpace(model.Name))
            {
                return BadRequest(new { message = "Event name cannot be empty." });
            }

            var userIdString = User.FindFirst("id")?.Value;
            if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out Guid userId))
            {
                return Unauthorized();
            }

            var user = await _context.AdminUsers.FindAsync(userId);
            if (user == null) return NotFound(new { message = "User not found." });

            var company = new Company
            {
                Id = Guid.NewGuid(),
                Name = model.Name,
                City = model.City,
                ParentId = model.ParentId,
                CreatedAt = DateTime.UtcNow
            };
            _context.Companies.Add(company);

            // Link user to new company
            var userCompany = new UserCompany
            {
                UserId = user.Id,
                CompanyId = company.Id,
                Role = "Admin",
                JoinedAt = DateTime.UtcNow
            };
            _context.UserCompanies.Add(userCompany);

            // Set as active company
            user.CompanyId = company.Id;

            // Seed default settings
            var settings = new List<SiteSetting>
            {
                new SiteSetting { CompanyId = company.Id, Key = "SurveyOpen", Value = "true" },
                new SiteSetting { CompanyId = company.Id, Key = "WelcomeTitle", Value = string.IsNullOrEmpty(model.WelcomeTitle) ? model.Name : model.WelcomeTitle },
                new SiteSetting { CompanyId = company.Id, Key = "WelcomeTagline", Value = string.IsNullOrEmpty(model.WelcomeTagline) ? "AI Matchmaking" : model.WelcomeTagline },
                new SiteSetting { CompanyId = company.Id, Key = "WelcomeDescription", Value = string.IsNullOrEmpty(model.WelcomeDescription) ? model.Description : model.WelcomeDescription },
                new SiteSetting { CompanyId = company.Id, Key = "WelcomeButton", Value = string.IsNullOrEmpty(model.WelcomeButton) ? "Start Matchmaking" : model.WelcomeButton },
                new SiteSetting { CompanyId = company.Id, Key = "SuccessMessage", Value = "We will send an email with your personal matches at least 48 hours before the event starts." },
                new SiteSetting { CompanyId = company.Id, Key = "BrandingPrimaryColor", Value = string.IsNullOrEmpty(model.BrandingPrimaryColor) ? "#1e293b" : model.BrandingPrimaryColor },
                new SiteSetting { CompanyId = company.Id, Key = "BrandingSecondaryColor", Value = string.IsNullOrEmpty(model.BrandingSecondaryColor) ? "#6366f1" : model.BrandingSecondaryColor },
                new SiteSetting { CompanyId = company.Id, Key = "BrandingBackgroundType", Value = string.IsNullOrEmpty(model.BrandingBackgroundType) ? "color" : model.BrandingBackgroundType },
                new SiteSetting { CompanyId = company.Id, Key = "BrandingBackgroundColor", Value = string.IsNullOrEmpty(model.BrandingBackgroundColor) ? "#0f172a" : model.BrandingBackgroundColor },
                new SiteSetting { CompanyId = company.Id, Key = "RoleSelectionEnabled", Value = model.RoleSelectionEnabled ? "true" : "false" }
            };

            if (model.RoleSelectionEnabled && model.Roles != null && model.Roles.Any())
            {
                settings.Add(new SiteSetting { CompanyId = company.Id, Key = "RoleSelectionTitle", Value = "Who are you?" });
                settings.Add(new SiteSetting { CompanyId = company.Id, Key = "RoleSelectionDescription", Value = "Select your role to get the most relevant matches during the event." });

                if (model.Roles.Count > 0)
                {
                    settings.Add(new SiteSetting { CompanyId = company.Id, Key = "RoleStudentName", Value = model.Roles[0].Name });
                    settings.Add(new SiteSetting { CompanyId = company.Id, Key = "RoleStudentDescription", Value = model.Roles[0].Description });
                }
                if (model.Roles.Count > 1)
                {
                    settings.Add(new SiteSetting { CompanyId = company.Id, Key = "RoleCompanyName", Value = model.Roles[1].Name });
                    settings.Add(new SiteSetting { CompanyId = company.Id, Key = "RoleCompanyDescription", Value = model.Roles[1].Description });
                }
            }

            var invitationTemplate = model.EmailTemplate;
            if (string.IsNullOrWhiteSpace(invitationTemplate))
            {
                invitationTemplate = @"
                    <div style='font-family: sans-serif; max-width: 600px; line-height: 1.6;'>
                        <p>Hi {{ParticipantName}}!</p>
                        <p>Join our AI-based matchmaking service to find the best connections at our event.</p>
                        <p>👉 <a href='{{MatchmakingLink}}' style='color: #6366f1; font-weight: bold; text-decoration: none;'>Yes, I want to find the right contacts via AI!</a></p>
                        <p>Best regards,<br>The Team</p>
                    </div>";
            }

            settings.Add(new SiteSetting { CompanyId = company.Id, Key = "InvitationEmailSubject", Value = "Maximize your networking - try our new AI matchmaking!" });
            settings.Add(new SiteSetting { CompanyId = company.Id, Key = "InvitationEmailTemplate", Value = invitationTemplate });

            // Result Email
            var resultTemplate = model.ResultEmailTemplate;
            if (string.IsNullOrWhiteSpace(resultTemplate))
            {
                resultTemplate = @"
                    <div style='font-family: sans-serif; max-width: 600px; line-height: 1.6;'>
                        <p>Hi {{ParticipantName}}!</p>
                        <p>Our AI has finished finding your best matches. Review them now and show interest to start booking meetings!</p>
                        <p>👉 <a href='{{ResultsTable}}' style='color: #6366f1; font-weight: bold; text-decoration: none;'>See my AI matches here</a></p>
                        <p>Warm regards,<br>The Team</p>
                    </div>";
            }

            settings.Add(new SiteSetting { CompanyId = company.Id, Key = "ResultEmailSubject", Value = "Your AI matches are here!" });
            settings.Add(new SiteSetting { CompanyId = company.Id, Key = "ResultEmailTemplate", Value = resultTemplate });

            // Feedback Email
            var feedbackTemplate = model.FeedbackEmailTemplate;
            if (string.IsNullOrWhiteSpace(feedbackTemplate))
            {
                feedbackTemplate = @"
                    <div style='font-family: sans-serif; line-height: 1.6;'>
                        <h2>Hi {{ParticipantName}}!</h2>
                        <p>We hope you had productive meetings!</p>
                        <p>We would love to hear your feedback. Please click the link below to see your results and leave feedback on your matches:</p>
                        <p style='margin: 20px 0;'>
                            <a href='{{ResultsLink}}' style='color: #6366f1; text-decoration: none; font-weight: bold;'>See results & leave feedback</a>
                        </p>
                        <p>Thank you!<br>The Team</p>
                    </div>";
            }

            settings.Add(new SiteSetting { CompanyId = company.Id, Key = "FeedbackEmailSubject", Value = "How was your matchmaking experience?" });
            settings.Add(new SiteSetting { CompanyId = company.Id, Key = "FeedbackEmailTemplate", Value = feedbackTemplate });

            _context.Settings.AddRange(settings);

            if (model.Questions != null && model.Questions.Any())
            {
                int order = 1;
                foreach (var q in model.Questions)
                {
                    var questionId = Guid.NewGuid().ToString("N").Substring(0, 8);
                    var question = new Question
                    {
                        Id = questionId,
                        CompanyId = company.Id,
                        Title = q.Title,
                        Description = "",
                        Type = q.Type,
                        IsHidden = q.IsHidden,
                        Order = order++,
                        Options = new List<QuestionOption>()
                    };

                    if (q.Options != null && q.Options.Any())
                    {
                        int optOrder = 1;
                        foreach (var opt in q.Options)
                        {
                            question.Options.Add(new QuestionOption
                            {
                                CompanyId = company.Id,
                                QuestionId = question.Id,
                                Title = opt,
                                Value = opt.ToLowerInvariant().Replace(" ", "_"),
                                Description = "",
                                Order = optOrder++,
                                Icon = GetOptionEmoji(opt)
                            });
                        }
                    }

                    _context.Questions.Add(question);
                }
            }

            await _context.SaveChangesAsync();

            var updatedToken = GenerateJwtToken(user);
            return Ok(new { message = "Event created successfully", token = updatedToken });
        }

        private string GetOptionEmoji(string title)
        {
            if (string.IsNullOrWhiteSpace(title)) return "🏷️";
            var lower = title.ToLowerInvariant();
            if (lower.Contains("fundraising") || lower.Contains("funding") || lower.Contains("capital")) return "💰";
            if (lower.Contains("network") || lower.Contains("social") || lower.Contains("connect")) return "🤝";
            if (lower.Contains("hire") || lower.Contains("hiring") || lower.Contains("job") || lower.Contains("talent") || lower.Contains("recruitment")) return "💼";
            if (lower.Contains("startup") || lower.Contains("launch") || lower.Contains("tech")) return "🚀";
            if (lower.Contains("sale") || lower.Contains("leads") || lower.Contains("client")) return "📈";
            if (lower.Contains("sustainable") || lower.Contains("green") || lower.Contains("circular")) return "🌱";
            if (lower.Contains("learn") || lower.Contains("student") || lower.Contains("education")) return "🎓";
            if (lower.Contains("mentor") || lower.Contains("advice") || lower.Contains("consult")) return "🧠";
            if (lower.Contains("invest") || lower.Contains("vc") || lower.Contains("angel")) return "📊";
            return "🏷️";
        }

        [HttpGet("companies")]
        public async Task<IActionResult> GetCompanies()
        {
            var userIdString = User.FindFirst("id")?.Value;
            if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out Guid userId))
            {
                return Unauthorized();
            }

            var user = await _context.AdminUsers.FindAsync(userId);
            if (user == null) return NotFound();

            if (user.Role == "SuperAdmin")
            {
                // SuperAdmin sees a list of Admin Accounts (Client Admins)
                // We filter out other SuperAdmins to focus on client accounts
                var adminAccounts = await _context.AdminUsers
                    .Where(u => u.Role == "Admin")
                    .OrderBy(u => u.Email)
                    .Select(u => new 
                    { 
                        u.Id,
                        Name = _context.Companies
                            .Where(c => c.Id == u.CompanyId)
                            .Select(c => c.Name)
                            .FirstOrDefault() ?? u.Email,
                        AdminEmail = u.Email,
                        AdminName = $"{u.FirstName} {u.LastName}",
                        AdminFirstName = u.FirstName,
                        AdminLastName = u.LastName,
                        AdminPassword = u.PasswordHash,
                        CompanyId = u.CompanyId,
                        IsVerified = u.IsVerified,
                        ParticipantCount = u.CompanyId.HasValue 
                            ? _context.Registrations.Count(r => r.CompanyId == u.CompanyId.Value) 
                            : 0,
                        HasMultipleEvents = u.CompanyId.HasValue && _context.Companies.Count(c => 
                            c.ParentId == (_context.Companies.Where(p => p.Id == u.CompanyId.Value).Select(p => p.ParentId).FirstOrDefault() ?? u.CompanyId.Value) ||
                            c.Id == (_context.Companies.Where(p => p.Id == u.CompanyId.Value).Select(p => p.ParentId).FirstOrDefault() ?? u.CompanyId.Value)
                        ) > 1
                    })
                    .ToListAsync();
                
                return Ok(adminAccounts);
            }
            else
            {
                // Regular admins see the companies/events they are linked to
                var companyIds = await _context.UserCompanies
                    .Where(uc => uc.UserId == userId)
                    .Select(uc => uc.CompanyId)
                    .ToListAsync();
                
                var companies = await _context.Companies
                    .Where(c => companyIds.Contains(c.Id))
                    .OrderBy(c => c.Name)
                    .Select(c => new 
                    { 
                        c.Id, 
                        c.Name, 
                        c.CreatedAt,
                        LogoUrl = _context.Settings
                            .Where(s => s.CompanyId == c.Id && s.Key == "WelcomeLogo")
                            .Select(s => s.Value)
                            .FirstOrDefault(),
                        ParticipantCount = _context.Registrations.Count(r => r.CompanyId == c.Id),
                        HasMultipleEvents = _context.Companies.Count(cc => 
                            cc.ParentId == (c.ParentId ?? c.Id) || 
                            cc.Id == (c.ParentId ?? c.Id)
                        ) > 1
                    })
                    .ToListAsync();
                
                return Ok(companies);
            }
        }

        [HttpGet("companies/{id}/events")]
        public async Task<IActionResult> GetEvents(Guid id)
        {
            // Verify permission
            var userIdString = User.FindFirst("id")?.Value;
            if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out Guid userId))
            {
                return Unauthorized();
            }

            var user = await _context.AdminUsers.FindAsync(userId);
            if (user == null) return NotFound();

            if (user.Role != "SuperAdmin")
            {
                var belongsToCompany = await _context.UserCompanies
                    .AnyAsync(uc => uc.UserId == userId && uc.CompanyId == id);
                
                if (!belongsToCompany) return Forbid();
            }

            var targetCompanyId = id;
            var company = await _context.Companies.FindAsync(id);
            if (company != null && company.ParentId != null)
            {
                // If this is an event, we want to find siblings, so use the parent ID
                targetCompanyId = company.ParentId.Value;
            }

            var events = await _context.Companies
                .Where(c => c.ParentId == targetCompanyId || c.Id == targetCompanyId)
                .OrderByDescending(c => c.CreatedAt)
                .Select(c => new 
                { 
                    c.Id, 
                    c.Name, 
                    c.CreatedAt,
                    IsParent = c.ParentId == null,
                    ParticipantCount = _context.Registrations.Count(r => r.CompanyId == c.Id)
                })
                .ToListAsync();

            return Ok(events);
        }

        [HttpPut("companies/{id}")]
        public async Task<IActionResult> UpdateCompany(Guid id, [FromBody] UpdateCompanyModel model)
        {
            if (string.IsNullOrWhiteSpace(model.Name))
            {
                return BadRequest("Company name cannot be empty");
            }

            var userIdString = User.FindFirst("id")?.Value;
            if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out Guid currentUserId))
            {
                return Unauthorized();
            }

            var currentUser = await _context.AdminUsers.FindAsync(currentUserId);
            if (currentUser == null) return NotFound("User not found");

            if (currentUser.Role != "SuperAdmin")
            {
                var isLinked = await _context.UserCompanies.AnyAsync(uc => uc.UserId == currentUserId && uc.CompanyId == id);
                if (!isLinked)
                {
                    return StatusCode(403, new { message = "You do not have permission to rename this event." });
                }
            }

            var company = await _context.Companies.FindAsync(id);
            if (company == null)
            {
                return NotFound("Company not found");
            }

            company.Name = model.Name;
            await _context.SaveChangesAsync();

            return Ok(new { message = "Company updated successfully" });
        }

        [HttpDelete("companies/{id}")]
        public async Task<IActionResult> DeleteCompany(Guid id)
        {
            var systemId = Guid.Parse("11111111-1111-1111-1111-111111111111");
            if (id == systemId) return BadRequest("Cannot delete system settings project.");

            var userIdString = User.FindFirst("id")?.Value;
            if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out Guid currentUserId))
            {
                return Unauthorized();
            }

            var currentUser = await _context.AdminUsers.FindAsync(currentUserId);
            if (currentUser == null) return NotFound("User not found");

            // Permission Check: SuperAdmin or linked to company
            if (currentUser.Role != "SuperAdmin")
            {
                var isLinked = await _context.UserCompanies.AnyAsync(uc => uc.UserId == currentUserId && uc.CompanyId == id);
                if (!isLinked)
                {
                    return StatusCode(403, new { message = "You do not have permission to delete this company." });
                }
            }

            var company = await _context.Companies.FindAsync(id);
            if (company == null) 
            {
                // Fallback: Check if this is an AdminUser ID (allows SuperAdmin to delete orphaned accounts)
                var targetUser = await _context.AdminUsers.FindAsync(id);
                if (targetUser != null && currentUser.Role == "SuperAdmin")
                {
                    _context.AdminUsers.Remove(targetUser);
                    await _context.SaveChangesAsync();
                    return Ok(new { message = "Administrator account removed successfully." });
                }
                return NotFound("Project or account not found.");
            }

            var participantCount = await _context.Registrations.CountAsync(r => r.CompanyId == id);
            if (participantCount > 0) return BadRequest("Cannot delete a project that has registered participants.");

            // Cascaded Deletion of data
            var settings = _context.Settings.Where(s => s.CompanyId == id);
            _context.Settings.RemoveRange(settings);

            var questions = _context.Questions.Where(q => q.CompanyId == id);
            _context.Questions.RemoveRange(questions);

            var options = _context.QuestionOptions.Where(o => o.CompanyId == id);
            _context.QuestionOptions.RemoveRange(options);

            // 1. Identify all users associated with this company
            // Check both the join table and the legacy CompanyId field
            var userIdsFromAssociations = await _context.UserCompanies
                .Where(uc => uc.CompanyId == id)
                .Select(uc => uc.UserId)
                .ToListAsync();
            
            var userIdsWithActiveContext = await _context.AdminUsers
                .Where(u => u.CompanyId == id)
                .Select(u => u.Id)
                .ToListAsync();

            var allAffectedUserIds = userIdsFromAssociations
                .Union(userIdsWithActiveContext)
                .Distinct()
                .ToList();

            // 2. Clean up associations
            var associations = _context.UserCompanies.Where(uc => uc.CompanyId == id);
            _context.UserCompanies.RemoveRange(associations);

            // 3. Clear active company context
            var usersToUpdate = await _context.AdminUsers.Where(u => u.CompanyId == id).ToListAsync();
            foreach (var u in usersToUpdate)
            {
                u.CompanyId = null;
            }

            // Save these changes so we can check remaining associations accurately
            await _context.SaveChangesAsync();

            // 4. Remove orphaned users (except the current user)
            foreach (var userId in allAffectedUserIds)
            {
                // Skip the user who is currently performing the deletion
                if (userId == currentUserId) continue;

                var user = await _context.AdminUsers
                    .Include(u => u.UserCompanies)
                    .FirstOrDefaultAsync(u => u.Id == userId);

                if (user != null)
                {
                    // If the user has no more companies linked via UserCompanies 
                    // AND no active CompanyId, they are orphaned.
                    var hasOtherCompanies = user.UserCompanies.Any() || (user.CompanyId.HasValue && user.CompanyId != id);
                    
                    if (!hasOtherCompanies)
                    {
                        _context.AdminUsers.Remove(user);
                    }
                }
            }

            _context.Companies.Remove(company);
            await _context.SaveChangesAsync();

            // Return a fresh token in case the active context was deleted
            var updatedToken = GenerateJwtToken(currentUser);
            return Ok(new { message = "Project deleted successfully", token = updatedToken });
        }

        [HttpPost("switch-company/{companyId}")]
        public async Task<IActionResult> SwitchCompany(Guid companyId)
        {
            var userIdString = User.FindFirst("id")?.Value;
            if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out Guid userId))
            {
                return Unauthorized();
            }

            var user = await _context.AdminUsers.FindAsync(userId);
            if (user == null) return NotFound();

            // Permission check: Must be SuperAdmin or belong to the company
            if (user.Role != "SuperAdmin")
            {
                var userCompanyIds = await _context.UserCompanies
                    .Where(uc => uc.UserId == userId)
                    .Select(uc => uc.CompanyId)
                    .ToListAsync();

                var targetCompany = await _context.Companies.FindAsync(companyId);
                if (targetCompany == null) return BadRequest("Company not found.");

                bool belongsToCompany = userCompanyIds.Contains(companyId);
                if (!belongsToCompany)
                {
                    foreach (var linkedId in userCompanyIds)
                    {
                        var linkedCompany = await _context.Companies.FindAsync(linkedId);
                        if (linkedCompany != null)
                        {
                            if (targetCompany.ParentId == linkedCompany.Id) { belongsToCompany = true; break; }
                            if (linkedCompany.ParentId == targetCompany.Id) { belongsToCompany = true; break; }
                            if (targetCompany.ParentId != null && targetCompany.ParentId == linkedCompany.ParentId) { belongsToCompany = true; break; }
                        }
                    }
                }

                if (!belongsToCompany)
                {
                    return StatusCode(403, new { message = "You do not have permission to manage this company." });
                }
            }

            var companyExists = await _context.Companies.AnyAsync(c => c.Id == companyId);
            if (!companyExists) return BadRequest("Company not found.");

            // Update user's current company (sticky session)
            user.CompanyId = companyId;
            await _context.SaveChangesAsync();

            var token = GenerateJwtToken(user);

            return Ok(new { token });
        }

        [HttpGet("me")]
        public async Task<IActionResult> GetMe()
        {
            var userIdString = User.FindFirst("id")?.Value;
            if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out Guid userId))
            {
                return Unauthorized();
            }

            var user = await _context.AdminUsers
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null) return NotFound();

            // Direct check for CompanyName since navigation properties can sometimes be tricky after an update/switch
            var companyName = "No Company";
            string? projectAdminEmail = null;

            if (user.CompanyId.HasValue)
            {
                var company = await _context.Companies.FindAsync(user.CompanyId.Value);
                if (company != null)
                {
                    companyName = company.Name;
                    
                    // If SuperAdmin, try to find the actual project admin info
                    if (user.Role == "SuperAdmin")
                    {
                        var projectAdmin = await _context.AdminUsers
                            .Where(u => u.CompanyId == user.CompanyId.Value && u.Role == "Admin")
                            .OrderBy(u => u.Id)
                            .Select(u => u.Email)
                            .FirstOrDefaultAsync();
                        
                        projectAdminEmail = projectAdmin;
                    }
                }
            }

            return Ok(new
            {
                user.Id,
                user.Username,
                user.FirstName,
                user.LastName,
                user.Role,
                CompanyId = user.CompanyId,
                ParentId = user.CompanyId.HasValue 
                    ? _context.Companies.Where(c => c.Id == user.CompanyId.Value).Select(c => c.ParentId).FirstOrDefault() 
                    : null,
                CompanyName = companyName,
                ProjectAdminEmail = projectAdminEmail,
                CurrentPassword = user.Role == "SuperAdmin"
                    ? _context.AdminUsers
                        .Where(u => u.CompanyId == user.CompanyId && u.Role == "Admin")
                        .OrderBy(u => u.Id)
                        .Select(u => u.PasswordHash)
                        .FirstOrDefault()
                    : user.PasswordHash
            });
        }

    }

    public class LoginModel
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public class UpdatePasswordModel
    {
        public string CurrentPassword { get; set; } = string.Empty;
        public string NewPassword { get; set; } = string.Empty;
    }

    public class SetupRequestModel
    {
        public string ProjectName { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public Guid? ParentId { get; set; }
        public SuperAdminModel SuperAdmin { get; set; } = new();
    }

    public class SuperAdminModel
    {
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public class UpdateCompanyModel
    {
        public string Name { get; set; } = string.Empty;
    }

    public class ForgotPasswordModel
    {
        public string Email { get; set; } = string.Empty;
    }

    public class ResetPasswordModel
    {
        public string Email { get; set; } = string.Empty;
        public string Token { get; set; } = string.Empty;
        public string NewPassword { get; set; } = string.Empty;
    }
}
