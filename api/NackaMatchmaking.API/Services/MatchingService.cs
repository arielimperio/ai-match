using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NackaMatchmaking.API.Data;
using NackaMatchmaking.API.Models;

namespace NackaMatchmaking.API.Services
{
    public class MatchingService
    {
        private readonly ApplicationDbContext _context;
        private readonly IAiMatchingService _aiMatchingService;
        private readonly TaskProgressService _progressService;
        private readonly ILogger<MatchingService> _logger;

        public MatchingService(ApplicationDbContext context, IAiMatchingService aiMatchingService, TaskProgressService progressService, ILogger<MatchingService> logger)
        {
            _context = context;
            _aiMatchingService = aiMatchingService;
            _progressService = progressService;
            _logger = logger;
        }

        public async Task<List<MatchResult>> GetMatchesForParticipant(Guid participantId)
        {
            var source = await _context.Participants.FindAsync(participantId);
            if (source == null) return new List<MatchResult>();

            // Only fetch persisted matches from DB
            var dbMatches = await _context.Matches
                .Include(m => m.User1)
                .Include(m => m.User2)
                .Where(m => m.User1Id == participantId || m.User2Id == participantId)
                .ToListAsync();

            var results = new List<MatchResult>();

            foreach (var m in dbMatches)
            {
                var isUser1 = m.User1Id == participantId;
                var partner = isUser1 ? m.User2 : m.User1;
                
                if (partner == null) continue;

                results.Add(new MatchResult
                {
                    Participant = partner,
                    Score = m.Score,
                    IsInterested = isUser1 ? m.User1Interested : m.User2Interested,
                    IsMutual = m.Status == MatchStatus.Matched,
                    MatchId = m.Id,
                    Feedback = isUser1 ? m.User1Feedback ?? 0 : m.User2Feedback ?? 0,
                    FeedbackReason = isUser1 ? m.User1FeedbackReason : m.User2FeedbackReason
                });
            }

            // Enforce cross-role matching on the read side to hide any existing invalid matches
            var participantRole = await _context.UserAnswers
                .Where(ua => ua.ParticipantId == participantId && ua.QuestionId == "system_role")
                .Select(ua => ua.AnswerValue)
                .FirstOrDefaultAsync();

            if (!string.IsNullOrEmpty(participantRole))
            {
                var targetRole = participantRole == "Student" ? "Company" : "Student";
                var validPartnerIds = await _context.UserAnswers
                    .Where(ua => ua.QuestionId == "system_role" && ua.AnswerValue == targetRole)
                    .Select(ua => ua.ParticipantId)
                    .ToHashSetAsync();

                results = results.Where(r => validPartnerIds.Contains(r.Participant.Id)).ToList();
            }

            return results.OrderByDescending(r => r.Score).ToList();
        }

