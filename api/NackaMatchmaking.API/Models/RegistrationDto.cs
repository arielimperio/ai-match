using System;

namespace NackaMatchmaking.API.Models
{
    public class RegistrationDto
    {
        public string? Firstname { get; set; }
        public string? Lastname { get; set; }
        public string? Organization { get; set; }
        public string? Title { get; set; }
        public string? Email { get; set; }
        public bool? HasAcceptedTerms { get; set; }

        public string? Bio { get; set; }
        public string? Photo { get; set; }
        public string? Superpower { get; set; }
        public string? SuperpowerOther { get; set; }
        public string? Challenge { get; set; }
        public string? ChallengeOther { get; set; }
        public string? Topics { get; set; }
        public string? TopicsOther { get; set; }

        // Dynamic answers: QuestionId -> AnswerValue
        public Dictionary<string, string>? Answers { get; set; }
        // Dynamic "Other" values: QuestionId -> OtherText
        public Dictionary<string, string>? OtherAnswers { get; set; }
    }
}
