using System;

namespace NackaMatchmaking.API.Models
{
    public class Participant
    {
        public Guid Id { get; set; }
        public Guid CompanyId { get; set; }
        public Company? Company { get; set; }
        public string? Email { get; set; }
        public string? Title { get; set; }
        public bool HasAcceptedTerms { get; set; }

        public string? Firstname { get; set; }
        public string? Lastname { get; set; }
        public string? Organization { get; set; }

        public string? Bio { get; set; }
        public string? Photo { get; set; }
        public string? Superpower { get; set; }
        public string? SuperpowerOther { get; set; }
        public string? Challenge { get; set; }
        public string? ChallengeOther { get; set; }
        public string? Topics { get; set; }
        public string? TopicsOther { get; set; }
        public string? CompanyDescription { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public int? EventRating { get; set; }
        public string? EventComment { get; set; }

        /// <summary>Set when the admin sends a feedback request email to this participant.</summary>
        public DateTime? FeedbackRequestSentAt { get; set; }
    }
}