        public async Task GenerateMatchesForAll(Guid companyId, System.Threading.CancellationToken ct = default)
        {
            var taskKey = "matching_" + companyId;
            _progressService.UpdateProgress(taskKey, 0);
            var participants = await _context.Participants.Where(p => p.CompanyId == companyId).ToListAsync();
            _progressService.UpdateProgress(taskKey, 2);
            var participantIds = participants.Select(p => p.Id).ToList();
            var existingMatchesList = await _context.Matches.Where(m => participantIds.Contains(m.User1Id) || participantIds.Contains(m.User2Id)).ToListAsync();
            _progressService.UpdateProgress(taskKey, 5);
            
            // 1. Get AI Suggestions (reports progress to 95%)
            var aiSuggestions = await _aiMatchingService.GetSuggestionsAsync(participants, ct);
            _progressService.UpdateProgress(taskKey, 96);
            
            // 2. Pre-index existing matches and AI suggestions
            var existingMatchesLookup = existingMatchesList
                .GroupBy(m => {
                    var u1 = m.User1Id.CompareTo(m.User2Id) < 0 ? m.User1Id : m.User2Id;
                    var u2 = m.User1Id.CompareTo(m.User2Id) < 0 ? m.User2Id : m.User1Id;
                    return (u1, u2);
                })
                .ToDictionary(g => g.Key, g => g.First());

            var aiSuggestionsLookup = aiSuggestions
                .GroupBy(s => {
                    var u1 = s.User1Id.CompareTo(s.User2Id) < 0 ? s.User1Id : s.User2Id;
                    var u2 = s.User1Id.CompareTo(s.User2Id) < 0 ? s.User2Id : s.User1Id;
                    return (u1, u2);
                })
                .ToDictionary(g => g.Key, g => g.First().Score);

            // 3. Calculate ALL potential scores O(N^2)
            var roles = await _context.UserAnswers
                .Where(ua => ua.CompanyId == companyId && ua.QuestionId == "system_role")
                .ToDictionaryAsync(ua => ua.ParticipantId, ua => ua.AnswerValue);

            var candidates = new List<CandidateMatch>();
            for (int i = 0; i < participants.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                var p1 = participants[i];
                roles.TryGetValue(p1.Id, out var role1);

                for (int j = i + 1; j < participants.Count; j++)
                {
                    var p2 = participants[j];
                    roles.TryGetValue(p2.Id, out var role2);

                    if (!string.IsNullOrEmpty(role1) && !string.IsNullOrEmpty(role2) && role1 == role2) continue;
                    var u1Id = p1.Id.CompareTo(p2.Id) < 0 ? p1.Id : p2.Id;
                    var u2Id = p1.Id.CompareTo(p2.Id) < 0 ? p2.Id : p1.Id;

                    var ruleScore = CalculateMatchScore(p1, p2);
                    aiSuggestionsLookup.TryGetValue((u1Id, u2Id), out var aiScore);
                    
                    var finalScore = Math.Min(Math.Max(ruleScore, aiScore), 100);
                    if (finalScore > 0)
                    {
                        candidates.Add(new CandidateMatch { U1Id = u1Id, U2Id = u2Id, Score = finalScore });
                    }
                }
            }


            // 5. Update/Add matches only if they are in the top-3 set
            var newMatches = new List<UserMatch>();
            bool hasChanges = false;

            foreach (var c in candidates)
            {

                if (existingMatchesLookup.TryGetValue((c.U1Id, c.U2Id), out var existing))
                {
                    if (existing.Score != c.Score)
                    {
                        existing.Score = c.Score;
                        hasChanges = true;
                    }
                }
                else
                {
                    newMatches.Add(new UserMatch
                    {
                        Id = Guid.NewGuid(),
                        User1Id = c.U1Id,
                        User2Id = c.U2Id,
                        Score = c.Score,
                        Status = MatchStatus.Proposed,
                        CreatedAt = DateTime.UtcNow,
                        User1Interested = false,
                        User2Interested = false
                    });
                    hasChanges = true;
                }
            }

            if (newMatches.Any())
            {
                await _context.Matches.AddRangeAsync(newMatches);
            }

            // 6. Cleanup: Remove Proposed matches that are no longer in top 3 for either person
            var matchesToRemove = existingMatchesList
                .Where(m => m.Status == MatchStatus.Proposed && !candidates.Any(c => (m.User1Id == c.U1Id && m.User2Id == c.U2Id) || (m.User1Id == c.U2Id && m.User2Id == c.U1Id)))
                .ToList();
            
            if (matchesToRemove.Any())
            {
                _context.Matches.RemoveRange(matchesToRemove);
                hasChanges = true;
            }

            if (hasChanges)
            {
                await _context.SaveChangesAsync();
            }
            _progressService.UpdateProgress(taskKey, 100);
        }

        private class CandidateMatch
        {
            public Guid U1Id { get; set; }
            public Guid U2Id { get; set; }
            public int Score { get; set; }
        }

        private int CalculateMatchScore(Participant source, Participant candidate)
        {
            int score = 0;
            var delimiters = new[] { ' ', ',', ';', '/', '.', '!' };

            // 1. Complementary Matching (My Superpower solves your Challenge)
            if (!string.IsNullOrEmpty(source.Superpower) && !string.IsNullOrEmpty(candidate.Challenge))
            {
                var powers = source.Superpower.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(p => p.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);
                if (powers.Contains(candidate.Challenge))
                {
                    score += 40;
                }
            }
            
            // Reverse: You offer what I need
            if (!string.IsNullOrEmpty(source.Challenge) && !string.IsNullOrEmpty(candidate.Superpower))
            {
                var powers = candidate.Superpower.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(p => p.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);
                if (powers.Contains(source.Challenge))
                {
                    score += 40;
                }
            }

            // 2. Shared Interests (Topics)
            if (!string.IsNullOrEmpty(source.Topics) && !string.IsNullOrEmpty(candidate.Topics))
            {
                var sourceTopics = source.Topics.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(t => t.Trim())
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
                var candidateTopics = candidate.Topics.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(t => t.Trim());

                foreach (var topic in candidateTopics)
                {
                    if (sourceTopics.Contains(topic))
                    {
                        score += 15;
                    }
                }
            }

            // 3. Company Description Overlap (Keyword context)
            if (!string.IsNullOrEmpty(source.CompanyDescription) && !string.IsNullOrEmpty(candidate.CompanyDescription))
            {
                var wordsA = source.CompanyDescription.ToLowerInvariant().Split(delimiters, StringSplitOptions.RemoveEmptyEntries).ToHashSet();
                var wordsB = candidate.CompanyDescription.ToLowerInvariant().Split(delimiters, StringSplitOptions.RemoveEmptyEntries);
                
                // Add 5 points per overlapping keyword
                score += wordsB.Count(w => wordsA.Contains(w) && w.Length > 2) * 5;
            }

            score += 10; 

            return Math.Min(score, 100);
        }

