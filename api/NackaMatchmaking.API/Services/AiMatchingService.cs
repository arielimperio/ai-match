using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NackaMatchmaking.API.Data;
using NackaMatchmaking.API.Models;

namespace NackaMatchmaking.API.Services
{
    public class AiMatchingService : IAiMatchingService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<AiMatchingService> _logger;
        private readonly ApplicationDbContext _context;
        private readonly TaskProgressService _progressService;

        public AiMatchingService(HttpClient httpClient, IConfiguration configuration, ILogger<AiMatchingService> logger, ApplicationDbContext context, TaskProgressService progressService)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _logger = logger;
            _context = context;
            _progressService = progressService;
        }

        public async Task<List<AiMatchSuggestion>> GetSuggestionsAsync(List<Participant> participants, System.Threading.CancellationToken ct = default)
        {
            var companyId = participants.FirstOrDefault()?.CompanyId ?? Guid.Empty;
            var taskKey = companyId != Guid.Empty ? "matching_" + companyId : "matching";

            var systemId = Guid.Parse("11111111-1111-1111-1111-111111111111");
            var dbApiKey = await _context.Settings.FindAsync(companyId, "AiApiKey") ?? await _context.Settings.FindAsync(systemId, "AiApiKey");
            var dbModel = await _context.Settings.FindAsync(companyId, "AiModel") ?? await _context.Settings.FindAsync(systemId, "AiModel");
            var dbProvider = await _context.Settings.FindAsync(companyId, "AiProvider") ?? await _context.Settings.FindAsync(systemId, "AiProvider");

            var apiKey = dbApiKey?.Value ?? _configuration["AiSettings:ApiKey"];
            var model = dbModel?.Value ?? _configuration["AiSettings:Model"] ?? "gpt-4o";
            var provider = dbProvider?.Value ?? _configuration["AiSettings:Provider"] ?? "OpenAI";
            
            _logger.LogInformation($"[AiMatchingService] Starting optimized AI matching for {participants.Count} participants in Company {companyId}...");
            _progressService.UpdateProgress(taskKey, 5);

            if (string.IsNullOrEmpty(apiKey) || participants.Count < 2)
            {
                return new List<AiMatchSuggestion>();
            }

            // 1. Prepare Enrichment Data
            var questions = await _context.Questions.Where(q => q.CompanyId == companyId).ToDictionaryAsync(q => q.Id, q => q.Title);
            var participantIds = participants.Select(p => p.Id).ToList();
            var allAnswers = await _context.UserAnswers
                .Where(ua => participantIds.Contains(ua.ParticipantId))
                .ToListAsync();

            var enrichedProfiles = participants.Select(p => {
                var pAnswers = allAnswers.Where(ua => ua.ParticipantId == p.Id)
                    .Select(ua => new { 
                        Id = ua.QuestionId, 
                        Title = questions.ContainsKey(ua.QuestionId) ? questions[ua.QuestionId] : ua.QuestionId,
                        Value = string.IsNullOrEmpty(ua.OtherValue) ? ua.AnswerValue : $"{ua.AnswerValue} (Annat: {ua.OtherValue})"
                    }).ToList();

                return new
                {
                    p.Id,
                    Name = $"{p.Firstname} {p.Lastname}",
                    p.Title,
                    p.Organization,
                    Profile = pAnswers
                };
            }).ToList();

            // 2. Local Pre-filtering
            _progressService.UpdateProgress(taskKey, 10);
            var candidatePairs = new List<object>();
            var seenPairs = new HashSet<string>();
            const int topKPerPerson = 10;

            for (int i = 0; i < enrichedProfiles.Count; i++)
            {
                var a = enrichedProfiles[i];
                var roleA = a.Profile.FirstOrDefault(x => x.Id == "system_role")?.Value;

                var candidates = enrichedProfiles
                    .Where((b, idx) => idx != i)
                    .Where(b => 
                    {
                        var roleB = b.Profile.FirstOrDefault(x => x.Id == "system_role")?.Value;
                        if (!string.IsNullOrEmpty(roleA) && !string.IsNullOrEmpty(roleB) && roleA == roleB) return false;
                        return true;
                    })
                    .Select(b => new { 
                        Profile = b, 
                        LocalScore = CalculateLocalScore(
                            a.Title ?? "", a.Organization ?? "", a.Profile.ToDictionary(x => x.Id, x => x.Value), 
                            b.Title ?? "", b.Organization ?? "", b.Profile.ToDictionary(x => x.Id, x => x.Value)) 
                    })
                    .OrderByDescending(x => x.LocalScore)
                    .Take(topKPerPerson);

                foreach (var c in candidates)
                {
                    var id1 = a.Id;
                    var id2 = c.Profile.Id;
                    var key = id1.CompareTo(id2) < 0 ? $"{id1}|{id2}" : $"{id2}|{id1}";
                    
                    if (seenPairs.Add(key))
                    {
                        candidatePairs.Add(new { person1 = a, person2 = c.Profile });
                    }
                }
            }

            _logger.LogInformation($"[AiMatchingService] Pre-filtered down to {candidatePairs.Count} candidate pairs. Processing in batches...");

            // 3. Batch AI Processing
            const int batchSize = 25;
            var allSuggestions = new List<AiMatchSuggestion>();
            var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            int totalBatches = (int)Math.Ceiling((double)candidatePairs.Count / batchSize);

            for (int i = 0; i < candidatePairs.Count; i += batchSize)
            {
                ct.ThrowIfCancellationRequested();
                var batch = candidatePairs.Skip(i).Take(batchSize).ToList();
                var suggestions = await ScoreBatchAsync(batch, provider, model, apiKey, jsonOptions, ct);
                allSuggestions.AddRange(suggestions);

                // Report progress: from 10% to 95%
                int currentBatchNum = (i / batchSize) + 1;
                double batchProgress = 10 + ((double)currentBatchNum / totalBatches) * 85;
                _progressService.UpdateProgress(taskKey, batchProgress);
            }

            _logger.LogInformation($"[AiMatchingService] Completed. Found {allSuggestions.Count} AI-validated matches.");
            return allSuggestions;
        }

        public async Task<List<AiMatchSuggestion>> GetSuggestionsForSingleAsync(Participant source, List<Participant> candidates, System.Threading.CancellationToken ct = default)
        {
            var companyId = source.CompanyId;
            var taskKey = "matching_single_" + source.Id;

            var systemId = Guid.Parse("11111111-1111-1111-1111-111111111111");
            var dbApiKey = await _context.Settings.FindAsync(companyId, "AiApiKey") ?? await _context.Settings.FindAsync(systemId, "AiApiKey");
            var dbModel = await _context.Settings.FindAsync(companyId, "AiModel") ?? await _context.Settings.FindAsync(systemId, "AiModel");
            var dbProvider = await _context.Settings.FindAsync(companyId, "AiProvider") ?? await _context.Settings.FindAsync(systemId, "AiProvider");

            var apiKey = dbApiKey?.Value ?? _configuration["AiSettings:ApiKey"];
            var model = dbModel?.Value ?? _configuration["AiSettings:Model"] ?? "gpt-4o";
            var provider = dbProvider?.Value ?? _configuration["AiSettings:Provider"] ?? "OpenAI";
            
            _logger.LogInformation($"[AiMatchingService] Starting AI matching for single participant {source.Id} against {candidates.Count} candidates...");

            if (string.IsNullOrEmpty(apiKey) || candidates.Count == 0)
            {
                return new List<AiMatchSuggestion>();
            }

            // 1. Prepare Enrichment Data
            var questions = await _context.Questions.Where(q => q.CompanyId == companyId).ToDictionaryAsync(q => q.Id, q => q.Title);
            
            var allParticipantIds = candidates.Select(c => c.Id).ToList();
            allParticipantIds.Add(source.Id);
            
            var allAnswers = await _context.UserAnswers
                .Where(ua => allParticipantIds.Contains(ua.ParticipantId))
                .ToListAsync();

            var enrichedProfiles = allParticipantIds.Select(id => {
                var p = id == source.Id ? source : candidates.First(c => c.Id == id);
                var pAnswers = allAnswers.Where(ua => ua.ParticipantId == id)
                    .Select(ua => new { 
                        Id = ua.QuestionId, 
                        Title = questions.ContainsKey(ua.QuestionId) ? questions[ua.QuestionId] : ua.QuestionId,
                        Value = string.IsNullOrEmpty(ua.OtherValue) ? ua.AnswerValue : $"{ua.AnswerValue} (Annat: {ua.OtherValue})"
                    }).ToList();

                return new
                {
                    p.Id,
                    Name = $"{p.Firstname} {p.Lastname}",
                    p.Title,
                    p.Organization,
                    Profile = pAnswers
                };
            }).ToList();

            var a = enrichedProfiles.First(p => p.Id == source.Id);
            var otherProfiles = enrichedProfiles.Where(p => p.Id != source.Id).ToList();

            var roleA = a.Profile.FirstOrDefault(x => x.Id == "system_role")?.Value;

            var validOtherProfiles = otherProfiles.Where(b => 
            {
                var roleB = b.Profile.FirstOrDefault(x => x.Id == "system_role")?.Value;
                if (!string.IsNullOrEmpty(roleA) && !string.IsNullOrEmpty(roleB) && roleA == roleB) return false;
                return true;
            }).ToList();

            // 2. Local Pre-filtering
            const int topKPerPerson = 20;

            var topCandidates = validOtherProfiles
                .Select(b => new { 
                    Profile = b, 
                    LocalScore = CalculateLocalScore(
                        a.Title ?? "", a.Organization ?? "", a.Profile.ToDictionary(x => x.Id, x => x.Value), 
                        b.Title ?? "", b.Organization ?? "", b.Profile.ToDictionary(x => x.Id, x => x.Value)) 
                })
                .OrderByDescending(x => x.LocalScore)
                .Take(topKPerPerson);

            var candidatePairs = new List<object>();
            foreach (var c in topCandidates)
            {
                candidatePairs.Add(new { person1 = a, person2 = c.Profile });
            }

            // 3. Batch AI Processing
            const int batchSize = 25;
            var allSuggestions = new List<AiMatchSuggestion>();
            var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            
            for (int i = 0; i < candidatePairs.Count; i += batchSize)
            {
                ct.ThrowIfCancellationRequested();
                var batch = candidatePairs.Skip(i).Take(batchSize).ToList();
                var suggestions = await ScoreBatchAsync(batch, provider, model, apiKey, jsonOptions, ct);
                allSuggestions.AddRange(suggestions);
            }

            _logger.LogInformation($"[AiMatchingService] Single AI match completed. Found {allSuggestions.Count} AI-validated matches.");
            return allSuggestions;
        }

        private int CalculateLocalScore(string titleA, string orgA, Dictionary<string, string> profileA, string titleB, string orgB, Dictionary<string, string> profileB)
        {
            int score = 0;
            var delimiters = new[] { ' ', ',', ';', '/', '.' };

            // 1. Title & Org overlap (Professional synergy) - Weight 2
            var pA = ($"{titleA} {orgA}").ToLowerInvariant().Split(delimiters, StringSplitOptions.RemoveEmptyEntries).ToHashSet();
            var pB = ($"{titleB} {orgB}").ToLowerInvariant().Split(delimiters, StringSplitOptions.RemoveEmptyEntries);
            score += pB.Count(w => pA.Contains(w)) * 2;

            // 2. Profile answers overlap with weighted Question IDs
            var weights = new Dictionary<string, int>
            {
                { "q1", 5 }, // Superpower
                { "q2", 5 }, // Utmaning
                { "q5", 4 }, // Företagsbeskrivning
                { "q4", 3 }, // Bio
                { "q3", 1 }  // Topics
            };

            foreach (var qId in profileA.Keys)
            {
                if (profileB.TryGetValue(qId, out var valB))
                {
                    int weight = weights.GetValueOrDefault(qId, 1);
                    var wordsA = profileA[qId].ToLowerInvariant().Split(delimiters, StringSplitOptions.RemoveEmptyEntries).ToHashSet();
                    var wordsB = valB.ToLowerInvariant().Split(delimiters, StringSplitOptions.RemoveEmptyEntries);
                    
                    score += wordsB.Count(w => wordsA.Contains(w)) * weight;
                }
            }
            return score;
        }

        private async Task<List<AiMatchSuggestion>> ScoreBatchAsync(List<object> batch, string provider, string model, string apiKey, JsonSerializerOptions jsonOptions, System.Threading.CancellationToken ct)
        {
            var prompt = @"
You are a professional business matchmaker. Analyze the following pairs of participants.
Score each pair 1-100 based on networking synergy, complementary skills, or shared strategic interests.

Return a JSON object with a 'matches' key containing an array of results.
Format:
{
  ""matches"": [
    {
      ""User1Id"": ""guid"",
      ""User2Id"": ""guid"",
      ""Score"": 85,
      ""Reasoning"": ""Explain briefly why they should meet based on their profiles.""
    }
  ]
}
Only return pairs with Score >= 25.
Pairs:
" + JsonSerializer.Serialize(batch);

            string contentString = "";

            try
            {
                if (provider.Equals("Gemini", StringComparison.OrdinalIgnoreCase))
                {
                    var requestBody = new
                    {
                        systemInstruction = new { parts = new[] { new { text = "You are a matchmaking expert. Output valid JSON only." } } },
                        contents = new[] { new { role = "user", parts = new[] { new { text = prompt } } } },
                        generationConfig = new { responseMimeType = "application/json" }
                    };

                    // Format model name correctly by removing "models/" if user accidentally prefixes it, 
                    // or ensuring the endpoint accepts the raw string via fallback handling.
                    var formattedModel = model.StartsWith("models/") ? model : $"models/{model}";

                    var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
                    using var request = new HttpRequestMessage(HttpMethod.Post, $"https://generativelanguage.googleapis.com/v1beta/{formattedModel}:generateContent?key={apiKey}");
                    request.Content = content;

                    var response = await _httpClient.SendAsync(request, ct);
                    if (!response.IsSuccessStatusCode)
                    {
                        _logger.LogError($"[AiMatchingService] Gemini API Error: {response.StatusCode} - {await response.Content.ReadAsStringAsync()}");
                        return new List<AiMatchSuggestion>();
                    }

                    var jsonResponse = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(jsonResponse);
                    var candidates = doc.RootElement.GetProperty("candidates");
                    if (candidates.GetArrayLength() > 0)
                    {
                        contentString = candidates[0].GetProperty("content").GetProperty("parts")[0].GetProperty("text").GetString() ?? "";
                    }
                }
                else
                {
                    // OpenAI
                    var requestBody = new
                    {
                        model = model,
                        messages = new[]
                        {
                            new { role = "system", content = "You are a matchmaking expert. Output valid JSON only." },
                            new { role = "user", content = prompt }
                        },
                        response_format = new { type = "json_object" }
                    };

                    var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
                    
                    using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions");
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
                    request.Content = content;

                    var response = await _httpClient.SendAsync(request, ct);
                    if (!response.IsSuccessStatusCode)
                    {
                        _logger.LogError($"[AiMatchingService] OpenAI API Error: {response.StatusCode} - {await response.Content.ReadAsStringAsync()}");
                        return new List<AiMatchSuggestion>();
                    }

                    var jsonResponse = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(jsonResponse);
                    contentString = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "";
                }

                if (string.IsNullOrEmpty(contentString)) return new List<AiMatchSuggestion>();

                // Clean up markdown serialization if present
                contentString = contentString.Trim();
                if (contentString.StartsWith("```json", StringComparison.OrdinalIgnoreCase)) contentString = contentString.Substring(7);
                else if (contentString.StartsWith("```")) contentString = contentString.Substring(3);
                if (contentString.EndsWith("```")) contentString = contentString.Substring(0, contentString.Length - 3);
                contentString = contentString.Trim();

                var resultDoc = JsonDocument.Parse(contentString);
                if (resultDoc.RootElement.TryGetProperty("matches", out var matchesEl))
                {
                    var rawMatches = JsonSerializer.Deserialize<List<RawAiMatchItem>>(matchesEl.GetRawText(), jsonOptions) ?? new List<RawAiMatchItem>();
                    var results = new List<AiMatchSuggestion>();
                    
                    foreach (var raw in rawMatches)
                    {
                        if (Guid.TryParse(raw.User1Id, out var g1) && Guid.TryParse(raw.User2Id, out var g2))
                        {
                            // Normalize: ensure g1 is always the smaller GUID
                            var u1 = g1.CompareTo(g2) < 0 ? g1 : g2;
                            var u2 = g1.CompareTo(g2) < 0 ? g2 : g1;

                            // Deduplicate within the batch results
                            if (!results.Any(r => r.User1Id == u1 && r.User2Id == u2))
                            {
                                results.Add(new AiMatchSuggestion
                                {
                                    User1Id = u1,
                                    User2Id = u2,
                                    Score = raw.Score,
                                    Reasoning = raw.Reasoning
                                });
                            }
                        }
                    }
                    return results;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Batch scoring failed");
            }

            return new List<AiMatchSuggestion>();
        }

        private class RawAiMatchItem
        {
            public string User1Id { get; set; } = string.Empty;
            public string User2Id { get; set; } = string.Empty;
            public int Score { get; set; }
            public string Reasoning { get; set; } = string.Empty;
        }
    }
}
