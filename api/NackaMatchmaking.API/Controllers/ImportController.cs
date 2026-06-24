using Microsoft.AspNetCore.Mvc;
using NackaMatchmaking.API.Data;
using NackaMatchmaking.API.Models;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
using Microsoft.EntityFrameworkCore;

namespace NackaMatchmaking.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Microsoft.AspNetCore.Authorization.Authorize]
    public class ImportController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<ImportController> _logger;

        public ImportController(ApplicationDbContext context, ILogger<ImportController> logger)
        {
            _context = context;
            _logger = logger;
        }
        [HttpGet("template")]
        public IActionResult GetTemplate()
        {
            try
            {
                IWorkbook workbook = new XSSFWorkbook();
                ISheet sheet = workbook.CreateSheet("Registrations");

                // Headers
                IRow headerRow = sheet.CreateRow(0);
                headerRow.CreateCell(0).SetCellValue("Firstname");
                headerRow.CreateCell(1).SetCellValue("Lastname");
                headerRow.CreateCell(2).SetCellValue("Organization");
                headerRow.CreateCell(3).SetCellValue("Title");
                headerRow.CreateCell(4).SetCellValue("Email");

                // Example Row
                IRow exampleRow = sheet.CreateRow(1);
                exampleRow.CreateCell(0).SetCellValue("John");
                exampleRow.CreateCell(1).SetCellValue("Doe");
                exampleRow.CreateCell(2).SetCellValue("Example AB");
                exampleRow.CreateCell(3).SetCellValue("CEO");
                exampleRow.CreateCell(4).SetCellValue("john.doe@example.com");

                // Auto-size columns
                for (int i = 0; i < 5; i++)
                {
                    sheet.AutoSizeColumn(i);
                }

                using (var ms = new MemoryStream())
                {
                    workbook.Write(ms);
                    var bytes = ms.ToArray();
                    return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "registration_template.xlsx");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating template.");
                return StatusCode(500, "Error generating template.");
            }
        }

        [HttpPost("upload")]
        public async Task<IActionResult> Upload(IFormFile file)
        {
            var companyIdString = User.FindFirst("companyId")?.Value;
            if (string.IsNullOrEmpty(companyIdString) || !Guid.TryParse(companyIdString, out Guid companyId)) return Unauthorized();

            if (file == null || file.Length == 0)
                return BadRequest("No file uploaded.");

            if (!file.FileName.EndsWith(".xlsx"))
                return BadRequest("Only .xlsx files are supported.");

            var newRegistrations = new List<Registration>();
            var updatedCount = 0;
            var newCount = 0;

            try
            {
                using (var stream = file.OpenReadStream())
                {
                    IWorkbook workbook = new XSSFWorkbook(stream);
                    ISheet sheet = workbook.GetSheetAt(0);

                    var parsedRows = new List<Registration>();

                    // Assume first row is header
                    for (int i = 1; i <= sheet.LastRowNum; i++)
                    {
                        IRow row = sheet.GetRow(i);
                        if (row == null) continue;

                        var email = GetCellValue(row.GetCell(4));
                        var firstname = GetCellValue(row.GetCell(0));

                        // Basic validation: ignore if no email or name
                        if (string.IsNullOrWhiteSpace(email) && string.IsNullOrWhiteSpace(firstname))
                             continue;

                        var reg = new Registration
                        {
                            Firstname = firstname,
                            Lastname = GetCellValue(row.GetCell(1)),
                            Organization = GetCellValue(row.GetCell(2)),
                            Title = GetCellValue(row.GetCell(3)),
                            Email = email,
                            CompanyId = companyId,
                            CreatedAt = DateTime.UtcNow
                        };
                        parsedRows.Add(reg);
                    }

                    if (!parsedRows.Any())
                        return Ok(new { message = "No valid records found to import." });

                    // 1. Get all emails from the imported list to minimize DB hits
                    var emailsStart = parsedRows.Where(r => !string.IsNullOrEmpty(r.Email)).Select(r => r.Email).ToList();
                    
                    // 2. Fetch existing records from DB (scoped by company)
                    var existingRecords = _context.Registrations
                        .Where(r => r.CompanyId == companyId && emailsStart.Contains(r.Email))
                        .ToList();

                    foreach (var parsed in parsedRows)
                    {
                        // Try to find by Email
                        var existing = existingRecords.FirstOrDefault(r => r.Email != null && r.Email.Equals(parsed.Email, StringComparison.OrdinalIgnoreCase));

                        if (existing != null)
                        {
                            // Update existing Registration
                            existing.Firstname = parsed.Firstname;
                            existing.Lastname = parsed.Lastname;
                            existing.Organization = parsed.Organization;
                            existing.Title = parsed.Title;

                            updatedCount++;
                        }
                        else
                        {
                            // Add new Registration
                            newRegistrations.Add(parsed);
                            newCount++;
                        }
                    }

                    if (newRegistrations.Any())
                    {
                        await _context.Registrations.AddRangeAsync(newRegistrations);
                    }

                    await _context.SaveChangesAsync();
                    
                    _logger.LogInformation($"Import complete. Added: {newCount}, Updated: {updatedCount}");
                    return Ok(new { message = $"Import complete. Added {newCount} new, Updated {updatedCount} existing records." });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error importing file.");
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        [HttpPost("matches")]
        public async Task<IActionResult> UploadMatches(IFormFile file)
        {
            var companyIdString = User.FindFirst("companyId")?.Value;
            if (string.IsNullOrEmpty(companyIdString) || !Guid.TryParse(companyIdString, out Guid companyId)) return Unauthorized();

            if (file == null || file.Length == 0)
                return BadRequest("No file uploaded.");

            var isXlsx = file.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase);
            var isCsv = file.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase);

            if (!isXlsx && !isCsv)
                return BadRequest("Only .xlsx and .csv files are supported.");

            int addedCount = 0;
            int updatedCount = 0;
            int removedCount = 0;
            var matchesToImport = new List<UserMatch>();
            var errorRows = new List<string>();
            var processedPairs = new HashSet<(Guid, Guid)>();

            try
            {
                // Pre-fetch existing matches with normalization (scoped by company participants potentially, but let's just filter by any match involving our company's participants)
                // Actually UserMatch doesn't have CompanyId, so we join with Participants to ensure isolation
                var existingMatches = await _context.Matches
                    .Where(m => m.User1.CompanyId == companyId && m.User2.CompanyId == companyId)
                    .Select(m => new { m.User1Id, m.User2Id })
                    .ToListAsync();
                var existingSet = new HashSet<(Guid, Guid)>(existingMatches.Select(m => 
                {
                    var u1 = m.User1Id.CompareTo(m.User2Id) < 0 ? m.User1Id : m.User2Id;
                    var u2 = m.User1Id.CompareTo(m.User2Id) < 0 ? m.User2Id : m.User1Id;
                    return (u1, u2);
                }));

                using (var stream = file.OpenReadStream())
                {
                    if (isXlsx)
                    {
                        IWorkbook workbook = new XSSFWorkbook(stream);
                        ISheet sheet = workbook.GetSheetAt(0);

                        for (int i = 1; i <= sheet.LastRowNum; i++)
                        {
                            IRow row = sheet.GetRow(i);
                            if (row == null) continue;

                            var u1IdStr = GetCellValue(row.GetCell(0));
                            var u2IdStr = GetCellValue(row.GetCell(1));
                            var scoreStr = GetCellValue(row.GetCell(2));

                            if (string.IsNullOrWhiteSpace(u1IdStr) || string.IsNullOrWhiteSpace(u2IdStr))
                                continue;

                            if (Guid.TryParse(u1IdStr, out Guid u1Id) && Guid.TryParse(u2IdStr, out Guid u2Id))
                            {
                                // Enforce ordering convention
                                var id1 = u1Id.CompareTo(u2Id) < 0 ? u1Id : u2Id;
                                var id2 = u1Id.CompareTo(u2Id) < 0 ? u2Id : u1Id;

                                int.TryParse(scoreStr, out int score);

                                // Check if this pair was already seen in THIS BATCH
                                if (processedPairs.Contains((id1, id2)))
                                    continue;

                                // Check if this pair exists in THE DATABASE (try both orders to be extremely safe, though normalization should handle it)
                                if (existingSet.Contains((id1, id2)))
                                {
                                    // Update existing match's score
                                    var existingMatch = await _context.Matches.FirstOrDefaultAsync(m => 
                                        m.User1.CompanyId == companyId && m.User2.CompanyId == companyId &&
                                        ((m.User1Id == id1 && m.User2Id == id2) || 
                                         (m.User1Id == id2 && m.User2Id == id1)));
                                    if (existingMatch != null)
                                    {
                                        existingMatch.Score = score;
                                        updatedCount++;
                                    }
                                }
                                else
                                {
                                    matchesToImport.Add(new UserMatch
                                    {
                                        Id = Guid.NewGuid(),
                                        User1Id = id1,
                                        User2Id = id2,
                                        Score = score,
                                        Status = MatchStatus.Proposed,
                                        CreatedAt = DateTime.UtcNow
                                    });
                                    addedCount++;
                                }
                                processedPairs.Add((id1, id2));
                            }
                            else
                            {
                                errorRows.Add($"Row {i + 1}: Invalid GUIDs.");
                            }
                        }
                    }
                    else if (isCsv)
                    {
                        using (var reader = new StreamReader(stream))
                        {
                            await reader.ReadLineAsync(); // Skip header
                            int rowNum = 1;
                            string? line;
                            while ((line = await reader.ReadLineAsync()) != null)
                            {
                                rowNum++;
                                if (string.IsNullOrWhiteSpace(line)) continue;

                                var parts = line.Split(',');
                                if (parts.Length < 2) continue;

                                var u1IdStr = parts[0].Trim().Trim('"');
                                var u2IdStr = parts[1].Trim().Trim('"');
                                var scoreStr = parts.Length > 2 ? parts[2].Trim().Trim('"') : "0";

                                if (Guid.TryParse(u1IdStr, out Guid u1Id) && Guid.TryParse(u2IdStr, out Guid u2Id))
                                {
                                    // Enforce ordering convention
                                    var id1 = u1Id.CompareTo(u2Id) < 0 ? u1Id : u2Id;
                                    var id2 = u1Id.CompareTo(u2Id) < 0 ? u2Id : u1Id;

                                    int.TryParse(scoreStr, out int score);

                                    if (processedPairs.Contains((id1, id2)))
                                        continue;

                                    if (existingSet.Contains((id1, id2)))
                                    {
                                        var existingMatch = await _context.Matches.FirstOrDefaultAsync(m => 
                                            m.User1.CompanyId == companyId && m.User2.CompanyId == companyId &&
                                            ((m.User1Id == id1 && m.User2Id == id2) || 
                                             (m.User1Id == id2 && m.User2Id == id1)));
                                        if (existingMatch != null)
                                        {
                                            existingMatch.Score = score;
                                            updatedCount++;
                                        }
                                    }
                                    else
                                    {
                                        matchesToImport.Add(new UserMatch
                                        {
                                            Id = Guid.NewGuid(),
                                            User1Id = id1,
                                            User2Id = id2,
                                            Score = score,
                                            Status = MatchStatus.Proposed,
                                            CreatedAt = DateTime.UtcNow
                                        });
                                        addedCount++;
                                    }
                                    processedPairs.Add((id1, id2));
                                }
                                else
                                {
                                    errorRows.Add($"Row {rowNum}: Invalid GUIDs.");
                                }
                            }
                        }
                    }

                    // 1. Collect all participants involved in this import
                    var participantsInFile = processedPairs
                        .SelectMany(p => new[] { p.Item1, p.Item2 })
                        .Distinct()
                        .ToList();

                    // 2. Remove any EXISTING 'Proposed' or 'Pending' matches for these participants that are NOT in the imported set
                    // This ensures the import file is the "source of truth" for these users
                    var matchesToRemove = await _context.Matches
                        .Where(m => m.User1.CompanyId == companyId && m.User2.CompanyId == companyId &&
                                   (m.Status == MatchStatus.Proposed || m.Status == MatchStatus.Pending) && 
                                   (participantsInFile.Contains(m.User1Id) || participantsInFile.Contains(m.User2Id)))
                        .ToListAsync();

                    var toDelete = matchesToRemove
                        .Where(m => {
                            var id1 = m.User1Id.CompareTo(m.User2Id) < 0 ? m.User1Id : m.User2Id;
                            var id2 = m.User1Id.CompareTo(m.User2Id) < 0 ? m.User2Id : m.User1Id;
                            return !processedPairs.Contains((id1, id2));
                        })
                        .ToList();

                    if (toDelete.Any())
                    {
                        _context.Matches.RemoveRange(toDelete);
                        removedCount = toDelete.Count;
                    }

                    // 3. Save updates and Prepare new matches
                    await _context.SaveChangesAsync();

                    if (!matchesToImport.Any() && processedPairs.Count == 0)
                        return BadRequest(new { message = "No valid match records found.", errors = errorRows });

                    // Validate that all NEW participant IDs actually exist in the DB
                    if (matchesToImport.Any())
                    {
                        var allIds = matchesToImport
                            .SelectMany(m => new[] { m.User1Id, m.User2Id })
                            .Distinct()
                            .ToList();
                        var validIds = await _context.Participants
                            .Where(p => p.CompanyId == companyId && allIds.Contains(p.Id))
                            .Select(p => p.Id)
                            .ToListAsync();
                        var validIdSet = new HashSet<Guid>(validIds);

                        var invalidIdPairs = matchesToImport
                            .Where(m => !validIdSet.Contains(m.User1Id) || !validIdSet.Contains(m.User2Id))
                            .ToList();

                        foreach (var inv in invalidIdPairs)
                        {
                            var missing = !validIdSet.Contains(inv.User1Id) ? inv.User1Id.ToString() : inv.User2Id.ToString();
                            errorRows.Add($"Match skipped: Participant ID {missing} not found in database.");
                        }

                        var validMatches = matchesToImport
                            .Where(m => validIdSet.Contains(m.User1Id) && validIdSet.Contains(m.User2Id))
                            .ToList();

                        if (validMatches.Any())
                        {
                            await _context.Matches.AddRangeAsync(validMatches);
                            await _context.SaveChangesAsync();
                        }
                    }

                    return Ok(new 
                    { 
                        message = $"Match import complete. Summary: {addedCount} new matches added, {updatedCount} existing matches updated, {removedCount} old matches removed.", 
                        errors = errorRows.Any() ? errorRows : null,
                        summary = new { added = addedCount, updated = updatedCount, removed = removedCount }
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error importing matches.");
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        [HttpPost("map-matches")]
        public async Task<IActionResult> MapMatches(IFormFile file)
        {
            var companyIdString = User.FindFirst("companyId")?.Value;
            if (string.IsNullOrEmpty(companyIdString) || !Guid.TryParse(companyIdString, out Guid companyId)) return Unauthorized();

            if (file == null || file.Length == 0)
                return BadRequest("No file uploaded.");

            if (!file.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
                return BadRequest("Only .csv files are supported.");

            var rows = new List<string>();
            var unresolved = new List<string>();
            var processedPairs = new HashSet<(Guid, Guid)>();

            try
            {
                var participants = await _context.Participants.Where(p => p.CompanyId == companyId).ToListAsync();

                using (var stream = file.OpenReadStream())
                using (var reader = new StreamReader(stream, System.Text.Encoding.GetEncoding(1252), detectEncodingFromByteOrderMarks: true))
                {
                    await reader.ReadLineAsync(); // Skip header
                    int rowNum = 1;

                    string? line;
                    while ((line = await reader.ReadLineAsync()) != null)
                    {
                        rowNum++;
                        if (string.IsNullOrWhiteSpace(line)) continue;

                        var parts = ParseCsvLine(line);
                        // The Excel export has P1 fields [0-17], P2 fields [18-35], Score [36], Reasoning [37]
                        if (parts.Count < 37) continue;

                        var personName = parts[0].Trim();
                        var personTitle = parts[10].Trim(); // Job Title is at index 10 in the profile block
                        var matchName = parts[18].Trim();
                        var matchTitle = parts[28].Trim(); // P2 Title is at 18 + 10 = 28
                        var scoreStr = parts[36].Trim();

                        if (string.IsNullOrEmpty(personName) || string.IsNullOrEmpty(matchName))
                            continue;

                        var p1 = ResolveParticipantWithTitle(participants, personName, personTitle);
                        var p2 = ResolveParticipantWithTitle(participants, matchName, matchTitle);

                        if (p1 != null && p2 != null)
                        {
                            var id1 = p1.Id.CompareTo(p2.Id) < 0 ? p1.Id : p2.Id;
                            var id2 = p1.Id.CompareTo(p2.Id) < 0 ? p2.Id : p1.Id;

                            if (processedPairs.Contains((id1, id2)))
                                continue;

                            int.TryParse(scoreStr, out int score);
                            rows.Add($"{id1},{id2},{score}");
                            processedPairs.Add((id1, id2));
                        }
                        else
                        {
                            var missing = p1 == null ? $"{personName} ({personTitle})" : $"{matchName} ({matchTitle})";
                            unresolved.Add(missing);
                        }
                    }
                }

                if (!rows.Any())
                    return BadRequest(new { message = "No matches could be resolved.", unresolved });

                var csvLines = new List<string> { "User1Id,User2Id,Score" };
                csvLines.AddRange(rows);
                var csvContent = string.Join("\n", csvLines);
                var bytes = System.Text.Encoding.UTF8.GetBytes(csvContent);

                _logger.LogInformation($"Mapped {rows.Count} unique matches. {unresolved.Count} unresolved.");

                return File(bytes, "text/csv", $"matches_mapped_{DateTime.UtcNow:yyyyMMdd}.csv");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error mapping matches.");
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        private Participant? ResolveParticipantWithTitle(List<Participant> participants, string fullName, string jobTitle)
        {
            // Try exact name + title match first
            if (!string.IsNullOrEmpty(jobTitle))
            {
                var exact = participants.FirstOrDefault(p =>
                    $"{p.Firstname} {p.Lastname}".Equals(fullName, StringComparison.OrdinalIgnoreCase) &&
                    (p.Title ?? "").Equals(jobTitle, StringComparison.OrdinalIgnoreCase));
                if (exact != null) return exact;
            }

            // Fallback: name only
            return participants.FirstOrDefault(p =>
                $"{p.Firstname} {p.Lastname}".Equals(fullName, StringComparison.OrdinalIgnoreCase));
        }

        [HttpPost("ai-matches")]
        public async Task<IActionResult> UploadAiMatches(IFormFile file)
        {
            var companyIdString = User.FindFirst("companyId")?.Value;
            if (string.IsNullOrEmpty(companyIdString) || !Guid.TryParse(companyIdString, out Guid companyId)) return Unauthorized();

            if (file == null || file.Length == 0)
                return BadRequest("No file uploaded.");

            if (!file.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
                return BadRequest("Only .csv files are supported for AI matches.");

            var matchesToImport = new List<UserMatch>();
            var errors = new List<string>();
            var processedPairs = new HashSet<(Guid, Guid)>();

            try
            {
                // 1. Pre-fetch all participants to resolve names (scoped by company)
                var participants = await _context.Participants.Where(p => p.CompanyId == companyId).ToListAsync();
                
                // 2. Pre-fetch existing matches to avoid duplicates
                var existingMatches = await _context.Matches
                    .Where(m => m.User1.CompanyId == companyId && m.User2.CompanyId == companyId)
                    .Select(m => new { m.User1Id, m.User2Id })
                    .ToListAsync();
                var existingSet = new HashSet<(Guid, Guid)>(existingMatches.Select(m => (m.User1Id, m.User2Id)));

                using (var stream = file.OpenReadStream())
                using (var reader = new StreamReader(stream, System.Text.Encoding.GetEncoding(1252), detectEncodingFromByteOrderMarks: true))
                {
                    // Skip header
                    var header = await reader.ReadLineAsync();
                    int rowNum = 1;

                    string? line;
                    while ((line = await reader.ReadLineAsync()) != null)
                    {
                        rowNum++;
                        if (string.IsNullOrWhiteSpace(line)) continue;

                        var parts = ParseCsvLine(line);
                        if (parts.Count < 7) continue;

                        var personName = parts[0].Trim();
                        var matchName = parts[6].Trim();
                        var scoreStr = parts.Count > 9 ? parts[9].Trim() : "0";

                        if (string.IsNullOrEmpty(personName) || string.IsNullOrEmpty(matchName))
                            continue;

                        // Resolve IDs by name
                        var p1 = ResolveParticipant(participants, personName);
                        var p2 = ResolveParticipant(participants, matchName);

                        if (p1 != null && p2 != null)
                        {
                            // Enforce User1Id < User2Id convention
                            var id1 = p1.Id.CompareTo(p2.Id) < 0 ? p1.Id : p2.Id;
                            var id2 = p1.Id.CompareTo(p2.Id) < 0 ? p2.Id : p1.Id;

                            // Skip if already processed in this batch OR already exists in DB
                            if (processedPairs.Contains((id1, id2)) || existingSet.Contains((id1, id2)))
                            {
                                continue;
                            }

                            int.TryParse(scoreStr, out int score);
                            matchesToImport.Add(new UserMatch
                            {
                                Id = Guid.NewGuid(),
                                User1Id = id1,
                                User2Id = id2,
                                Score = score,
                                Status = MatchStatus.Proposed,
                                CreatedAt = DateTime.UtcNow
                            });

                            processedPairs.Add((id1, id2));
                        }
                        else
                        {
                            var missing = p1 == null ? personName : matchName;
                            errors.Add($"Row {rowNum}: Could not resolve '{missing}' to a participant.");
                        }
                    }
                }

                if (!matchesToImport.Any())
                    return BadRequest(new { message = "No matches could be resolved.", errors });

                // Optional: Clear existing proposed matches first? 
                // Or just add new ones. The user might want to overwrite or append.
                // For now, let's append.

                await _context.Matches.AddRangeAsync(matchesToImport);
                await _context.SaveChangesAsync();

                return Ok(new 
                { 
                    message = $"Successfully imported {matchesToImport.Count} AI-enriched matches.", 
                    errors = errors.Any() ? errors : null 
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error importing AI matches.");
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        private Participant? ResolveParticipant(List<Participant> participants, string fullName)
        {
            // Try exact match first
            var match = participants.FirstOrDefault(p => 
                $"{p.Firstname} {p.Lastname}".Equals(fullName, StringComparison.OrdinalIgnoreCase));
            
            if (match == null)
            {
                // Try fuzzy/partial if needed, but exact is safer for CSV imports
                // Let's stick with exact for now.
            }

            return match;
        }

        private List<string> ParseCsvLine(string line)
        {
            var result = new List<string>();
            var current = new System.Text.StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];

                if (c == '"')
                {
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        current.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                    }
                }
                else if (c == ',' && !inQuotes)
                {
                    result.Add(current.ToString().Trim());
                    current.Clear();
                }
                else
                {
                    current.Append(c);
                }
            }
            result.Add(current.ToString().Trim());

            return result;
        }

        private string? GetCellValue(ICell? cell)
        {
            if (cell == null) return null;
            
            switch (cell.CellType)
            {
                case CellType.String:
                    return cell.StringCellValue?.Trim();
                case CellType.Numeric:
                    return cell.NumericCellValue.ToString();
                case CellType.Boolean:
                    return cell.BooleanCellValue.ToString();
                default:
                    return cell.ToString()?.Trim();
            }
        }
        [Microsoft.AspNetCore.Authorization.AllowAnonymous]
        [HttpGet("debug-matches/{id}")]
        public async Task<IActionResult> DebugMatches(Guid id)
        {
            var companyIdString = User.FindFirst("companyId")?.Value;
            if (string.IsNullOrEmpty(companyIdString) || !Guid.TryParse(companyIdString, out Guid companyId)) return Unauthorized();

            var p = await _context.Participants.FirstOrDefaultAsync(x => x.Id == id && x.CompanyId == companyId);
            if (p == null) return NotFound();

            var matches = await _context.Matches
                .Include(m => m.User1)
                .Include(m => m.User2)
                .Where(m => (m.User1Id == id || m.User2Id == id) && m.User1.CompanyId == companyId)
                .ToListAsync();

            var result = matches.Select(m => new {
                m.Id,
                Status = m.Status.ToString(),
                StatusInt = (int)m.Status,
                m.Score,
                User1 = new { m.User1Id, m.User1.Firstname, m.User1.Lastname },
                User2 = new { m.User2Id, m.User2.Firstname, m.User2.Lastname },
                IsMe = m.User1Id == id ? "User1" : "User2"
            }).ToList();

            return Ok(new { 
                Participant = p != null ? $"{p.Firstname} {p.Lastname}" : "Unknown",
                MatchCount = result.Count,
                Matches = result 
            });
        }
    }
}
