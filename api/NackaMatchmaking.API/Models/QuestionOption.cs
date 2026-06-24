using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace NackaMatchmaking.API.Models
{
    public class QuestionOption
    {
        public int Id { get; set; }
        
        public Guid CompanyId { get; set; }
        public string QuestionId { get; set; } = null!;
        [JsonIgnore]
        public Question? Question { get; set; }

        public string Value { get; set; } = null!; // e.g., "sales", "tech"
        public string Icon { get; set; } = null!; // Emoji or icon code
        public string Title { get; set; } = null!;
        public string Description { get; set; } = null!;
        public int Order { get; set; }
        public bool IsHidden { get; set; } = false;
    }
}
