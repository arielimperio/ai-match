using System;

namespace NackaMatchmaking.API.Models
{
    public class Registration
    {
        public Guid Id { get; set; }
        public Guid CompanyId { get; set; }
        public Company? Company { get; set; }
        public string? Firstname { get; set; }
        public string? Lastname { get; set; }
        public string? Organization { get; set; }
        public string? Title { get; set; }
        public bool HasAcceptedTerms { get; set; }
        public string? Email { get; set; }
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
