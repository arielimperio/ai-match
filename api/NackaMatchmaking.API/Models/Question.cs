using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace NackaMatchmaking.API.Models
{
    public class Question
    {
        public Guid CompanyId { get; set; }
        public Company? Company { get; set; }

        public string Id { get; set; } = null!; // e.g., "q1", "q2"
        public string Title { get; set; } = null!;
        public string Description { get; set; } = null!;
        public string? Placeholder { get; set; } // For text questions like q4
        public int? MaxLength { get; set; } // Added for character limits
        public bool IsHidden { get; set; } // new property
        public int Order { get; set; } // Added for custom sorting
        public string Type { get; set; } = "Choice"; // Choice, MultipleChoice, Text, Profile
        public string TargetRole { get; set; } = "All"; // All, Student, Company
        
        public ICollection<QuestionOption> Options { get; set; } = new List<QuestionOption>();
    }
}
