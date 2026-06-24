using System;
using System.Collections.Generic;

namespace NackaMatchmaking.API.Models
{
    public class Company
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public Guid? ParentId { get; set; }
        public Company? Parent { get; set; }
        public ICollection<Company> SubEvents { get; set; } = new List<Company>();

        // Navigation property for related AdminUsers
        public ICollection<AdminUser> AdminUsers { get; set; } = new List<AdminUser>();
        public ICollection<UserCompany> UserCompanies { get; set; } = new List<UserCompany>();
    }
}
