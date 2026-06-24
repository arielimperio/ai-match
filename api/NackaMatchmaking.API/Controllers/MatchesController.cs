using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using NackaMatchmaking.API.Data;
using NackaMatchmaking.API.Models;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;

namespace NackaMatchmaking.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class MatchesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public MatchesController(ApplicationDbContext context)
        {
            _context = context;
        }

        [AllowAnonymous]
        [HttpPost("interest")]
        public async Task<ActionResult<UserMatch>> SetInterest([FromBody] InterestRequest request)
        {
            // Resolve companyId from the source participant (no JWT needed for end users)
            var sourceParticipant = await _context.Participants.FindAsync(request.SourceId);
            if (sourceParticipant == null) return Unauthorized();
            var companyId = sourceParticipant.CompanyId;

            // Convention: User1Id < User2Id
            var compare = request.SourceId.CompareTo(request.TargetId);
            Guid u1 = compare < 0 ? request.SourceId : request.TargetId;
            Guid u2 = compare < 0 ? request.TargetId : request.SourceId;

            var match = await _context.Matches
                .FirstOrDefaultAsync(m => m.User1Id == u1 && m.User2Id == u2 && m.User1.CompanyId == companyId);

            if (match == null)
            {
                match = new UserMatch
                {
                    User1Id = u1,
                    User2Id = u2,
                    User1Interested = false,
                    User2Interested = false,
                    Status = MatchStatus.Pending
                };
                _context.Matches.Add(match);
            }

            // Update interest based on who is asking
            if (request.SourceId == u1) match.User1Interested = true;
            else match.User2Interested = true;

            // Check if mutual
            if (match.User1Interested && match.User2Interested)
            {
                match.Status = MatchStatus.Matched;
                match.MatchedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();
            return Ok(match);
        }

        // POST: api/Matches/interest/undo
        [AllowAnonymous]
        [HttpPost("interest/undo")]
        public async Task<ActionResult<UserMatch>> UndoInterest([FromBody] InterestRequest request)
        {
            var sourceParticipant = await _context.Participants.FindAsync(request.SourceId);
            if (sourceParticipant == null) return Unauthorized();
            var companyId = sourceParticipant.CompanyId;

            var compare = request.SourceId.CompareTo(request.TargetId);
            Guid u1 = compare < 0 ? request.SourceId : request.TargetId;
            Guid u2 = compare < 0 ? request.TargetId : request.SourceId;

            var match = await _context.Matches
                .FirstOrDefaultAsync(m => m.User1Id == u1 && m.User2Id == u2 && m.User1.CompanyId == companyId);

            if (match == null) return NotFound();

            // Update interest based on who is asking
            if (request.SourceId == u1) match.User1Interested = false;
            else match.User2Interested = false;

            // If it was matched, revert to pending
            if (match.Status == MatchStatus.Matched)
            {
                match.Status = MatchStatus.Pending;
                match.MatchedAt = null;
            }

            await _context.SaveChangesAsync();
            return Ok(match);
        }

        // POST: api/Matches/feedback
        [AllowAnonymous]
        [HttpPost("feedback")]
        public async Task<ActionResult> SetFeedback([FromBody] FeedbackRequest request)
        {
            var sourceParticipant = await _context.Participants.FindAsync(request.SourceId);
            if (sourceParticipant == null) return Unauthorized();
            var companyId = sourceParticipant.CompanyId;

            var match = await _context.Matches
                .FirstOrDefaultAsync(m => m.Id == request.MatchId && m.User1.CompanyId == companyId);

            if (match == null) return NotFound();

            if (request.SourceId == match.User1Id)
            {
                match.User1Feedback = request.Rating;
                match.User1FeedbackReason = request.Comment;
            }
            else if (request.SourceId == match.User2Id)
            {
                match.User2Feedback = request.Rating;
                match.User2FeedbackReason = request.Comment;
            }
            else return BadRequest("Invalid SourceId for this match.");

            await _context.SaveChangesAsync();
            return Ok();
        }

        // GET: api/Matches/{matchId}/chat
        [AllowAnonymous]
        [HttpGet("{matchId}/chat")]
        public async Task<ActionResult<IEnumerable<ChatMessage>>> GetChat(Guid matchId, [FromQuery] Guid? participantId)
        {
            bool matchExists;
            if (participantId.HasValue)
            {
                matchExists = await _context.Matches.AnyAsync(m => m.Id == matchId && (m.User1Id == participantId || m.User2Id == participantId));
            }
            else
            {
                var companyIdString = User.FindFirst("companyId")?.Value;
                if (string.IsNullOrEmpty(companyIdString) || !Guid.TryParse(companyIdString, out Guid companyId)) return Unauthorized();
                matchExists = await _context.Matches.AnyAsync(m => m.Id == matchId && m.User1.CompanyId == companyId);
            }
            if (!matchExists) return NotFound();

            return await _context.ChatMessages
                .Where(c => c.MatchId == matchId)
                .OrderBy(c => c.Timestamp)
                .ToListAsync();
        }

        // POST: api/Matches/{matchId}/chat
        [AllowAnonymous]
        [HttpPost("{matchId}/chat")]
        public async Task<ActionResult<ChatMessage>> SendMessage(Guid matchId, [FromBody] ChatMessage message, [FromQuery] Guid? participantId)
        {
            bool matchExists;
            if (participantId.HasValue)
            {
                matchExists = await _context.Matches.AnyAsync(m => m.Id == matchId && (m.User1Id == participantId || m.User2Id == participantId));
            }
            else
            {
                var companyIdString = User.FindFirst("companyId")?.Value;
                if (string.IsNullOrEmpty(companyIdString) || !Guid.TryParse(companyIdString, out Guid companyId)) return Unauthorized();
                matchExists = await _context.Matches.AnyAsync(m => m.Id == matchId && m.User1.CompanyId == companyId);
            }
            if (!matchExists) return NotFound();

            message.MatchId = matchId;
            message.Timestamp = DateTime.UtcNow;
            
            _context.ChatMessages.Add(message);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetChat), new { matchId = matchId }, message);
        }

        // GET: api/Matches/mutual
        [HttpGet("mutual")]
        public async Task<IActionResult> GetMutualMatches([FromQuery] string search = "")
        {
            var companyIdString = User.FindFirst("companyId")?.Value;
            if (string.IsNullOrEmpty(companyIdString) || !Guid.TryParse(companyIdString, out Guid companyId))
                return Unauthorized();

            var query = _context.Matches
                .Include(m => m.User1)
                .Include(m => m.User2)
                .Where(m => m.Status == MatchStatus.Matched && m.User1.CompanyId == companyId);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.ToLower();
                query = query.Where(m =>
                    (m.User1.Firstname + " " + m.User1.Lastname).ToLower().Contains(s) ||
                    (m.User1.Organization ?? "").ToLower().Contains(s) ||
                    (m.User2.Firstname + " " + m.User2.Lastname).ToLower().Contains(s) ||
                    (m.User2.Organization ?? "").ToLower().Contains(s));
            }

            var matches = await query
                .OrderByDescending(m => m.MatchedAt)
                .Select(m => new
                {
                    id = m.Id,
                    participant1Id = m.User1Id,
                    participant1Name = (m.User1.Firstname + " " + m.User1.Lastname).Trim(),
                    participant1Company = m.User1.Organization ?? "",
                    participant2Id = m.User2Id,
                    participant2Name = (m.User2.Firstname + " " + m.User2.Lastname).Trim(),
                    participant2Company = m.User2.Organization ?? "",
                    score = m.Score,
                    matchedAt = m.MatchedAt
                })
                .ToListAsync();

            return Ok(matches);
        }

        // DELETE: api/Matches/reset
        [HttpDelete("reset")]
        public async Task<ActionResult> ResetMatches()
        {
            var companyIdString = User.FindFirst("companyId")?.Value;
            if (string.IsNullOrEmpty(companyIdString) || !Guid.TryParse(companyIdString, out Guid companyId)) return Unauthorized();

            // Scoped cleanup
            var companyMatches = await _context.Matches.Where(m => m.User1.CompanyId == companyId).ToListAsync();
            var matchIds = companyMatches.Select(m => m.Id).ToList();

            var companyChats = await _context.ChatMessages.Where(c => matchIds.Contains(c.MatchId)).ToListAsync();

            _context.ChatMessages.RemoveRange(companyChats);
            _context.Matches.RemoveRange(companyMatches);

            await _context.SaveChangesAsync();
            return Ok(new { message = "Company matches have been cleared." });
        }

        [HttpGet("export")]
        public async Task<IActionResult> ExportMatchesToExcel()
        {
            var companyIdString = User.FindFirst("companyId")?.Value;
            if (string.IsNullOrEmpty(companyIdString) || !Guid.TryParse(companyIdString, out Guid companyId)) return Unauthorized();

            var matches = await _context.Matches
                .Include(m => m.User1)
                .Include(m => m.User2)
                .Where(m => m.User1.CompanyId == companyId)
                .OrderBy(m => m.User1.Firstname)
                .ThenBy(m => m.User1.Lastname)
                .ToListAsync();

            IWorkbook workbook = new XSSFWorkbook();
            ISheet sheet = workbook.CreateSheet("Matches");

            // Header style
            var headerFont = workbook.CreateFont();
            headerFont.IsBold = true;
            var headerStyle = workbook.CreateCellStyle();
            headerStyle.SetFont(headerFont);

            // Header row
            var header = sheet.CreateRow(0);
            var headers = new[] { "#", "User1 Name", "User1 Company", "User2 Name", "User2 Company", "Score", "Status", "User1 Interested", "User2 Interested", "Created At" };
            for (int i = 0; i < headers.Length; i++)
            {
                var cell = header.CreateCell(i);
                cell.SetCellValue(headers[i]);
                cell.CellStyle = headerStyle;
            }

            // Data rows
            for (int i = 0; i < matches.Count; i++)
            {
                var m = matches[i];
                var row = sheet.CreateRow(i + 1);
                row.CreateCell(0).SetCellValue(i + 1);
                row.CreateCell(1).SetCellValue($"{m.User1?.Firstname} {m.User1?.Lastname}".Trim());
                row.CreateCell(2).SetCellValue(m.User1?.Organization ?? "");
                row.CreateCell(3).SetCellValue($"{m.User2?.Firstname} {m.User2?.Lastname}".Trim());
                row.CreateCell(4).SetCellValue(m.User2?.Organization ?? "");
                row.CreateCell(5).SetCellValue(m.Score);
                row.CreateCell(6).SetCellValue(m.Status.ToString());
                row.CreateCell(7).SetCellValue(m.User1Interested ? "Yes" : "No");
                row.CreateCell(8).SetCellValue(m.User2Interested ? "Yes" : "No");
                row.CreateCell(9).SetCellValue(m.CreatedAt.ToString("yyyy-MM-dd HH:mm"));
            }

            // Auto-size columns
            for (int i = 0; i < headers.Length; i++)
                sheet.AutoSizeColumn(i);

            using var ms = new MemoryStream();
            workbook.Write(ms);
            var bytes = ms.ToArray();

            return File(bytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"matches_export_{DateTime.UtcNow:yyyyMMdd}.xlsx");
        }
    }

    public class InterestRequest
    {
        public Guid SourceId { get; set; }
        public Guid TargetId { get; set; }
    }

    public class FeedbackRequest
    {
        public Guid MatchId { get; set; }
        public Guid SourceId { get; set; }
        public int Rating { get; set; } // 1 or -1
        public string? Comment { get; set; }
    }
}
