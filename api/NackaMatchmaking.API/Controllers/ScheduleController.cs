using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NackaMatchmaking.API.Data;
using NackaMatchmaking.API.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NackaMatchmaking.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ScheduleController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public ScheduleController(ApplicationDbContext context)
        {
            _context = context;
        }

        // 1. GET: Get the schedule settings for an event (or create default)
        [HttpGet("event/{eventId}")]
        public async Task<IActionResult> GetEventScheduleSettings(Guid eventId)
        {
            var evt = await _context.Set<MatchmakingEvent>().FirstOrDefaultAsync(e => e.Id == eventId);
            if (evt == null)
            {
                return NoContent();
            }
            return Ok(evt);
        }

        // 2. POST: Update event schedule settings
        [HttpPost("event/{eventId}")]
        public async Task<IActionResult> UpdateEventScheduleSettings(Guid eventId, [FromBody] MatchmakingEvent settings)
        {
            var evt = await _context.Set<MatchmakingEvent>().FirstOrDefaultAsync(e => e.Id == eventId);
            if (evt == null)
            {
                evt = new MatchmakingEvent
                {
                    Id = eventId,
                    Name = settings.Name ?? "Event Name",
                    CompanyId = settings.CompanyId,
                    EventStartTime = settings.EventStartTime,
                    EventEndTime = settings.EventEndTime,
                    SlotDurationMinutes = settings.SlotDurationMinutes > 0 ? settings.SlotDurationMinutes : 30,
                    BreakStartTime = settings.BreakStartTime,
                    BreakEndTime = settings.BreakEndTime,
                    IsActive = true
                };
                _context.Set<MatchmakingEvent>().Add(evt);
            }
            else
            {
                evt.IsActive = settings.IsActive;
                evt.EventStartTime = settings.EventStartTime;
                evt.EventEndTime = settings.EventEndTime;
                evt.SlotDurationMinutes = settings.SlotDurationMinutes > 0 ? settings.SlotDurationMinutes : 30;
                evt.BreakStartTime = settings.BreakStartTime;
                evt.BreakEndTime = settings.BreakEndTime;
            }

            await _context.SaveChangesAsync();
            return Ok(evt);
        }

        // 3. POST: Generate slots for a company
        [HttpPost("generate/event/{eventId}/company/{companyParticipantId}")]
        public async Task<IActionResult> GenerateSlotsForCompany(Guid eventId, Guid companyParticipantId)
        {
            var evt = await _context.Set<MatchmakingEvent>().FirstOrDefaultAsync(e => e.Id == eventId);
            if (evt == null || evt.EventStartTime == null || evt.EventEndTime == null)
                return BadRequest("Event schedule is not configured completely.");

            // Check if slots already exist
            var existingSlots = await _context.MeetingSlots.Where(s => s.MatchmakingEventId == eventId && s.CompanyParticipantId == companyParticipantId).ToListAsync();
            if (existingSlots.Any())
                return BadRequest("Slots already generated for this company.");

            var slots = new List<MeetingSlot>();
            
            // Generate slots based on TimeSpan
            var baseDate = evt.CreatedAt.Date;

            var currentTime = evt.EventStartTime.Value;
            var endTime = evt.EventEndTime.Value;
            var duration = TimeSpan.FromMinutes(evt.SlotDurationMinutes);

            while (currentTime + duration <= endTime)
            {
                // Check if it overlaps with break
                bool isBreak = false;
                if (evt.BreakStartTime.HasValue && evt.BreakEndTime.HasValue)
                {
                    if (currentTime >= evt.BreakStartTime.Value && currentTime < evt.BreakEndTime.Value)
                    {
                        isBreak = true;
                    }
                }

                if (!isBreak)
                {
                    var slot = new MeetingSlot
                    {
                        Id = Guid.NewGuid(),
                        MatchmakingEventId = eventId,
                        CompanyParticipantId = companyParticipantId,
                        StartTime = baseDate.Add(currentTime),
                        EndTime = baseDate.Add(currentTime + duration),
                        IsAvailable = true
                    };
                    slots.Add(slot);
                }

                currentTime += duration;
            }

            _context.MeetingSlots.AddRange(slots);
            await _context.SaveChangesAsync();

            return Ok(slots);
        }

        // 4. GET: Get slots for a specific company
        [HttpGet("event/{eventId}/company/{companyParticipantId}/slots")]
        public async Task<IActionResult> GetCompanySlots(Guid eventId, Guid companyParticipantId)
        {
            var slots = await _context.MeetingSlots
                .Include(s => s.AssignedStudent)
                .Where(s => s.MatchmakingEventId == eventId && s.CompanyParticipantId == companyParticipantId)
                .OrderBy(s => s.StartTime)
                .ToListAsync();

            return Ok(slots);
        }

        // 5. GET: Get all slots (Global View for Helpdesk)
        [HttpGet("event/{eventId}/slots")]
        public async Task<IActionResult> GetAllEventSlots(Guid eventId)
        {
            var companyRoleParticipantIds = await _context.UserAnswers
                .Where(ua => ua.QuestionId == "system_role" && ua.AnswerValue == "Company")
                .Select(ua => ua.ParticipantId)
                .ToListAsync();

            var slots = await _context.MeetingSlots
                .Include(s => s.CompanyParticipant)
                .Include(s => s.AssignedStudent)
                .Where(s => s.MatchmakingEventId == eventId && companyRoleParticipantIds.Contains(s.CompanyParticipantId))
                .OrderBy(s => s.CompanyParticipantId).ThenBy(s => s.StartTime)
                .ToListAsync();

            return Ok(slots);
        }

        // 6. PUT: Assign student to a slot
        [HttpPut("slots/{slotId}/assign/{studentId}")]
        public async Task<IActionResult> AssignStudentToSlot(Guid slotId, Guid studentId)
        {
            var slot = await _context.MeetingSlots.Include(s => s.CompanyParticipant).FirstOrDefaultAsync(s => s.Id == slotId);
            if (slot == null) return NotFound("Slot not found");

            if (!slot.IsAvailable || slot.AssignedStudentId.HasValue)
                return BadRequest("Slot is not available or already booked.");

            var student = await _context.Participants.FindAsync(studentId);
            if (student == null) return NotFound("Student not found");

            slot.AssignedStudentId = studentId;

            // Optional: Insert Chat Message to notify student
            // Find the match between the student and a company representative
            var match = await _context.Matches.FirstOrDefaultAsync(m => 
                (m.User1Id == slot.CompanyParticipantId && m.User2Id == studentId) ||
                (m.User2Id == slot.CompanyParticipantId && m.User1Id == studentId));

            if (match != null)
            {
                var chatMsg = new ChatMessage
                {
                    Id = Guid.NewGuid(),
                    MatchId = match.Id,
                    SenderId = match.User1Id == studentId ? match.User2Id : match.User1Id,
                    Content = $"We would like to meet with you! Your booked time slot is {slot.StartTime.ToString("HH:mm")}.",
                    Timestamp = DateTime.UtcNow
                };
                _context.ChatMessages.Add(chatMsg);
            }

            await _context.SaveChangesAsync();
            return Ok(slot);
        }

        // 7. PUT: Toggle availability
        [HttpPut("slots/{slotId}/availability")]
        public async Task<IActionResult> ToggleSlotAvailability(Guid slotId, [FromBody] bool isAvailable)
        {
            var slot = await _context.MeetingSlots.FindAsync(slotId);
            if (slot == null) return NotFound();

            slot.IsAvailable = isAvailable;
            await _context.SaveChangesAsync();
            return Ok(slot);
        }

        // 8. PUT: Mark No-Show
        [HttpPut("slots/{slotId}/noshow")]
        public async Task<IActionResult> MarkNoShow(Guid slotId, [FromBody] bool isNoShow)
        {
            var slot = await _context.MeetingSlots.FindAsync(slotId);
            if (slot == null) return NotFound();

            slot.CompanyMarkedNoShow = isNoShow;
            await _context.SaveChangesAsync();
            return Ok(slot);
        }

        // 9. PUT: Student Decline
        [HttpPut("slots/{slotId}/decline")]
        public async Task<IActionResult> DeclineSlot(Guid slotId)
        {
            var slot = await _context.MeetingSlots.FindAsync(slotId);
            if (slot == null) return NotFound();

            slot.StudentDeclined = true;
            slot.AssignedStudentId = null; // Free up the slot
            await _context.SaveChangesAsync();
            return Ok(slot);
        }

        // 10. PUT: Student Check-In
        [HttpPut("slots/{slotId}/checkin")]
        public async Task<IActionResult> CheckInSlot(Guid slotId)
        {
            var slot = await _context.MeetingSlots.FindAsync(slotId);
            if (slot == null) return NotFound();

            slot.StudentCheckedIn = true;
            await _context.SaveChangesAsync();
            return Ok(slot);
        }

        // 11. PUT: Unassign Student (Helpdesk)
        [HttpPut("slots/{slotId}/unassign")]
        public async Task<IActionResult> UnassignStudentFromSlot(Guid slotId)
        {
            var slot = await _context.MeetingSlots.FindAsync(slotId);
            if (slot == null) return NotFound();

            slot.AssignedStudentId = null;
            slot.StudentCheckedIn = false;
            slot.StudentDeclined = false;
            slot.CompanyMarkedNoShow = false;

            await _context.SaveChangesAsync();
            return Ok(slot);
        }
    }
}
