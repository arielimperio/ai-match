using System;

namespace NackaMatchmaking.API.Models
{
    public class MeetingSlot
    {
        public Guid Id { get; set; }

        public Guid MatchmakingEventId { get; set; }
        public MatchmakingEvent? MatchmakingEvent { get; set; }

        public Guid CompanyParticipantId { get; set; }
        public Participant? CompanyParticipant { get; set; }

        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }

        public bool IsAvailable { get; set; } = true;

        public Guid? AssignedStudentId { get; set; }
        public Participant? AssignedStudent { get; set; }

        public bool StudentCheckedIn { get; set; }
        public bool StudentDeclined { get; set; }
        public bool CompanyMarkedNoShow { get; set; }
    }
}
