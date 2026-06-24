using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using NackaMatchmaking.API.Data;
using NackaMatchmaking.API.Models;

namespace NackaMatchmaking.API.Controllers
{
    [Route("api/administrators")]
    [ApiController]
    [Authorize]
    public class ManagementAdminController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public ManagementAdminController(ApplicationDbContext context)
        {
            _context = context;
        }


        [HttpPost("fetch")]
        public async Task<IActionResult> GetAdmins([FromQuery] Guid? companyId)
        {
            var userIdString = User.FindFirst("id")?.Value;
            if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out Guid currentUserId))
            {
                return Unauthorized();
            }

            var currentUser = await _context.AdminUsers
                .Include(u => u.UserCompanies)
                .FirstOrDefaultAsync(u => u.Id == currentUserId);

            if (currentUser == null) return NotFound();

            // If no companyId provided, try user's single CompanyId, then first associated company
            var effectiveCompanyId = companyId ?? currentUser.CompanyId;
            
            if (effectiveCompanyId == null && currentUser.Role != "SuperAdmin")
            {
                var firstAssoc = currentUser.UserCompanies.FirstOrDefault();
                if (firstAssoc != null)
                {
                    effectiveCompanyId = firstAssoc.CompanyId;
                }
            }

            if (effectiveCompanyId == null && currentUser.Role != "SuperAdmin")
            {
                return BadRequest("No company context found.");
            }

            // Permission Check: Must be SuperAdmin or belong to the company
            if (currentUser.Role != "SuperAdmin")
            {
                var belongsToCompany = await _context.UserCompanies
                    .AnyAsync(uc => uc.UserId == currentUserId && uc.CompanyId == effectiveCompanyId);
                
                if (!belongsToCompany)
                {
                    return StatusCode(403, new { message = "You do not have permission to view admins for this company." });
                }
            }

            // Query admins for the company (including those linked via single FK and join table)
            var query = _context.AdminUsers.AsQueryable();
            
            if (effectiveCompanyId.HasValue)
            {
                // Find all users linked to this company via UserCompanies
                var userIdsFromAssoc = _context.UserCompanies
                    .Where(uc => uc.CompanyId == effectiveCompanyId.Value)
                    .Select(uc => uc.UserId);
                
                query = query.Where(u => u.CompanyId == effectiveCompanyId.Value || userIdsFromAssoc.Contains(u.Id));
            }

            // Filter out system-wide SuperAdmins and the current user to keep the list clean
            query = query.Where(u => u.Role != "SuperAdmin" && u.Id != currentUserId);

            var admins = await query
                .OrderBy(u => u.FirstName)
                .ThenBy(u => u.LastName)
                .Select(u => new AdminUserManagementDto
                {
                    Id = u.Id,
                    Username = u.Username,
                    FirstName = u.FirstName,
                    LastName = u.LastName,
                    Email = u.Email,
                    Role = u.Role,
                    IsVerified = u.IsVerified,
                    IsDeactivated = !u.IsVerified && u.VerificationToken == null
                })
                .ToListAsync();

            return Ok(admins);
        }

        [HttpPost("add")]
        public async Task<IActionResult> AddAdmin([FromBody] AdminCreationModel model)
        {
            var userIdString = User.FindFirst("id")?.Value;
            if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out Guid currentUserId))
            {
                return Unauthorized();
            }

            var currentUser = await _context.AdminUsers.FindAsync(currentUserId);
            if (currentUser == null) return NotFound();

            // If no companyId provided, try user's single CompanyId, then first associated company
            var targetCompanyId = model.CompanyId ?? currentUser.CompanyId;
            
            if (targetCompanyId == null && currentUser.Role != "SuperAdmin")
            {
                var firstAssoc = await _context.UserCompanies
                    .Where(uc => uc.UserId == currentUserId)
                    .FirstOrDefaultAsync();
                
                if (firstAssoc != null)
                {
                    targetCompanyId = firstAssoc.CompanyId;
                }
            }

            if (targetCompanyId == null) return BadRequest("Company context is required.");

            // Permission Check: Must be SuperAdmin or belong to the company
            if (currentUser.Role != "SuperAdmin")
            {
                var belongsToCompany = await _context.UserCompanies
                    .AnyAsync(uc => uc.UserId == currentUserId && uc.CompanyId == targetCompanyId);
                
                if (!belongsToCompany)
                {
                    return StatusCode(403, new { message = "You do not have permission to add admins to this company." });
                }

                // Project admins cannot create SuperAdmins
                if (model.Role == "SuperAdmin")
                {
                    return StatusCode(403, new { message = "Only SuperAdmins can create other SuperAdmins." });
                }
            }

            // Check if user already exists
            var user = await _context.AdminUsers.FirstOrDefaultAsync(u => u.Email.ToLower() == model.Email.ToLower());
            
            if (user == null)
            {
                // Create new user
                user = new AdminUser
                {
                    Id = Guid.NewGuid(),
                    Username = model.Email.ToLower(),
                    Email = model.Email.ToLower(),
                    FirstName = model.FirstName,
                    LastName = model.LastName,
                    Role = model.Role ?? "Admin",
                    CompanyId = targetCompanyId,
                    PasswordHash = !string.IsNullOrEmpty(model.Password) ? model.Password : "InitialPassword123!",
                    IsVerified = true
                };
                _context.AdminUsers.Add(user);
            }
            else
            {
                // Verify if already linked
                var isAlreadyLinked = await _context.UserCompanies
                    .AnyAsync(uc => uc.UserId == user.Id && uc.CompanyId == targetCompanyId);
                
                if (isAlreadyLinked)
                {
                    return BadRequest("User is already an admin for this company.");
                }
            }

            // Link user to company
            var userCompany = new UserCompany
            {
                UserId = user.Id,
                CompanyId = targetCompanyId.Value,
                Role = model.Role ?? "Admin",
                JoinedAt = DateTime.UtcNow
            };
            _context.UserCompanies.Add(userCompany);

            await _context.SaveChangesAsync();

            return Ok(new AdminUserManagementDto
            {
                Id = user.Id,
                Username = user.Username,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Email = user.Email,
                Role = user.Role,
                IsVerified = user.IsVerified
            });
        }

        [HttpPut("update/{id}")]
        public async Task<IActionResult> UpdateAdmin(Guid id, [FromBody] AdminUpdateModel model)
        {
            var userIdString = User.FindFirst("id")?.Value;
            if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out Guid currentUserId))
            {
                return Unauthorized();
            }

            var currentUser = await _context.AdminUsers.FindAsync(currentUserId);
            if (currentUser == null) return NotFound();

            var userToUpdate = await _context.AdminUsers.FindAsync(id);
            if (userToUpdate == null) return NotFound("User not found.");

            // Determine company context (sticky from user or provided in model if available)
            var targetCompanyId = model.CompanyId ?? currentUser.CompanyId;
            
            if (targetCompanyId == null && currentUser.Role != "SuperAdmin")
            {
                // Try to find the company this target user belongs to that the current user ALSO belongs to
                var commonCompany = await _context.UserCompanies
                    .Where(uc => uc.UserId == id)
                    .Join(_context.UserCompanies.Where(cuc => cuc.UserId == currentUserId),
                          tu => tu.CompanyId,
                          cu => cu.CompanyId,
                          (tu, cu) => tu.CompanyId)
                    .FirstOrDefaultAsync();
                
                if (commonCompany != Guid.Empty)
                {
                    targetCompanyId = commonCompany;
                }
            }

            // Permission Check
            if (currentUser.Role != "SuperAdmin")
            {
                if (targetCompanyId == null) return BadRequest("Context required for permission check.");

                var belongsToCompany = await _context.UserCompanies
                    .AnyAsync(uc => uc.UserId == currentUserId && uc.CompanyId == targetCompanyId);
                
                if (!belongsToCompany)
                {
                    return StatusCode(403, new { message = "You do not have permission to manage admins for this company." });
                }

                // Check if the user being updated is also in this company
                var userInCompany = await _context.UserCompanies
                    .AnyAsync(uc => uc.UserId == id && uc.CompanyId == targetCompanyId);
                
                if (!userInCompany)
                {
                    return StatusCode(403, new { message = "User is not part of your company context." });
                }

                if (model.Role == "SuperAdmin")
                {
                    return StatusCode(403, new { message = "Only SuperAdmins can grant SuperAdmin role." });
                }
            }

            // Update details
            userToUpdate.FirstName = model.FirstName;
            userToUpdate.LastName = model.LastName;
            userToUpdate.Role = model.Role ?? userToUpdate.Role;

            // Email update - only allowed for SuperAdmins
            if (!string.IsNullOrEmpty(model.Email) && currentUser.Role == "SuperAdmin")
            {
                var emailExists = await _context.AdminUsers.AnyAsync(u => u.Email.ToLower() == model.Email.ToLower() && u.Id != id);
                if (emailExists)
                {
                    return BadRequest("Email is already in use by another account.");
                }
                userToUpdate.Email = model.Email.ToLower();
                userToUpdate.Username = model.Email.ToLower();
            }

            // Password update - only allowed for SuperAdmins
            if (!string.IsNullOrEmpty(model.Password) && currentUser.Role == "SuperAdmin")
            {
                userToUpdate.PasswordHash = model.Password;
            }

            // If role changed, update it in the join table as well for this specific company
            if (targetCompanyId.HasValue && model.Role != null)
            {
                var uc = await _context.UserCompanies
                    .FirstOrDefaultAsync(uc => uc.UserId == id && uc.CompanyId == targetCompanyId.Value);
                if (uc != null)
                {
                    uc.Role = model.Role;
                }
            }

            await _context.SaveChangesAsync();

            return Ok(new AdminUserManagementDto
            {
                Id = userToUpdate.Id,
                Username = userToUpdate.Username,
                FirstName = userToUpdate.FirstName,
                LastName = userToUpdate.LastName,
                Email = userToUpdate.Email,
                Role = userToUpdate.Role,
                IsVerified = userToUpdate.IsVerified
            });
        }

        [HttpDelete("delete/{id}")]
        public async Task<IActionResult> DeleteAdmin(Guid id, [FromQuery] Guid? companyId)
        {
            var userIdString = User.FindFirst("id")?.Value;
            if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out Guid currentUserId))
            {
                return Unauthorized();
            }

            var currentUser = await _context.AdminUsers.FindAsync(currentUserId);
            if (currentUser == null) return NotFound();

            var targetCompanyId = companyId ?? currentUser.CompanyId;
            
            if (targetCompanyId == null && currentUser.Role != "SuperAdmin")
            {
                // Fallback to first common company
                var commonCompany = await _context.UserCompanies
                    .Where(uc => uc.UserId == id)
                    .Join(_context.UserCompanies.Where(cuc => cuc.UserId == currentUserId),
                          tu => tu.CompanyId,
                          cu => cu.CompanyId,
                          (tu, cu) => tu.CompanyId)
                    .FirstOrDefaultAsync();
                
                if (commonCompany != Guid.Empty)
                {
                    targetCompanyId = commonCompany;
                }
            }

            if (targetCompanyId == null) return BadRequest("Company context required.");

            // Prevent self-deletion from project access
            if (id == currentUserId)
            {
                return BadRequest("You cannot remove yourself from admin access.");
            }

            // Permission Check
            if (currentUser.Role != "SuperAdmin")
            {
                var belongsToCompany = await _context.UserCompanies
                    .AnyAsync(uc => uc.UserId == currentUserId && uc.CompanyId == targetCompanyId);
                
                if (!belongsToCompany)
                {
                    return StatusCode(403, new { message = "You do not have permission to manage admins for this company." });
                }
            }

            // Remove association
            var association = await _context.UserCompanies
                .FirstOrDefaultAsync(uc => uc.UserId == id && uc.CompanyId == targetCompanyId);
            
            if (association != null)
            {
                _context.UserCompanies.Remove(association);
                await _context.SaveChangesAsync(); // Save first to check remaining associations
            }

            // Check if we should delete the user account entirely
            var userToDelete = await _context.AdminUsers
                .Include(u => u.UserCompanies)
                .FirstOrDefaultAsync(u => u.Id == id);

            if (userToDelete != null)
            {
                // Clear active context if it was this company
                if (userToDelete.CompanyId == targetCompanyId)
                {
                    userToDelete.CompanyId = null;
                }

                // If no more projects linked and not a SuperAdmin, delete the account
                var hasOtherProjects = userToDelete.UserCompanies.Any();
                if (!hasOtherProjects && userToDelete.Role != "SuperAdmin")
                {
                    _context.AdminUsers.Remove(userToDelete);
                    await _context.SaveChangesAsync();
                    return Ok(new { message = "Admin account deleted successfully." });
                }
                
                await _context.SaveChangesAsync();
            }

            return Ok(new { message = "Admin removed from project successfully." });
        }
    }

    public class AdminUserManagementDto
    {
        public Guid Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Role { get; set; } = "Admin";
        public bool IsVerified { get; set; }
        public bool IsDeactivated { get; set; }
    }

    public class AdminCreationModel
    {
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? Role { get; set; }
        public string? Password { get; set; }
        public Guid? CompanyId { get; set; }
    }

    public class AdminUpdateModel
    {
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string? Role { get; set; }
        public string? Password { get; set; }
        public Guid? CompanyId { get; set; }
    }
}
