using System.ComponentModel.DataAnnotations;

namespace NackaMatchmaking.API.Models
{
    public class UserAnswer
    {
        public int Id { get; set; }
        public Guid CompanyId { get; set; }
        public Guid ParticipantId { get; set; }
        public Participant Participant { get; set; } = null!;
        public string QuestionId { get; set; } = null!;
        public Question Question { get; set; } = null!;
        public string AnswerValue { get; set; } = null!; // Comma separated for multi-choice
        public string? OtherValue { get; set; }
    }
}
