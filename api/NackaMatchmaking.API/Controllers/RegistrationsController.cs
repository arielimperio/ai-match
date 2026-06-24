using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NackaMatchmaking.API.Data;
using NackaMatchmaking.API.Models;

namespace NackaMatchmaking.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Microsoft.AspNetCore.Authorization.Authorize]
    public class RegistrationsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly NackaMatchmaking.API.Services.IEmailService _emailService;
        private readonly IConfiguration _configuration;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<RegistrationsController> _logger;

        public RegistrationsController(ApplicationDbContext context, NackaMatchmaking.API.Services.IEmailService emailService, IConfiguration configuration, IServiceScopeFactory scopeFactory, ILogger<RegistrationsController> logger)
        {
            _context = context;
            _emailService = emailService;
            _configuration = configuration;
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Registration>>> GetRegistrations()
        {
            var companyIdString = User.FindFirst("companyId")?.Value;
            if (string.IsNullOrEmpty(companyIdString) || !Guid.TryParse(companyIdString, out Guid companyId)) return Unauthorized();

            return await _context.Registrations
                .Where(r => r.CompanyId == companyId)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();
        }

        [HttpGet("export")]
        public async Task<ActionResult<IEnumerable<object>>> ExportRegistrations()
        {
            var companyIdString = User.FindFirst("companyId")?.Value;
            if (string.IsNullOrEmpty(companyIdString) || !Guid.TryParse(companyIdString, out Guid companyId)) return Unauthorized();

            var registrations = await _context.Registrations.Where(r => r.CompanyId == companyId).ToListAsync();
            var participants = await _context.Participants.Where(p => p.CompanyId == companyId).ToListAsync();
            var participantIds = participants.Select(p => p.Id).ToList();
            
            // Fetch mutual matches to sync status logic with dashboard
            var mutualMatches = await _context.Matches
                .Where(m => m.Status == MatchStatus.Matched && (participantIds.Contains(m.User1Id) && participantIds.Contains(m.User2Id)))
                .ToListAsync();

            var mutualMatchDict = new Dictionary<Guid, int>();
            foreach (var m in mutualMatches)
            {
                mutualMatchDict[m.User1Id] = mutualMatchDict.GetValueOrDefault(m.User1Id) + 1;
                mutualMatchDict[m.User2Id] = mutualMatchDict.GetValueOrDefault(m.User2Id) + 1;
            }

            var exportData = registrations.Select(r => 
            {
                var p = participants.FirstOrDefault(p => p.Email == r.Email);
                int matchCount = p != null ? mutualMatchDict.GetValueOrDefault(p.Id) : 0;
                bool surveyComplete = p != null && !string.IsNullOrEmpty(p.Superpower) && !string.IsNullOrEmpty(p.Challenge) && !string.IsNullOrEmpty(p.Topics);

                return new 
                {
                    Id = p?.Id ?? Guid.Empty,
                    Name = $"{r.Firstname} {r.Lastname}",
                    Email = r.Email,
                    Company = r.Organization,
                    Title = r.Title,
                    Phone = "", // Not in model currently
                    Superpower = p?.Superpower ?? "",
                    Interests = p?.Topics ?? "",
                    Bio = p?.Bio ?? "",
                    Challenge = p?.Challenge ?? "",
                    CompanyDescription = p?.CompanyDescription ?? "",
                    Status = p == null ? "Not started" : (surveyComplete && matchCount > 0) ? "Matched" : "Answered",
                    Registered = r.CreatedAt.ToString("yyyy-MM-dd HH:mm"),
                    Answered = p?.CreatedAt.ToString("yyyy-MM-dd HH:mm") ?? ""
                };
            });

            return Ok(exportData);
        }

        // GET: api/Registrations/participant/{id}/matches
        [HttpGet("participant/{id}/matches")]
        [Microsoft.AspNetCore.Authorization.AllowAnonymous]
        public async Task<ActionResult<object>> GetMatches(Guid id, [FromServices] NackaMatchmaking.API.Services.MatchingService matchingService)
        {
            var matches = await matchingService.GetMatchesForParticipant(id);
            bool hasAnswered = await _context.UserAnswers.AnyAsync(ua => ua.ParticipantId == id && ua.QuestionId != "system_role");

            if (matches == null || !matches.Any())
            {
                if (hasAnswered)
                {
                    await matchingService.GenerateMatchesForSingleParticipant(id);
                    matches = await matchingService.GetMatchesForParticipant(id);
                }
            }

            // Always include mutual matches; apply score filter only to non-mutual ones
            var filteredMatches = matches
                .Where(m => m.IsMutual || m.Score >= 25)
                .ToList();

            // If we have no qualifying matches at all, try to (re)generate — covers the case
            // where a previous AI run saved nothing or all scores were below threshold.
            if (!filteredMatches.Any() && hasAnswered)
            {
                await matchingService.GenerateMatchesForSingleParticipant(id);
                matches = await matchingService.GetMatchesForParticipant(id);
                filteredMatches = matches
                    .Where(m => m.IsMutual || m.Score >= 25)
                    .ToList();
            }

            if (!filteredMatches.Any() && matches.Any())
            {
                // Last resort fallback: show top 5 even if they are below threshold
                filteredMatches = matches.OrderByDescending(m => m.Score).Take(5).ToList();
            }
            
            // Map to simplified DTO for frontend
            var result = filteredMatches.Select(m => new 
            {
                id = m.Participant.Id,
                name = $"{m.Participant.Firstname} {m.Participant.Lastname}",
                title = m.Participant.Title ?? "Deltagare",
                company = m.Participant.Organization,
                img = m.Participant.Photo,
                percentage = m.Score,
                description = !string.IsNullOrEmpty(m.Participant.Bio) ? $"\"{m.Participant.Bio}\"" : "",
                superpower = m.Participant.Superpower,
                topics = m.Participant.Topics,
                isInterested = m.IsInterested,
                isMutual = m.IsMutual,
                matchId = m.MatchId,
                feedback = m.Feedback,
                feedbackReason = m.FeedbackReason,
                companyDescription = m.Participant.CompanyDescription,
                bio = m.Participant.Bio
            });

            return Ok(result);
        }

        // GET: api/Registrations/{id}
        [HttpGet("{id}")]
        [Microsoft.AspNetCore.Authorization.AllowAnonymous]
        public async Task<ActionResult<Registration>> GetRegistration(Guid id)
        {
            var registration = await _context.Registrations.FindAsync(id);

            if (registration == null)
            {
                return NotFound();
            }

            return registration;
        }

        [HttpPost]
        [Microsoft.AspNetCore.Authorization.AllowAnonymous]
        public async Task<ActionResult<object>> PostRegistration([FromQuery] Guid companyId, [FromBody] RegistrationDto dto)
        {
            if (companyId == Guid.Empty) return BadRequest("companyId is required");

            // 0. Ensure registration exists (allow anyone to register even if not in pre-imported list)
            var registration = await _context.Registrations.FirstOrDefaultAsync(r => r.CompanyId == companyId && r.Email == dto.Email);
            if (registration == null)
            {
                registration = new Registration
                {
                    Id = Guid.NewGuid(),
                    CompanyId = companyId,
                    Email = dto.Email,
                    Firstname = dto.Firstname,
                    Lastname = dto.Lastname,
                    Organization = dto.Organization,
                    Title = dto.Title,
                    HasAcceptedTerms = dto.HasAcceptedTerms ?? false,
                    CreatedAt = DateTime.UtcNow
                };
                _context.Registrations.Add(registration);
            }
            else
            {
                // Update registration record to keep in sync with profile updates
                registration.Firstname = dto.Firstname ?? registration.Firstname;
                registration.Lastname = dto.Lastname ?? registration.Lastname;
                registration.Organization = dto.Organization ?? registration.Organization;
                registration.Title = dto.Title ?? registration.Title;
            }

            // 1. Create or update participant profile (Matchmaking Profile)
            var participant = await _context.Participants
                .FirstOrDefaultAsync(p => p.CompanyId == companyId && p.Email == dto.Email);

            if (participant == null)
            {
                participant = new Participant
                {
                    CompanyId = companyId,
                    Email = dto.Email,
                    Title = dto.Title,
                    Firstname = dto.Firstname,
                    Lastname = dto.Lastname,
                    Organization = dto.Organization,
                    Bio = dto.Bio,
                    Photo = dto.Photo,
                    HasAcceptedTerms = dto.HasAcceptedTerms ?? false,
                    // Legacy values from DTO if provided
                    Superpower = dto.Superpower,
                    SuperpowerOther = dto.SuperpowerOther,
                    Challenge = dto.Challenge,
                    ChallengeOther = dto.ChallengeOther,
                    Topics = dto.Topics,
                    TopicsOther = dto.TopicsOther,
                    CreatedAt = DateTime.UtcNow
                };
                _context.Participants.Add(participant);
                
                // Save changes initially so we have the participant ID
                await _context.SaveChangesAsync();
            }
            else
            {
                // Update Participant's profile data
                participant.Title = dto.Title ?? participant.Title;
                participant.Firstname = dto.Firstname ?? participant.Firstname;
                participant.Lastname = dto.Lastname ?? participant.Lastname;
                participant.Organization = dto.Organization ?? participant.Organization;
                participant.Bio = dto.Bio;
                participant.Photo = dto.Photo;
                
                // Legacy values from DTO if provided
                if (dto.Superpower != null) participant.Superpower = dto.Superpower;
                if (dto.SuperpowerOther != null) participant.SuperpowerOther = dto.SuperpowerOther;
                if (dto.Challenge != null) participant.Challenge = dto.Challenge;
                if (dto.ChallengeOther != null) participant.ChallengeOther = dto.ChallengeOther;
                if (dto.Topics != null) participant.Topics = dto.Topics;
                if (dto.TopicsOther != null) participant.TopicsOther = dto.TopicsOther;
            }

            // --- Handle Dynamic Answers ---
            if (dto.Answers != null)
            {
                // Ensure system_role question exists if it's being submitted to prevent FK violation
                if (dto.Answers.ContainsKey("system_role"))
                {
                    var roleQ = await _context.Questions.FindAsync(companyId, "system_role");
                    if (roleQ == null)
                    {
                        _context.Questions.Add(new Question 
                        { 
                            CompanyId = companyId, 
                            Id = "system_role", 
                            Title = "System Role", 
                            Description = "System generated role",
                            Type = "System", 
                            IsHidden = true,
                            Order = 0 
                        });
                        await _context.SaveChangesAsync();
                    }
                }

                // 1. Clear existing dynamic answers for this participant
                var existingAnswers = await _context.UserAnswers
                    .Where(ua => ua.ParticipantId == participant.Id)
                    .ToListAsync();
                _context.UserAnswers.RemoveRange(existingAnswers);

                // 2. Add new ones
                foreach (var kvp in dto.Answers)
                {
                    var questionId = kvp.Key;
                    var answerValue = kvp.Value;
                    var otherValue = dto.OtherAnswers?.ContainsKey(questionId) == true 
                        ? dto.OtherAnswers[questionId] 
                        : null;

                    _context.UserAnswers.Add(new UserAnswer
                    {
                        CompanyId = companyId,
                        ParticipantId = participant.Id,
                        QuestionId = questionId,
                        AnswerValue = answerValue,
                        OtherValue = otherValue
                    });

                    // Sync to legacy columns for AI matching compatibility
                    if (questionId == "q1") participant.Superpower = answerValue;
                    if (questionId == "q1") participant.SuperpowerOther = otherValue;
                    if (questionId == "q2") participant.Challenge = answerValue;
                    if (questionId == "q2") participant.ChallengeOther = otherValue;
                    if (questionId == "q3") participant.Topics = answerValue;
                    if (questionId == "q3") participant.TopicsOther = otherValue;
                    if (questionId == "q4") participant.Bio = answerValue;
                    if (questionId == "q5") participant.CompanyDescription = answerValue;
                }
            }

            await _context.SaveChangesAsync();

            // Return Participant ID so frontend can use it for subsequent calls
            return Ok(new { id = participant.Id, participantId = participant.Id, email = participant.Email });
        }

        [HttpGet("summaries")]
        public async Task<ActionResult<object>> GetSummaries([FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string search = "", [FromQuery] string status = "", [FromQuery] string role = "")
        {
            var companyIdString = User.FindFirst("companyId")?.Value;
            if (string.IsNullOrEmpty(companyIdString) || !Guid.TryParse(companyIdString, out Guid companyId)) return Unauthorized();

            // 1. Initial Query joining Registrations with Participants and counting mutual matches
            var query = from r in _context.Registrations.Where(r => r.CompanyId == companyId)
                        join p in _context.Participants.Where(p => p.CompanyId == companyId) on r.Email equals p.Email into pJoin
                        from p in pJoin.DefaultIfEmpty()
                        select new {
                            Registration = r,
                            Participant = p,
                            MutualMatchCount = p == null ? 0 : _context.Matches.Count(m => m.Status == MatchStatus.Matched && (m.User1Id == p.Id || m.User2Id == p.Id)),
                            ProposedMatchCount = p == null ? 0 : _context.Matches.Count(m => (m.User1Id == p.Id || m.User2Id == p.Id) && m.Score >= 25),
                            RequestedCount = p == null ? 0 : _context.Matches.Count(m => (m.User1Id == p.Id && m.User1Interested) || (m.User2Id == p.Id && m.User2Interested)),
                            LatestMatchedAt = p == null ? (DateTime?)null : _context.Matches
                                .Where(m => m.Status == MatchStatus.Matched && (m.User1Id == p.Id || m.User2Id == p.Id))
                                .Select(m => (DateTime?)m.MatchedAt)
                                .Max(),
                            // Count real survey answers (excluding system-generated ones)
                            HasSurveyAnswers = p == null ? false : _context.UserAnswers.Any(ua => ua.ParticipantId == p.Id && ua.QuestionId != "system_role"),
                            Role = p == null ? null : _context.UserAnswers.Where(ua => ua.ParticipantId == p.Id && ua.QuestionId == "system_role").Select(ua => ua.AnswerValue).FirstOrDefault()
                        };

            // 2. Apply Search
            if (!string.IsNullOrEmpty(search))
            {
                var s = search.ToLower();
                query = query.Where(x => (x.Registration.Firstname != null && x.Registration.Firstname.ToLower().Contains(s)) || 
                                         (x.Registration.Lastname != null && x.Registration.Lastname.ToLower().Contains(s)) || 
                                         (x.Registration.Organization != null && x.Registration.Organization.ToLower().Contains(s)));
            }

            // 2.5 Apply Role Filter
            if (!string.IsNullOrEmpty(role))
            {
                query = query.Where(x => x.Participant != null && 
                    _context.UserAnswers.Any(ua => ua.ParticipantId == x.Participant.Id && ua.QuestionId == "system_role" && ua.AnswerValue == role));
            }

            // 3. Define the status derived property in the projection
            var statusQuery = query.Select(x => new {
                x.Registration,
                x.Participant,
                x.MutualMatchCount,
                x.ProposedMatchCount,
                x.RequestedCount,
                x.LatestMatchedAt,
                x.HasSurveyAnswers,
                // NOT STARTED: no participant OR participant exists but hasn't answered any questions yet
                // IN PROGRESS: participant has answered the survey but no mutual match yet
                // COMPLETED: has at least one mutual match
                ComputedStatus = x.Participant == null ? "NOT STARTED" :
                                 (!x.HasSurveyAnswers ? "NOT STARTED" :
                                 (x.MutualMatchCount > 0 ? "COMPLETED" : "IN PROGRESS")),
                Role = x.Role
            });

            // 4. Apply Status Filter
            if (!string.IsNullOrEmpty(status))
            {
                statusQuery = statusQuery.Where(x => x.ComputedStatus == status);
            }

            // 5. Total Count for Paging (Database-side)
            var totalCount = await statusQuery.CountAsync();

            // 6. Fetch Paged Results (Database-side)
            var results = await statusQuery
                .OrderBy(x => x.Registration.Firstname)
                .ThenBy(x => x.Registration.Lastname)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // 7. Map to DTO
            var finalItems = results.Select(x => new ParticipantSummaryDto
            {
                Id = x.Participant?.Id ?? Guid.Empty,
                RegistrationId = x.Registration.Id,
                Name = $"{x.Registration.Firstname} {x.Registration.Lastname}",
                Company = x.Registration.Organization ?? "-",
                Superpower = x.Participant?.Superpower ?? "-",
                Status = x.ComputedStatus,
                MatchCount = x.ProposedMatchCount,
                InterestCount = x.MutualMatchCount,
                RequestedCount = x.RequestedCount,
                BookedCount = x.MutualMatchCount,
                MatchedAt = x.LatestMatchedAt,
                LastActive = x.Participant?.CreatedAt ?? x.Registration.CreatedAt,
                Role = x.Role
            }).ToList();

            return Ok(new
            {
                items = finalItems,
                totalCount = totalCount
            });
        }

        [HttpGet("stats")]
        public async Task<ActionResult<object>> GetStats()
        {
            var companyIdString = User.FindFirst("companyId")?.Value;
            if (string.IsNullOrEmpty(companyIdString) || !Guid.TryParse(companyIdString, out Guid companyId)) return Unauthorized();

            var participantIdsQuery = _context.Participants.Where(p => p.CompanyId == companyId).Select(p => p.Id);

            // 1. Calculate Relevanta (1)
            var relevantCount = await _context.Matches.CountAsync(m => (participantIdsQuery.Contains(m.User1Id) || participantIdsQuery.Contains(m.User2Id)) && ((m.User1Feedback == 1) || (m.User2Feedback == 1)));

            // 2. Calculate Ej Relevanta (-1)
            var notRelevantCount = await _context.Matches.CountAsync(m => (participantIdsQuery.Contains(m.User1Id) || participantIdsQuery.Contains(m.User2Id)) && ((m.User1Feedback == -1) || (m.User2Feedback == -1)));

            // 3. Calculate Accuracy
            var totalFeedback = relevantCount + notRelevantCount;
            var accuracy = totalFeedback > 0 ? (double)relevantCount / totalFeedback * 100 : 0;

            // 4. Aggregate Reasons for thumbs down
            // Fetch only fields needed for reasoning
            var feedbackData = await _context.Matches
                .Where(m => (participantIdsQuery.Contains(m.User1Id) || participantIdsQuery.Contains(m.User2Id)) && (!string.IsNullOrEmpty(m.User1FeedbackReason) || !string.IsNullOrEmpty(m.User2FeedbackReason)))
                .Select(m => new { m.User1FeedbackReason, m.User2FeedbackReason })
                .ToListAsync();

            var reasons = new Dictionary<string, int>();
            foreach (var m in feedbackData)
            {
                if (!string.IsNullOrEmpty(m.User1FeedbackReason))
                {
                    reasons[m.User1FeedbackReason] = reasons.GetValueOrDefault(m.User1FeedbackReason) + 1;
                }
                if (!string.IsNullOrEmpty(m.User2FeedbackReason))
                {
                    reasons[m.User2FeedbackReason] = reasons.GetValueOrDefault(m.User2FeedbackReason) + 1;
                }
            }

            var topReasons = reasons.Select(x => new { label = x.Key, count = x.Value })
                                    .OrderByDescending(x => x.count)
                                    .Take(5)
                                    .ToList();

            // 5. Calculate Market Needs from Topics
            var participantsWithTopics = await _context.Participants
                .Where(p => p.CompanyId == companyId && !string.IsNullOrEmpty(p.Topics))
                .Select(p => p.Topics)
                .ToListAsync();

            var topicCounts = new Dictionary<string, int>();
            foreach (var topicsStr in participantsWithTopics)
            {
                var topics = topicsStr!.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                                     .Select(t => t.Trim())
                                     .Where(t => !string.IsNullOrEmpty(t));
                
                foreach (var topic in topics)
                {
                    topicCounts[topic] = topicCounts.GetValueOrDefault(topic) + 1;
                }
            }

            var totalWithTopics = participantsWithTopics.Count;

            var marketNeeds = topicCounts.Select(x => new 
                                     { 
                                         label = x.Key, 
                                         count = x.Value,
                                         percentage = totalWithTopics > 0 ? (int)Math.Round((double)x.Value / totalWithTopics * 100) : 0
                                     })
                                     .OrderByDescending(x => x.count)
                                     .Take(5)
                                     .ToList();

            // 6. Global Dashboard Stats
            var totalParticipants = await _context.Registrations.CountAsync(r => r.CompanyId == companyId);
            var participantsWithProfile = await _context.Participants.CountAsync(p => p.CompanyId == companyId);
            var totalMutualMatches = await _context.Matches.CountAsync(m => (participantIdsQuery.Contains(m.User1Id) && participantIdsQuery.Contains(m.User2Id)) && m.Status == MatchStatus.Matched);

            // Requested meetings: matches where at least one side expressed interest
            var requestedMatches = await _context.Matches
                .Where(m => (participantIdsQuery.Contains(m.User1Id) && participantIdsQuery.Contains(m.User2Id))
                         && (m.User1Interested || m.User2Interested))
                .Select(m => new { m.User1Id, m.User1Interested, m.User2Id, m.User2Interested })
                .ToListAsync();

            var totalRequestedMeetings = requestedMatches.Count;
            var requestedMeetingParticipants = requestedMatches
                .SelectMany(m => new[] { 
                    (m.User1Interested && participantIdsQuery.Contains(m.User1Id)) ? m.User1Id : Guid.Empty,
                    (m.User2Interested && participantIdsQuery.Contains(m.User2Id)) ? m.User2Id : Guid.Empty 
                })
                .Where(id => id != Guid.Empty)
                .Distinct()
                .Count();

            // Booked meetings: mutually matched
            var bookedMatches = await _context.Matches
                .Where(m => (participantIdsQuery.Contains(m.User1Id) && participantIdsQuery.Contains(m.User2Id))
                         && m.Status == MatchStatus.Matched)
                .Select(m => new { m.User1Id, m.User2Id })
                .ToListAsync();
            var totalBookedMeetings = totalMutualMatches;
            var bookedMeetingParticipants = bookedMatches
                .SelectMany(m => new[] { m.User1Id, m.User2Id })
                .Distinct()
                .Count();

            // Potential matches (Total AI results)
            var totalPotentialMatches = await _context.Matches.CountAsync(m => (participantIdsQuery.Contains(m.User1Id) && participantIdsQuery.Contains(m.User2Id)) && m.Score >= 25);


            return Ok(new
            {
                totalParticipants = totalParticipants,
                completedCount = participantsWithProfile,
                totalMatches = totalMutualMatches,
                requestedMeetingParticipants = requestedMeetingParticipants,
                totalRequestedMeetings = totalRequestedMeetings,
                bookedMeetingParticipants = bookedMeetingParticipants,
                totalBookedMeetings = totalBookedMeetings,
                totalPotentialMatches = totalPotentialMatches,
                totalFeedback = relevantCount + notRelevantCount,
                relevant = relevantCount,
                notRelevant = notRelevantCount,
                accuracy = Math.Round(accuracy),
                reasons = topReasons,
                marketNeeds = marketNeeds
            });
        }

        [HttpGet("matching-progress")]
        public IActionResult GetMatchingProgress([FromServices] NackaMatchmaking.API.Services.TaskProgressService progressService)
        {
            var companyIdString = User.FindFirst("companyId")?.Value;
            if (string.IsNullOrEmpty(companyIdString) || !Guid.TryParse(companyIdString, out Guid companyId)) return Unauthorized();

            var taskKey = "matching_" + companyId;
            var counts = progressService.GetItemCounts(taskKey);
            return Ok(new 
            { 
                progress = progressService.GetProgress(taskKey),
                isActive = progressService.IsTaskActive(taskKey),
                estimatedTimeRemainingSeconds = progressService.GetEstimatedTimeRemainingSeconds(taskKey),
                processed = counts.Processed,
                total = counts.Total
            });
        }

        // GET: api/Registrations/participant/{id}
        [HttpGet("participant/{id}")]
        [Microsoft.AspNetCore.Authorization.AllowAnonymous]
        public async Task<ActionResult<object>> GetParticipant(Guid id)
        {
            var p = await _context.Participants.FindAsync(id);
            if (p == null) return NotFound();

            var role = await _context.UserAnswers
                .Where(ua => ua.ParticipantId == id && ua.QuestionId == "system_role")
                .Select(ua => ua.AnswerValue)
                .FirstOrDefaultAsync();

            return Ok(new 
            {
                id = p.Id,
                companyId = p.CompanyId,
                name = $"{p.Firstname} {p.Lastname}",
                title = p.Title,
                company = p.Organization,
                email = p.Email,
                bio = p.Bio,
                superpower = p.Superpower,
                challenge = p.Challenge,
                topics = p.Topics,
                companyDescription = p.CompanyDescription,
                eventRating = p.EventRating,
                eventComment = p.EventComment,
                feedbackRequestSent = p.FeedbackRequestSentAt.HasValue,
                role = role
            });
        }

        // POST: api/Registrations/participant/{id}/event-feedback
        [HttpPost("participant/{id}/event-feedback")]
        [Microsoft.AspNetCore.Authorization.AllowAnonymous]
        public async Task<IActionResult> SubmitEventFeedback(Guid id, [FromBody] EventFeedbackRequest request)
        {
            if (request.Rating < 1 || request.Rating > 5)
                return BadRequest("Rating must be between 1 and 5.");

            var participant = await _context.Participants.FindAsync(id);
            if (participant == null) return NotFound();

            participant.EventRating = request.Rating;
            participant.EventComment = request.Comment?.Trim();

            await _context.SaveChangesAsync();
            return Ok();
        }

        // DELETE: api/Registrations/{id}
        // Deletes Registration AND Participant AND Matches
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteRegistration(Guid id)
        {
            var companyIdString = User.FindFirst("companyId")?.Value;
            if (string.IsNullOrEmpty(companyIdString) || !Guid.TryParse(companyIdString, out Guid companyId)) return Unauthorized();

            // 1. Find Registration (select only needed fields)
            var registration = await _context.Registrations
                .Where(r => r.CompanyId == companyId && r.Id == id)
                .Select(r => new { r.Id, r.Email })
                .FirstOrDefaultAsync();

            if (registration == null) return NotFound("Registration not found");

            // 2. Find Participant (if any) by Email
            if (!string.IsNullOrEmpty(registration.Email))
            {
                var participantId = await _context.Participants
                    .Where(p => p.CompanyId == companyId && p.Email == registration.Email)
                    .Select(p => p.Id)
                    .FirstOrDefaultAsync();

                if (participantId != Guid.Empty)
                {
                    // 3. Find Matches to delete chats
                    var matchIds = await _context.Matches
                        .Where(m => m.User1Id == participantId || m.User2Id == participantId)
                        .Select(m => m.Id)
                        .ToListAsync();

                    if (matchIds.Any())
                    {
                        // 4. Delete ChatMessages and Matches (ExecuteDeleteAsync is introduced in EF Core 7)
                        await _context.ChatMessages
                            .Where(c => matchIds.Contains(c.MatchId))
                            .ExecuteDeleteAsync();

                        await _context.Matches
                            .Where(m => matchIds.Contains(m.Id))
                            .ExecuteDeleteAsync();
                    }

                    // 5. Delete Participant
                    await _context.Participants
                        .Where(p => p.Id == participantId)
                        .ExecuteDeleteAsync();
                }
            }

            // 6. Delete Registration
            await _context.Registrations
                .Where(r => r.Id == id)
                .ExecuteDeleteAsync();

            return NoContent();
        }

        // DELETE: api/Registrations/all
        [HttpDelete("all")]
        public async Task<IActionResult> DeleteAllParticipants()
        {
            var companyIdString = User.FindFirst("companyId")?.Value;
            if (string.IsNullOrEmpty(companyIdString) || !Guid.TryParse(companyIdString, out Guid companyId)) return Unauthorized();

            var participantIds = await _context.Participants.Where(p => p.CompanyId == companyId).Select(p => p.Id).ToListAsync();
            var matchIds = await _context.Matches.Where(m => participantIds.Contains(m.User1Id) || participantIds.Contains(m.User2Id)).Select(m => m.Id).ToListAsync();
            
            await _context.ChatMessages.Where(c => matchIds.Contains(c.MatchId)).ExecuteDeleteAsync();
            await _context.Matches.Where(m => matchIds.Contains(m.Id)).ExecuteDeleteAsync();
            await _context.Participants.Where(p => p.CompanyId == companyId).ExecuteDeleteAsync();
            await _context.Registrations.Where(r => r.CompanyId == companyId).ExecuteDeleteAsync();

            return NoContent();
        }

        [HttpPost("send-invitations")]
        public IActionResult SendInvitations([FromServices] NackaMatchmaking.API.Services.TaskProgressService progressService)
        {
            var companyIdString = User.FindFirst("companyId")?.Value;
            if (string.IsNullOrEmpty(companyIdString) || !Guid.TryParse(companyIdString, out Guid companyId)) return Unauthorized();

            var taskKey = "matching_" + companyId;

            if (progressService.IsTaskActive(taskKey))
            {
                return Accepted(new { message = "En annan process (matchning eller utskick) pågår redan." });
            }

            var ct = progressService.StartTask(taskKey);

            _ = Task.Run(async () =>
            {
                using var scope = _scopeFactory.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var emailService = scope.ServiceProvider.GetRequiredService<NackaMatchmaking.API.Services.IEmailService>();
                var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
                var logger = scope.ServiceProvider.GetRequiredService<ILogger<RegistrationsController>>();
                
                try 
                {
                    logger.LogInformation("Background SendInvitations task started for Company {CompanyId}.", companyId);
                    var registrations = await dbContext.Registrations.Where(r => r.CompanyId == companyId).ToListAsync();
                    var frontendUrl = config["FrontendUrl"] ?? "https://matchmaking.itmaskinen.se";
                    
                    var templateSetting = await dbContext.Settings.FirstOrDefaultAsync(s => s.CompanyId == companyId && s.Key == "InvitationEmailTemplate");
                    var defaultTemplate = $@"
                    <div style='font-family: sans-serif; max-width: 600px; line-height: 1.6;'>
                        <p>Hej {{{{ParticipantName}}}}!</p>
                        <p>Nacka Företagsträff har alltid handlat om att skapa möten som leder till riktig affärsnytta. I år tar vi nästa steg för att göra ditt deltagande ännu mer effektivt.</p>
                        <p>Tillsammans med <strong>ITmaskinen</strong>, som i 25 år hjälpt företag att ligga i framkant, introducerar vi nu <strong>Träffpunkten</strong> – en helt ny AI-baserad matchmaking-tjänst.</p>
                        
                        <p><strong>Varför testa AI-matchmaking?</strong> Istället för att hoppas på att du springer på rätt person vid kaffemaskinen, använder vi intelligent teknik för att para ihop dig med de deltagare, utställare eller partners som bäst matchar dina behov och intressen.</p>

                        <p><strong>Din oas på mässgolvet: Träffpunktens Lounge</strong><br>
                        När du tackar ja till att delta i matchmakingen får du exklusiv tillgång till ett dedikerat område mitt på träffen. I loungen kan du:</p>
                        <ul style='margin-bottom: 20px;'>
                            <li><strong>Genomföra dina möten:</strong> En lugnare plats dedikerad för dina AI-matchade kontakter.</li>
                            <li><strong>Ladda mobilen:</strong> Vi vet att batteriet sinar snabbt under en intensiv mässdag – vi har laddstationerna redo.</li>
                            <li><strong>Ta en paus:</strong> Vi bjuder på kaffe och ger dig utrymme att landa mellan besöken.</li>
                        </ul>

                        <p><strong>Så här gör du:</strong> Det tar mindre än en minut att komma igång. Klicka på länken nedan och ange kort vad du erbjuder och vad du söker på årets träff.</p>
                        
                        <p style='margin: 30px 0;'>
                            👉 <a href='{{{{MatchmakingLink}}}}' style='color: #6366f1; font-weight: bold; text-decoration: none;'>Ja, jag vill hitta rätt kontakter via AI!</a>
                        </p>

                        <p>Vi ses på Nacka Företagsträff – där traditionellt nätverkande möter morgondagens teknik!</p>
                        <p>Varma hälsningar,<br>
                        <strong>Projektledningen Nacka Företagsträff</strong> <em>i samarbete med ITmaskinen</em></p>
                    </div>";
                    
                    var rawTemplate = !string.IsNullOrWhiteSpace(templateSetting?.Value) ? templateSetting.Value : defaultTemplate;

            var subjectSetting = await dbContext.Settings.FirstOrDefaultAsync(s => s.CompanyId == companyId && s.Key == "InvitationEmailSubject");
            var subject = !string.IsNullOrWhiteSpace(subjectSetting?.Value) 
                ? subjectSetting.Value 
                : "Maxa ditt nätverkande på Nacka Företagarträffen - testa vår nya AI-matchmaking!";

            int count = 0;
            int total = registrations.Count;

            for (int i = 0; i < registrations.Count; i++)
            {
                if (ct.IsCancellationRequested) break;

                var reg = registrations[i];
                progressService.UpdateProgress(taskKey, (double)(i + 1) / total * 100, i + 1, total);
                var matchingLink = $"{frontendUrl}?companyId={companyId}&id={reg.Id}";
                var body = rawTemplate
                    .Replace("{{ParticipantName}}", reg.Firstname ?? "")
                    .Replace("{{CompanyName}}", reg.Organization ?? "")
                    .Replace("{{MatchmakingLink}}", matchingLink);

                await emailService.SendEmailAsync(reg.Email!, subject, body);
                count++;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error occurred during background SendInvitations task.");
        }
        finally
        {
            progressService.CompleteTask(taskKey);
        }

            });

            return Accepted(new { message = "Utskick av inbjudningar har startats i bakgrunden." });
        }

        [HttpPost("send-results")]
        public IActionResult SendResults([FromServices] NackaMatchmaking.API.Services.TaskProgressService progressService)
        {
            var companyIdString = User.FindFirst("companyId")?.Value;
            if (string.IsNullOrEmpty(companyIdString) || !Guid.TryParse(companyIdString, out Guid companyId)) return Unauthorized();

            var taskKey = "matching_" + companyId;

            if (progressService.IsTaskActive(taskKey))
            {
                return Accepted(new { message = "En annan process (matchning eller utskick) pågår redan." });
            }

            var ct = progressService.StartTask(taskKey);

            _ = Task.Run(async () =>
            {
                using var scope = _scopeFactory.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var emailService = scope.ServiceProvider.GetRequiredService<NackaMatchmaking.API.Services.IEmailService>();
                var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
                var logger = scope.ServiceProvider.GetRequiredService<ILogger<RegistrationsController>>();
                
                try 
                {
                    var participants = await dbContext.Participants.Where(p => p.CompanyId == companyId).ToListAsync();
                    var pIds = participants.Select(p => p.Id).ToList();
                    var matches = await dbContext.Matches
                        .Where(m => pIds.Contains(m.User1Id) && pIds.Contains(m.User2Id))
                        .ToListAsync();
                    var frontendUrl = config["FrontendUrl"] ?? "https://matchmaking.itmaskinen.se";
                    
                    var templateSetting = await dbContext.Settings.FirstOrDefaultAsync(s => s.CompanyId == companyId && s.Key == "ResultEmailTemplate");
                    var rawTemplate = !string.IsNullOrWhiteSpace(templateSetting?.Value) ? templateSetting.Value : ""; 
                    if (string.IsNullOrEmpty(rawTemplate)) {
                        rawTemplate = "Hi {{ParticipantName}}! See your matches here: {{ResultsTable}}";
                    }

                    var subjectSetting = await dbContext.Settings.FirstOrDefaultAsync(s => s.CompanyId == companyId && s.Key == "ResultEmailSubject");
                    var subject = !string.IsNullOrWhiteSpace(subjectSetting?.Value)
                        ? subjectSetting.Value
                        : "Your AI matches for Nacka Företagarträffen are here!";

                    int count = 0;
                    int total = participants.Count;

                    for (int i = 0; i < participants.Count; i++)
                    {
                        if (ct.IsCancellationRequested) break;

                        var p = participants[i];
                        progressService.UpdateProgress(taskKey, (double)(i + 1) / total * 100, i + 1, total);

                        bool surveyComplete = !string.IsNullOrEmpty(p.Superpower) && !string.IsNullOrEmpty(p.Challenge) && !string.IsNullOrEmpty(p.Topics);
                        int matchCount = matches.Count(m => (m.User1Id == p.Id || m.User2Id == p.Id) && m.Status == MatchStatus.Matched);
                        int relevantMatchCount = matches.Count(m => (m.User1Id == p.Id || m.User2Id == p.Id) && m.Score >= 25);

                        string itemStatus = (surveyComplete && matchCount > 0) ? "COMPLETED" : "IN PROGRESS";
                        // Only send results to completed participants
                        if (itemStatus == "IN PROGRESS") continue; // skip incomplete participants
                        if (relevantMatchCount == 0) continue;

                        var matchingLink = $"{frontendUrl}/matches?companyId={companyId}&id={p.Id}";
                        var body = rawTemplate
                            .Replace("{{ParticipantName}}", p.Firstname ?? "")
                            .Replace("{{CompanyName}}", p.Organization ?? "")
                            .Replace("{{ResultsTable}}", matchingLink);

                        await emailService.SendEmailAsync(p.Email!, subject, body);
                        count++;
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error occurred during background SendResults task.");
                }
                finally
                {
                    progressService.CompleteTask(taskKey);
                }
            });

            return Accepted(new { message = "Utskick av matchningsresultat har startats i bakgrunden." });
        }

        [HttpPost("send-feedback-requests")]
        public IActionResult SendFeedbackRequests([FromServices] NackaMatchmaking.API.Services.TaskProgressService progressService)
        {
            var companyIdString = User.FindFirst("companyId")?.Value;
            if (string.IsNullOrEmpty(companyIdString) || !Guid.TryParse(companyIdString, out Guid companyId)) return Unauthorized();

            var taskKey = "matching_" + companyId;

            if (progressService.IsTaskActive(taskKey))
            {
                return Accepted(new { message = "Another process is already running." });
            }

            var ct = progressService.StartTask(taskKey);

            _ = Task.Run(async () =>
            {
                using var scope = _scopeFactory.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var emailService = scope.ServiceProvider.GetRequiredService<NackaMatchmaking.API.Services.IEmailService>();
                var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
                var logger = scope.ServiceProvider.GetRequiredService<ILogger<RegistrationsController>>();
                
                try 
                {
                    logger.LogInformation("Background SendFeedbackRequests task started.");
                    var participants = await dbContext.Participants
                        .Where(p => p.CompanyId == companyId)
                        .ToListAsync();

                    // Retrieve settings
                    var feedbackTemplateSetting = await dbContext.Settings.FirstOrDefaultAsync(s => s.CompanyId == companyId && s.Key == "FeedbackEmailTemplate");
                    var feedbackRawTemplate = !string.IsNullOrWhiteSpace(feedbackTemplateSetting?.Value) ? feedbackTemplateSetting.Value : ""; 
                    if (string.IsNullOrEmpty(feedbackRawTemplate)) {
                        feedbackRawTemplate = @"
                    <div style='font-family: sans-serif; line-height: 1.6;'>
                        <h2>Hi {{ParticipantName}}!</h2>
                        <p>We hope you had productive meetings and interesting conversations!</p>
                        <p>We would love to hear your feedback on your matches and your overall matchmaking experience. This helps us make future events even better.</p>
                        <p>Please click the link below to see your results and leave feedback on your matches:</p>
                        <p style='margin: 20px 0;'>
                            <a href='{{ResultsLink}}' style='color: #6366f1; text-decoration: none; font-weight: bold;'>See results & leave feedback</a>
                        </p>
                        <p>Thank you for participating!</p>
                        <p>Best regards,<br>The Nacka Företagarträff Team</p>
                    </div>";
                    }

                    var feedbackSubjectSetting = await dbContext.Settings.FirstOrDefaultAsync(s => s.CompanyId == companyId && s.Key == "FeedbackEmailSubject");
                    var feedbackSubject = !string.IsNullOrWhiteSpace(feedbackSubjectSetting?.Value)
                        ? feedbackSubjectSetting.Value
                        : "How was your matchmaking experience at Nacka Företagarträff?";

                    // Only those who have at least one match within this company
                    var pPoolIds = participants.Select(p => p.Id).ToList();
                    var matchedPairs = await dbContext.Matches
                        .Where(m => m.Status == MatchStatus.Matched && pPoolIds.Contains(m.User1Id) && pPoolIds.Contains(m.User2Id))
                        .Select(m => new { m.User1Id, m.User2Id })
                        .ToListAsync();

                    var participantIdsWithMatches = matchedPairs
                        .SelectMany(m => new[] { m.User1Id, m.User2Id })
                        .Distinct()
                        .ToList();

                    var targets = participants
                        .Where(p => participantIdsWithMatches.Contains(p.Id))
                        .ToList();

                    if (!targets.Any()) {
                        logger.LogInformation("No matched participants found for feedback.");
                        return;
                    }

                    var frontendUrl = config["FrontendUrl"] ?? "https://matchmaking.itmaskinen.se";
                    int total = targets.Count;

                    for (int i = 0; i < targets.Count; i++)
                    {
                        if (ct.IsCancellationRequested) break;

                        var p = targets[i];
                        progressService.UpdateProgress(taskKey, (double)(i + 1) / total * 100, i + 1, total);

                        var resultsLink = $"{frontendUrl}/matches?companyId={companyId}&id={p.Id}";
                        
                        var body = feedbackRawTemplate
                            .Replace("{{ParticipantName}}", p.Firstname ?? "")
                            .Replace("{{ResultsLink}}", resultsLink);

                        await emailService.SendEmailAsync(p.Email!, feedbackSubject, body);

                        // Mark that feedback request was sent to this participant
                        p.FeedbackRequestSentAt = DateTime.UtcNow;
                    }

                    await dbContext.SaveChangesAsync();
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error occurred during background SendFeedbackRequests task.");
                }
                finally
                {
                    progressService.CompleteTask(taskKey);
                }
            });

            return Accepted(new { message = "Feedback requests sending process started in the background." });
        }
        [HttpPost("send-feedback-request/{id}")]
        public async Task<IActionResult> SendFeedbackRequest(Guid id)
        {
            var companyIdString = User.FindFirst("companyId")?.Value;
            if (string.IsNullOrEmpty(companyIdString) || !Guid.TryParse(companyIdString, out Guid companyId)) return Unauthorized();

            var p = await _context.Participants.FindAsync(id);
            if (p == null || p.CompanyId != companyId) return NotFound("Participant not found");

            var hasMatches = await _context.Matches.AnyAsync(m => (m.User1Id == id || m.User2Id == id) && m.Status == MatchStatus.Matched);
            if (!hasMatches) return BadRequest("Participant has no mutual matches.");

            var config = _configuration;
            var frontendUrl = config["FrontendUrl"] ?? "https://matchmaking.itmaskinen.se";
            var feedbackTemplateSetting = await _context.Settings.FirstOrDefaultAsync(s => s.CompanyId == companyId && s.Key == "FeedbackEmailTemplate");
            var feedbackRawTemplate = !string.IsNullOrWhiteSpace(feedbackTemplateSetting?.Value) ? feedbackTemplateSetting.Value : ""; 
            if (string.IsNullOrEmpty(feedbackRawTemplate)) {
                feedbackRawTemplate = @"
                    <div style='font-family: sans-serif; line-height: 1.6;'>
                        <h2>Hi {{ParticipantName}}!</h2>
                        <p>We hope you had productive meetings and interesting conversations!</p>
                        <p>We would love to hear your feedback on your matches and your overall matchmaking experience. This helps us make future events even better.</p>
                        <p>Click the link below to see your results and leave feedback on your matches:</p>
                        <p style='margin: 20px 0;'>
                            <a href='{{ResultsLink}}' style='color: #6366f1; text-decoration: none; font-weight: bold;'>See results & leave feedback</a>
                        </p>
                        <p>Thank you for participating!</p>
                        <p>Best regards,<br>The Nacka Företagarträff Team</p>
                    </div>";
            }

            var feedbackSubjectSetting = await _context.Settings.FirstOrDefaultAsync(s => s.CompanyId == companyId && s.Key == "FeedbackEmailSubject");
            var feedbackSubject = !string.IsNullOrWhiteSpace(feedbackSubjectSetting?.Value)
                ? feedbackSubjectSetting.Value
                : "How was your matchmaking experience at Nacka Företagarträff?";

            var resultsLink = $"{frontendUrl}/matches?companyId={companyId}&id={p.Id}";
            
            var body = feedbackRawTemplate
                .Replace("{{ParticipantName}}", p.Firstname ?? "")
                .Replace("{{ResultsLink}}", resultsLink);

            await _emailService.SendEmailAsync(p.Email!, feedbackSubject, body);

            // Mark that feedback request was sent
            p.FeedbackRequestSentAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return Ok(new { message = "Feedback request sent successfully." });
        }

        [HttpPost("send-reminders")]
        public IActionResult SendReminders([FromServices] NackaMatchmaking.API.Services.TaskProgressService progressService)
        {
            var companyIdString = User.FindFirst("companyId")?.Value;
            if (string.IsNullOrEmpty(companyIdString) || !Guid.TryParse(companyIdString, out Guid companyId)) return Unauthorized();

            var taskKey = "matching_" + companyId;

            if (progressService.IsTaskActive(taskKey))
            {
                return Accepted(new { message = "En annan process (matchning eller utskick) pågår redan." });
            }

            var ct = progressService.StartTask(taskKey);

            _ = Task.Run(async () =>
            {
                using var scope = _scopeFactory.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var emailService = scope.ServiceProvider.GetRequiredService<NackaMatchmaking.API.Services.IEmailService>();
                var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
                var logger = scope.ServiceProvider.GetRequiredService<ILogger<RegistrationsController>>();
                
                try 
                {
                    logger.LogInformation("Background SendReminders task started.");
                    var registrations = await dbContext.Registrations.Where(r => r.CompanyId == companyId).ToListAsync();
                    var participants = await dbContext.Participants.Where(p => p.CompanyId == companyId).Select(p => p.Email).ToListAsync();
                    
                    // Filter for those who haven't started their profile yet
                    var unanswered = registrations.Where(r => !participants.Contains(r.Email)).ToList();
                    
                    if (!unanswered.Any()) {
                        logger.LogInformation("No unanswered participants found.");
                        return;
                    }

                    var frontendUrl = config["FrontendUrl"] ?? "https://matchmaking.itmaskinen.se";
                    
                    var templateSetting = await dbContext.Settings.FirstOrDefaultAsync(s => s.CompanyId == companyId && s.Key == "InvitationEmailTemplate");
                    var rawTemplate = !string.IsNullOrWhiteSpace(templateSetting?.Value) ? templateSetting.Value : ""; 
                    if (string.IsNullOrEmpty(rawTemplate)) {
                        rawTemplate = "Hi {{ParticipantName}}! Don't forget to maximize your networking. See your matches here: {{MatchmakingLink}}";
                    }

                    var subjectSetting = await dbContext.Settings.FirstOrDefaultAsync(s => s.CompanyId == companyId && s.Key == "InvitationEmailSubject");
                    var subject = !string.IsNullOrWhiteSpace(subjectSetting?.Value) 
                        ? subjectSetting.Value 
                        : "Påminnelse: Maxa ditt nätverkande på Nacka Företagarträffen!";

                    int total = unanswered.Count;

                    for (int i = 0; i < unanswered.Count; i++)
                    {
                        if (ct.IsCancellationRequested) break;

                        var reg = unanswered[i];
                        progressService.UpdateProgress(taskKey, (double)(i + 1) / total * 100, i + 1, total);
                        var matchingLink = $"{frontendUrl}?companyId={companyId}&id={reg.Id}";
                        var body = rawTemplate
                            .Replace("{{ParticipantName}}", reg.Firstname ?? "")
                            .Replace("{{CompanyName}}", reg.Organization ?? "")
                            .Replace("{{MatchmakingLink}}", matchingLink);

                        await emailService.SendEmailAsync(reg.Email!, subject, body);
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error in SendReminders background task.");
                }
                finally
                {
                    progressService.CompleteTask(taskKey);
                }
            });

            return Accepted(new { message = "Påminnelser har påbörjats i bakgrunden." });
        }


        [HttpPost("generate-matches")]
        public IActionResult GenerateMatches([FromServices] NackaMatchmaking.API.Services.TaskProgressService progressService)
        {
            var companyIdString = User.FindFirst("companyId")?.Value;
            if (string.IsNullOrEmpty(companyIdString) || !Guid.TryParse(companyIdString, out Guid companyId)) return Unauthorized();

            var taskKey = "matching_" + companyId;

            if (progressService.IsTaskActive(taskKey))
            {
                return Accepted(new { message = "Matchningsprocessen pågår redan i bakgrunden." });
            }

            var ct = progressService.StartTask(taskKey);

            // Start the matching process in the background
            _ = Task.Run(async () =>
            {
                using var scope = _scopeFactory.CreateScope();
                var matchingService = scope.ServiceProvider.GetRequiredService<NackaMatchmaking.API.Services.MatchingService>();
                var logger = scope.ServiceProvider.GetRequiredService<ILogger<RegistrationsController>>();
                try 
                {
                    logger.LogInformation("Background matching process started.");
                    await matchingService.GenerateMatchesForAll(companyId, ct);
                    logger.LogInformation("Background matching process completed successfully.");
                }
                catch (OperationCanceledException)
                {
                    logger.LogInformation("Background matching process was cancelled.");
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error during background matching process.");
                }
                finally
                {
                    progressService.CompleteTask(taskKey);
                }
            });

            return Accepted(new { message = "Matchningsprocessen har startats i bakgrunden." });
        }

        [HttpPost("cancel-matching")]
        public IActionResult CancelMatching([FromServices] NackaMatchmaking.API.Services.TaskProgressService progressService)
        {
            var companyIdString = User.FindFirst("companyId")?.Value;
            if (string.IsNullOrEmpty(companyIdString) || !Guid.TryParse(companyIdString, out Guid companyId)) return Unauthorized();

            progressService.CancelTask("matching_" + companyId);
            return Ok(new { message = "Matchningsprocessen har avbrutits." });
        }

        [HttpPost("add")]
        public async Task<ActionResult<object>> PostRegistrationAdmin([FromBody] RegistrationAdminDto dto)
        {
            var companyIdString = User.FindFirst("companyId")?.Value;
            if (string.IsNullOrEmpty(companyIdString) || !Guid.TryParse(companyIdString, out Guid companyId)) return Unauthorized();

            var res = await PostRegistration(companyId, new RegistrationDto 
            {
                Email = dto.Email,
                Firstname = dto.Firstname,
                Lastname = dto.Lastname,
                Organization = dto.Organization,
                Title = dto.Title,
                HasAcceptedTerms = true
            });

            if (res.Result is OkObjectResult okResult)
            {
                // Extract registration ID from the result of PostRegistration
                var reg = await _context.Registrations.FirstOrDefaultAsync(r => r.CompanyId == companyId && r.Email == dto.Email);
                if (reg != null && dto.SendInvite)
                {
                    var frontendUrl = _configuration["FrontendUrl"] ?? "https://matchmaking.itmaskinen.se";
                    var templateSetting = await _context.Settings.FirstOrDefaultAsync(s => s.CompanyId == companyId && s.Key == "InvitationEmailTemplate");
                    var rawTemplate = !string.IsNullOrWhiteSpace(templateSetting?.Value) ? templateSetting.Value : "Hi {{ParticipantName}}! See your matches here: {{MatchmakingLink}}";
                    
                    var subjectSetting = await _context.Settings.FirstOrDefaultAsync(s => s.CompanyId == companyId && s.Key == "InvitationEmailSubject");
                    var subject = !string.IsNullOrWhiteSpace(subjectSetting?.Value) 
                        ? subjectSetting.Value 
                        : "Maximize your networking - try our new AI matchmaking!";

                    await SendSingleInvitationAsync(reg, frontendUrl, rawTemplate, subject, companyId);
                }
                
                return Ok(new { message = "Participant added successfully", id = reg?.Id });
            }

            return res;
        }

        private async Task SendSingleInvitationAsync(Registration reg, string frontendUrl, string rawTemplate, string subject, Guid companyId)
        {
            var matchingLink = $"{frontendUrl}?companyId={companyId}&id={reg.Id}";
            var body = rawTemplate
                .Replace("{{ParticipantName}}", reg.Firstname ?? "")
                .Replace("{{CompanyName}}", reg.Organization ?? "")
                .Replace("{{MatchmakingLink}}", matchingLink);

            await _emailService.SendEmailAsync(reg.Email!, subject, body);
        }

        public class RegistrationAdminDto : RegistrationDto
        {
            public bool SendInvite { get; set; }
        }
    }

    public class EventFeedbackRequest
    {
        public int Rating { get; set; }
        public string? Comment { get; set; }
    }
}
