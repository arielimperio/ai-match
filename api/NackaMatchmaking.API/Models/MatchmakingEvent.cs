using System;
using System.Collections.Generic;

namespace NackaMatchmaking.API.Models
{
    public class MatchmakingEvent
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public bool IsActive { get; set; } = true;

        public TimeSpan? EventStartTime { get; set; }
        public TimeSpan? EventEndTime { get; set; }
        public int SlotDurationMinutes { get; set; } = 30;
        public TimeSpan? BreakStartTime { get; set; }
        public TimeSpan? BreakEndTime { get; set; }

        public Guid CompanyId { get; set; }
        public Company? Company { get; set; }
    }
}
