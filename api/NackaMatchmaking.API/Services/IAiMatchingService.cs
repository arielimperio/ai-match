using NackaMatchmaking.API.Models;

namespace NackaMatchmaking.API.Services
{
    public interface IAiMatchingService
    {
        Task<List<AiMatchSuggestion>> GetSuggestionsAsync(List<Participant> participants, System.Threading.CancellationToken ct = default);
        Task<List<AiMatchSuggestion>> GetSuggestionsForSingleAsync(Participant source, List<Participant> candidates, System.Threading.CancellationToken ct = default);
    }

    public class AiMatchSuggestion
    {
        public Guid User1Id { get; set; }
        public Guid User2Id { get; set; }
        public int Score { get; set; }
        public string Reasoning { get; set; } = string.Empty;
    }
}
