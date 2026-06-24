using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NackaMatchmaking.API.Models
{
    public class UserMatch
    {
        public Guid Id { get; set; }

        public Guid User1Id { get; set; }
        public Participant User1 { get; set; } = null!;

        public Guid User2Id { get; set; }
        public Participant User2 { get; set; } = null!;

        public bool User1Interested { get; set; }
        public bool User2Interested { get; set; }
        public int? User1Feedback { get; set; } // 1 for 👍, -1 for 👎
        public string? User1FeedbackReason { get; set; }
        public int? User2Feedback { get; set; }
        public string? User2FeedbackReason { get; set; }

        public int Score { get; set; } // Match score 0-100
        public MatchStatus Status { get; set; } = MatchStatus.Pending;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? MatchedAt { get; set; }
    }

    public enum MatchStatus
    {
        Pending,
        Matched,
        Proposed, // Generated but not yet interacted with
        Rejected // Optional
    }
}
