using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NackaMatchmaking.API.Data;
using NackaMatchmaking.API.Models;

namespace NackaMatchmaking.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class QuestionsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public QuestionsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: api/Questions
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Question>>> GetQuestions([FromQuery] Guid companyId, [FromQuery] bool includeHidden = false)
        {
            if (companyId == Guid.Empty) return BadRequest("companyId is required");

            var query = _context.Questions
                .Where(q => q.CompanyId == companyId && q.Type != "System")
                .Include(q => q.Options.OrderBy(o => o.Order))
                .AsQueryable();

            if (!includeHidden)
            {
                // Filter hidden questions AND hidden options
                query = query.Where(q => !q.IsHidden);
            }

            var questions = await query.OrderBy(q => q.Order).ToListAsync();

            if (!includeHidden)
            {
                // Pending: EF Core 8+ can filter included collections directly, but for safety in older versions we do client-side filtering of options
                foreach (var q in questions)
                {
                    q.Options = q.Options.Where(o => !o.IsHidden).ToList();
                }
            }

            return questions;
        }

        // POST: api/Questions
        [HttpPost]
        [Microsoft.AspNetCore.Authorization.Authorize]
        public async Task<ActionResult<Question>> PostQuestion(Question question)
        {
            var companyIdString = User.FindFirst("companyId")?.Value;
            if (string.IsNullOrEmpty(companyIdString) || !Guid.TryParse(companyIdString, out Guid companyId)) return Unauthorized();

            question.CompanyId = companyId;
            foreach (var opt in question.Options)
            {
                opt.CompanyId = companyId;
            }

            if (QuestionExists(companyId, question.Id))
            {
                return Conflict("Question ID already exists.");
            }

            _context.Questions.Add(question);
            await _context.SaveChangesAsync();

            return CreatedAtAction("GetQuestions", new { id = question.Id, companyId = companyId }, question);
        }

        // DELETE: api/Questions/q1
        [HttpDelete("{id}")]
        [Microsoft.AspNetCore.Authorization.Authorize]
        public async Task<IActionResult> DeleteQuestion(string id)
        {
            var companyIdString = User.FindFirst("companyId")?.Value;
            if (string.IsNullOrEmpty(companyIdString) || !Guid.TryParse(companyIdString, out Guid companyId)) return Unauthorized();

            var question = await _context.Questions.FindAsync(companyId, id);
            if (question == null)
            {
                return NotFound();
            }

            _context.Questions.Remove(question);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        // PUT: api/Questions/q1
        [HttpPut("{id}")]
        [Microsoft.AspNetCore.Authorization.Authorize] // Only admins should update questions
        public async Task<IActionResult> PutQuestion(string id, Question question)
        {
            if (id != question.Id)
            {
                return BadRequest();
            }

            var companyIdString = User.FindFirst("companyId")?.Value;
            if (string.IsNullOrEmpty(companyIdString) || !Guid.TryParse(companyIdString, out Guid companyId)) return Unauthorized();

            // Update Question details
            var existingQuestion = await _context.Questions
                .Include(q => q.Options)
                .FirstOrDefaultAsync(q => q.CompanyId == companyId && q.Id == id);

            if (existingQuestion == null)
            {
                return NotFound();
            }

            existingQuestion.Title = question.Title;
            existingQuestion.Description = question.Description;
            existingQuestion.Placeholder = question.Placeholder;
            existingQuestion.MaxLength = question.MaxLength;
            existingQuestion.IsHidden = question.IsHidden;
            existingQuestion.Type = question.Type;
            existingQuestion.TargetRole = question.TargetRole;

            // Update Options
            // 1. Handle Updates and Adds
            foreach (var option in question.Options)
            {
                if (option.Id != 0)
                {
                    var existingOption = existingQuestion.Options.FirstOrDefault(o => o.Id == option.Id);
                    if (existingOption != null)
                    {
                        // Update existing
                        existingOption.Title = option.Title;
                        existingOption.Description = option.Description;
                        existingOption.Icon = option.Icon;
                        existingOption.Value = option.Value;
                        existingOption.Order = option.Order;
                        existingOption.IsHidden = option.IsHidden;
                    }
                }
                else
                {
                    // Add new (Id is 0)
                    option.CompanyId = companyId;
                    existingQuestion.Options.Add(option);
                }
            }

            // 2. Handle Deletes
            // Find options in DB that are NOT in the incoming list
            var optionsKeepIds = question.Options.Select(o => o.Id).ToList();
            var optionsToDelete = existingQuestion.Options
                .Where(o => o.Id != 0 && !optionsKeepIds.Contains(o.Id))
                .ToList();

            foreach (var opt in optionsToDelete)
            {
                _context.Remove(opt);
            }

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!QuestionExists(companyId, id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return NoContent();
        }

        // POST: api/Questions/reorder
        [HttpPost("reorder")]
        [Microsoft.AspNetCore.Authorization.Authorize]
        public async Task<IActionResult> ReorderQuestions([FromBody] List<QuestionOrderDto> orders)
        {
            if (orders == null || !orders.Any()) return BadRequest("No order data provided.");

            var companyIdString = User.FindFirst("companyId")?.Value;
            if (string.IsNullOrEmpty(companyIdString) || !Guid.TryParse(companyIdString, out Guid companyId)) return Unauthorized();

            var questionIds = orders.Select(o => o.Id).ToList();
            var questions = await _context.Questions.Where(q => q.CompanyId == companyId && questionIds.Contains(q.Id)).ToListAsync();

            foreach (var orderDto in orders)
            {
                var question = questions.FirstOrDefault(q => q.Id == orderDto.Id);
                if (question != null)
                {
                    question.Order = orderDto.Order;
                }
            }

            await _context.SaveChangesAsync();
            return NoContent();
        }

        private bool QuestionExists(Guid companyId, string id)
        {
            return _context.Questions.Any(e => e.CompanyId == companyId && e.Id == id);
        }

        public class QuestionOrderDto
        {
            public string Id { get; set; } = null!;
            public int Order { get; set; }
        }
    }
}
