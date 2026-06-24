using System;

namespace NackaMatchmaking.API.Models
{
    public class ParticipantSummaryDto
    {
        public Guid Id { get; set; }
        public Guid RegistrationId { get; set; }
        public string Name { get; set; } = null!;
        public string Company { get; set; } = null!;
        public string Superpower { get; set; } = null!;
        public string Status { get; set; } = null!; // "KLAR" or "PÅBÖRJAD"
        public int MatchCount { get; set; }
        public int InterestCount { get; set; }
        public int RequestedCount { get; set; }
        public int BookedCount { get; set; }
        public DateTime? MatchedAt { get; set; }
        public DateTime LastActive { get; set; }
        public string? Role { get; set; }
    }
}
