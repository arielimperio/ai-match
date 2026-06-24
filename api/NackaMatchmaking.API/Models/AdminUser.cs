using System.ComponentModel.DataAnnotations.Schema;

namespace NackaMatchmaking.API.Models
{
    public class AdminUser
    {
        public Guid Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        
        public Guid? CompanyId { get; set; }
        public Company? Company { get; set; }
        public ICollection<UserCompany> UserCompanies { get; set; } = new List<UserCompany>();
        public string Role { get; set; } = "Admin";
        public bool IsVerified { get; set; } = false;
        public string? VerificationToken { get; set; }

        public string? PasswordResetToken { get; set; }
        public DateTime? PasswordResetTokenExpiry { get; set; }

        /// <summary>
        /// Not persisted — used only to surface the plain-text password in API responses (SuperAdmin only).
        /// </summary>
        [NotMapped]
        public string? PlainPassword { get; set; }
    }
}
