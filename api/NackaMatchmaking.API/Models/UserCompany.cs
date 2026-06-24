using System;

namespace NackaMatchmaking.API.Models
{
    public class UserCompany
    {
        public Guid UserId { get; set; }
        public AdminUser? User { get; set; }

        public Guid CompanyId { get; set; }
        public Company? Company { get; set; }

        public string Role { get; set; } = "Admin";
        public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
    }
}