        public async Task GenerateMatchesForSingleParticipant(Guid participantId)
        {
            var source = await _context.Participants.FindAsync(participantId);
            if (source == null) return;

            var sourceRole = await _context.UserAnswers
                .Where(ua => ua.ParticipantId == participantId && ua.QuestionId == "system_role")
                .Select(ua => ua.AnswerValue)
                .FirstOrDefaultAsync();

            var participantsQuery = _context.Participants
                .Where(p => p.CompanyId == source.CompanyId && p.Id != participantId);

            if (!string.IsNullOrEmpty(sourceRole))
            {
                var targetRole = sourceRole == "Student" ? "Company" : "Student";
                var targetParticipantIds = await _context.UserAnswers
                    .Where(ua => ua.CompanyId == source.CompanyId && ua.QuestionId == "system_role" && ua.AnswerValue == targetRole)
                    .Select(ua => ua.ParticipantId)
                    .ToListAsync();
                    
                participantsQuery = participantsQuery.Where(p => targetParticipantIds.Contains(p.Id));
            }

            var participants = await participantsQuery.ToListAsync();

            var existingMatches = await _context.Matches
                .Where(m => m.User1Id == participantId || m.User2Id == participantId)
                .ToListAsync();

            var existingLookup = existingMatches.ToDictionary(m => {
                var u1 = m.User1Id.CompareTo(m.User2Id) < 0 ? m.User1Id : m.User2Id;
                var u2 = m.User1Id.CompareTo(m.User2Id) < 0 ? m.User2Id : m.User1Id;
                return (u1, u2);
            });

            // Use AI Matching Service instead of rule-based for a single participant
            var aiSuggestions = await _aiMatchingService.GetSuggestionsForSingleAsync(source, participants);

            // Fallback: if AI returns nothing (no API key, or all scored too low), use local rule-based scoring
            if (!aiSuggestions.Any() && participants.Any())
            {
                _logger.LogWarning("[MatchingService] AI returned 0 suggestions for {ParticipantId}. Falling back to rule-based scoring.", participantId);
                aiSuggestions = participants.Select(partner =>
                {
                    var u1Id = participantId.CompareTo(partner.Id) < 0 ? participantId : partner.Id;
                    var u2Id = participantId.CompareTo(partner.Id) < 0 ? partner.Id : participantId;
                    return new AiMatchSuggestion
                    {
                        User1Id = u1Id,
                        User2Id = u2Id,
                        Score = CalculateMatchScore(source, partner),
                        Reasoning = "Score calculated by local matching rules."
                    };
                })
                .Where(s => s.Score > 0)
                .OrderByDescending(s => s.Score)
                .Take(10)
                .ToList();
            }

            var newMatches = new List<UserMatch>();
            bool hasChanges = false;

            foreach (var aiScore in aiSuggestions)
            {
                var u1Id = aiScore.User1Id;
                var u2Id = aiScore.User2Id;
                var score = aiScore.Score;

                if (existingLookup.TryGetValue((u1Id, u2Id), out var existing))
                {
                    if (existing.Score != score)
                    {
                        existing.Score = score;
                        hasChanges = true;
                    }
                }
                else
                {
                    newMatches.Add(new UserMatch
                    {
                        Id = Guid.NewGuid(),
                        User1Id = u1Id,
                        User2Id = u2Id,
                        Score = score,
                        Status = MatchStatus.Proposed,
                        CreatedAt = DateTime.UtcNow,
                        User1Interested = false,
                        User2Interested = false
                    });
                    hasChanges = true;
                }
            }

            if (newMatches.Any())
            {
                await _context.Matches.AddRangeAsync(newMatches);
            }
            if (hasChanges || newMatches.Any()) 
            {
                await _context.SaveChangesAsync();
            }
        }
    }

    public class MatchResult
    {
        public Participant Participant { get; set; } = null!;
        public int Score { get; set; }
        public bool IsInterested { get; set; }
        public bool IsMutual { get; set; }
        public Guid MatchId { get; set; }
        public int Feedback { get; set; }
        public string? FeedbackReason { get; set; }
    }
}
