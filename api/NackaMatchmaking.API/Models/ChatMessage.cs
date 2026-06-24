using System;

namespace NackaMatchmaking.API.Models
{
    public class ChatMessage
    {
        public Guid Id { get; set; }

        public Guid MatchId { get; set; }
        // public UserMatch Match { get; set; } // Optional navigation property

        public Guid SenderId { get; set; }
        public string Content { get; set; } = null!;
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}
