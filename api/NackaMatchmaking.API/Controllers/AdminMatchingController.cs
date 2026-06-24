using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
using NackaMatchmaking.API.Services;
using NackaMatchmaking.API.Data;

namespace NackaMatchmaking.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AdminMatchingController : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly ILogger<AdminMatchingController> _logger;
        private readonly TaskProgressService _progressService;
        private readonly ApplicationDbContext _context;

        public AdminMatchingController(
            IHttpClientFactory httpClientFactory, 
            IConfiguration configuration, 
            ILogger<AdminMatchingController> logger,
            TaskProgressService progressService,
            ApplicationDbContext context)
        {
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _logger = logger;
            _progressService = progressService;
            _context = context;
        }

        [HttpPost("match-csv")]
        [Microsoft.AspNetCore.Authorization.Authorize]
        public async Task<IActionResult> MatchFromCsv(IFormFile file)
        {
            var companyIdString = User.FindFirst("companyId")?.Value;
            if (string.IsNullOrEmpty(companyIdString) || !Guid.TryParse(companyIdString, out Guid companyId)) return Unauthorized();

            if (file == null || file.Length == 0)
                return BadRequest("No file uploaded.");

            if (!file.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
                return BadRequest("Only .csv files are supported.");

            var taskKey = "matching_" + companyId;
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(HttpContext.RequestAborted, _progressService.StartTask(taskKey));
            var ct = cts.Token;
            _progressService.ResetProgress(taskKey);
            var profiles = new List<CsvProfile>();

            try
            {
                // Use Windows-1252 as fallback for Excel-exported CSVs without UTF-8 BOM
                // detectEncodingFromByteOrderMarks: true handles UTF-8 with BOM automatically
                var encoding = Encoding.GetEncoding(1252);
                using (var reader = new StreamReader(file.OpenReadStream(), encoding, detectEncodingFromByteOrderMarks: true))
                {
                    // Skip header row
                    var headerLine = await reader.ReadLineAsync();
                    _logger.LogInformation($"CSV Header: {headerLine}");

                    int rowCount = 0;
                    string? line;
                    while ((line = await reader.ReadLineAsync(ct)) != null)
                    {
                        rowCount++;
                        if (string.IsNullOrWhiteSpace(line)) continue;

                        var values = ParseCsvLine(line);
                        if (values.Count < 18)
                        {
                            _logger.LogWarning($"Row {rowCount} skipped. Columns: {values.Count}");
                            continue;
                        }

                        // Map ALL CSV columns to a rich profile object
                        // Col 0:  Full Name
                        // Col 1:  Main goal for attending
                        // Col 2:  How they prefer to meet
                        // Col 3:  Skills / competences
                        // Col 4:  General interests
                        // Col 5:  Age
                        // Col 6:  Gender
                        // Col 7:  Languages spoken
                        // Col 8:  Personality focus
                        // Col 9:  Location (City, Country)
                        // Col 10: Job title
                        // Col 11: Education level
                        // Col 12: Seniority
                        // Col 13: Industry
                        // Col 14: Organization type
                        // Col 15: Company size
                        // Col 16: Company stage
                        // Col 17: Bio / background story

                        profiles.Add(new CsvProfile
                        {
                            Id = Guid.NewGuid(),
                            FullName = values[0],
                            Goals = values[1],
                            MeetingPreference = values[2],
                            Skills = values[3],
                            Interests = values[4],
                            Age = values[5],
                            Gender = values[6],
                            Languages = values[7],
                            Personality = values[8],
                            Location = values[9],
                            JobTitle = values[10],
                            Education = values[11],
                            Seniority = values[12],
                            Industry = values[13],
                            OrganizationType = values[14],
                            CompanySize = values[15],
                            CompanyStage = values[16],
                            Bio = values[17]
                        });
                    }
                }

                _logger.LogInformation($"CSV Parsed. Found {profiles.Count} valid profiles.");

                if (!profiles.Any())
                    return BadRequest("No valid participants found in CSV.");

                // Run AI Matching with batched approach
                var suggestions = await GetCsvMatchSuggestionsAsync(companyId, taskKey, profiles, ct);
                _logger.LogInformation($"AI returned {suggestions.Count} match suggestions.");

                // Generate Excel
                using (var memoryStream = new MemoryStream())
                {
                    IWorkbook workbook = new XSSFWorkbook();
                    ISheet sheet = workbook.CreateSheet("Matches");

                    // Style header row
                    var headerStyle = workbook.CreateCellStyle();
                    var headerFont = workbook.CreateFont();
                    headerFont.IsBold = true;
                    headerStyle.SetFont(headerFont);

                    IRow headerRow = sheet.CreateRow(0);
                    string[] headers =
                    {
                        // Person 1
                        "Person 1 Name", "Person 1 Goals", "Person 1 Meeting Preference",
                        "Person 1 Skills", "Person 1 Interests", "Person 1 Age", "Person 1 Gender",
                        "Person 1 Languages", "Person 1 Personality", "Person 1 Location",
                        "Person 1 Job Title", "Person 1 Education", "Person 1 Seniority",
                        "Person 1 Industry", "Person 1 Organization Type", "Person 1 Company Size",
                        "Person 1 Company Stage", "Person 1 Bio",
                        // Person 2
                        "Person 2 Name", "Person 2 Goals", "Person 2 Meeting Preference",
                        "Person 2 Skills", "Person 2 Interests", "Person 2 Age", "Person 2 Gender",
                        "Person 2 Languages", "Person 2 Personality", "Person 2 Location",
                        "Person 2 Job Title", "Person 2 Education", "Person 2 Seniority",
                        "Person 2 Industry", "Person 2 Organization Type", "Person 2 Company Size",
                        "Person 2 Company Stage", "Person 2 Bio",
                        // Match info
                        "Score", "Reasoning"
                    };
                    for (int i = 0; i < headers.Length; i++)
                    {
                        var cell = headerRow.CreateCell(i);
                        cell.SetCellValue(headers[i]);
                        cell.CellStyle = headerStyle;
                    }

                    // Helper: write all profile fields for a person starting at column offset
                    void WriteProfile(IRow r, int offset, CsvProfile p)
                    {
                        r.CreateCell(offset + 0).SetCellValue(p.FullName);
                        r.CreateCell(offset + 1).SetCellValue(p.Goals);
                        r.CreateCell(offset + 2).SetCellValue(p.MeetingPreference);
                        r.CreateCell(offset + 3).SetCellValue(p.Skills);
                        r.CreateCell(offset + 4).SetCellValue(p.Interests);
                        r.CreateCell(offset + 5).SetCellValue(p.Age);
                        r.CreateCell(offset + 6).SetCellValue(p.Gender);
                        r.CreateCell(offset + 7).SetCellValue(p.Languages);
                        r.CreateCell(offset + 8).SetCellValue(p.Personality);
                        r.CreateCell(offset + 9).SetCellValue(p.Location);
                        r.CreateCell(offset + 10).SetCellValue(p.JobTitle);
                        r.CreateCell(offset + 11).SetCellValue(p.Education);
                        r.CreateCell(offset + 12).SetCellValue(p.Seniority);
                        r.CreateCell(offset + 13).SetCellValue(p.Industry);
                        r.CreateCell(offset + 14).SetCellValue(p.OrganizationType);
                        r.CreateCell(offset + 15).SetCellValue(p.CompanySize);
                        r.CreateCell(offset + 16).SetCellValue(p.CompanyStage);
                        r.CreateCell(offset + 17).SetCellValue(p.Bio);
                    }

                    int rowIndex = 1;
                    var matchedIds = new HashSet<Guid>();

                    var allSuggestions = suggestions
                        .OrderByDescending(s => s.Score)
                        .ToList();

                    foreach (var match in allSuggestions)
                    {
                        var p1 = profiles.FirstOrDefault(p => p.Id == match.User1Id);
                        var p2 = profiles.FirstOrDefault(p => p.Id == match.User2Id);

                        if (p1 == null || p2 == null) continue;

                        matchedIds.Add(p1.Id);
                        matchedIds.Add(p2.Id);

                        // A → B  (18 P1 fields | 18 P2 fields | score | reasoning)
                        IRow row = sheet.CreateRow(rowIndex++);
                        WriteProfile(row, 0, p1);
                        WriteProfile(row, 18, p2);
                        row.CreateCell(36).SetCellValue(match.Score);
                        row.CreateCell(37).SetCellValue(match.Reasoning);

                        // B → A (reverse so each person appears on both sides)
                        IRow rowReverse = sheet.CreateRow(rowIndex++);
                        WriteProfile(rowReverse, 0, p2);
                        WriteProfile(rowReverse, 18, p1);
                        rowReverse.CreateCell(36).SetCellValue(match.Score);
                        rowReverse.CreateCell(37).SetCellValue(match.Reasoning);
                    }

                    // Auto-size columns
                    for (int i = 0; i < headers.Length; i++)
                        sheet.AutoSizeColumn(i);

                    // Second sheet: participants with no matches — always created so it's visible
                    var unmatched = profiles.Where(p => !matchedIds.Contains(p.Id)).ToList();
                    ISheet unmatchedSheet = workbook.CreateSheet("No Match Found");
                    string[] unmatchedHeaders = { "Name", "Goals", "Meeting Preference", "Skills", "Interests", "Age", "Gender", "Languages", "Personality", "Location", "Job Title", "Education", "Seniority", "Industry", "Organization Type", "Company Size", "Company Stage", "Bio" };

                    IRow unmatchedHeader = unmatchedSheet.CreateRow(0);
                    for (int i = 0; i < unmatchedHeaders.Length; i++)
                    {
                        var cell = unmatchedHeader.CreateCell(i);
                        cell.SetCellValue(unmatchedHeaders[i]);
                        cell.CellStyle = headerStyle;
                    }

                    if (unmatched.Any())
                    {
                        int uRow = 1;
                        foreach (var p in unmatched)
                        {
                            IRow row = unmatchedSheet.CreateRow(uRow++);
                            row.CreateCell(0).SetCellValue(p.FullName);
                            row.CreateCell(1).SetCellValue(p.Goals);
                            row.CreateCell(2).SetCellValue(p.MeetingPreference);
                            row.CreateCell(3).SetCellValue(p.Skills);
                            row.CreateCell(4).SetCellValue(p.Interests);
                            row.CreateCell(5).SetCellValue(p.Age);
                            row.CreateCell(6).SetCellValue(p.Gender);
                            row.CreateCell(7).SetCellValue(p.Languages);
                            row.CreateCell(8).SetCellValue(p.Personality);
                            row.CreateCell(9).SetCellValue(p.Location);
                            row.CreateCell(10).SetCellValue(p.JobTitle);
                            row.CreateCell(11).SetCellValue(p.Education);
                            row.CreateCell(12).SetCellValue(p.Seniority);
                            row.CreateCell(13).SetCellValue(p.Industry);
                            row.CreateCell(14).SetCellValue(p.OrganizationType);
                            row.CreateCell(15).SetCellValue(p.CompanySize);
                            row.CreateCell(16).SetCellValue(p.CompanyStage);
                            row.CreateCell(17).SetCellValue(p.Bio);
                        }
                    }
                    else
                    {
                        // All participants received at least one match
                        unmatchedSheet.CreateRow(1).CreateCell(0).SetCellValue("All participants received at least one match.");
                    }

                    for (int i = 0; i < unmatchedHeaders.Length; i++)
                        unmatchedSheet.AutoSizeColumn(i);

                    workbook.Write(memoryStream, leaveOpen: true);
                    memoryStream.Position = 0;

                    _progressService.UpdateProgress(taskKey, 100);
                    return File(memoryStream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "matches.xlsx");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing CSV match.");
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        // Fast local keyword-overlap score — no API calls, used for pre-filtering
        private int LocalScore(CsvProfile a, CsvProfile b)
        {
            int score = 0;
            var fields = new[]
            {
                (a.Goals,       b.Goals,       3),
                (a.Skills,      b.Skills,      3),
                (a.Interests,   b.Interests,   2),
                (a.Industry,    b.Industry,    2),
                (a.Languages,   b.Languages,   1),
                (a.Personality, b.Personality, 1),
                (a.Seniority,   b.Seniority,   1),
            };
            foreach (var (fa, fb, weight) in fields)
            {
                var wordsA = fa.ToLowerInvariant().Split(new[] { ' ', ',', ';', '/' }, StringSplitOptions.RemoveEmptyEntries).ToHashSet();
                var wordsB = fb.ToLowerInvariant().Split(new[] { ' ', ',', ';', '/' }, StringSplitOptions.RemoveEmptyEntries).ToHashSet();
                score += wordsA.Intersect(wordsB).Count() * weight;
            }
            return score;
        }

        private async Task<List<CsvMatchSuggestion>> GetCsvMatchSuggestionsAsync(Guid companyId, string taskKey, List<CsvProfile> profiles, CancellationToken ct)
        {
            var dbApiKey = await _context.Settings.FindAsync(companyId, "AiApiKey");
            var dbModel = await _context.Settings.FindAsync(companyId, "AiModel");
            var dbProvider = await _context.Settings.FindAsync(companyId, "AiProvider");

            var apiKey = (dbApiKey?.Value ?? _configuration["AiSettings:ApiKey"])?.Trim();
            var model = (dbModel?.Value ?? _configuration["AiSettings:Model"] ?? "gpt-4o").Trim();
            var provider = (dbProvider?.Value ?? _configuration["AiSettings:Provider"] ?? "OpenAI").Trim();

            if (string.IsNullOrEmpty(apiKey))
            {
                _logger.LogWarning("AI API Key is missing.");
                return new List<CsvMatchSuggestion>();
            }

            // ── Phase 1: local pre-filter ─────────────────────────────────────
            // For each person keep only their top-K candidates by keyword overlap.
            // This reduces AI calls from O(N²) to O(N * topK).
            const int topK = 20; // Increased from 3 to allow more matches per person
            var seenPairs = new HashSet<string>();
            var allPairs = new List<object>();
            var pairMeta = new List<(Guid Id1, Guid Id2)>();

            for (int i = 0; i < profiles.Count; i++)
            {
                var a = profiles[i];

                var candidates = profiles
                    .Where((_, idx) => idx != i)
                    .Select(b => (Profile: b, Score: LocalScore(a, b)))
                    .OrderByDescending(x => x.Score)
                    .Take(topK);

                foreach (var (b, _) in candidates)
                {
                    var key = a.Id.CompareTo(b.Id) < 0
                        ? $"{a.Id}|{b.Id}"
                        : $"{b.Id}|{a.Id}";
                    if (!seenPairs.Add(key)) continue;

                    allPairs.Add(new
                    {
                        pair_index = allPairs.Count,
                        person1 = new
                        {
                            id = a.Id,
                            name = a.FullName,
                            goals = a.Goals,
                            meetingPreference = a.MeetingPreference,
                            skills = a.Skills,
                            interests = a.Interests,
                            languages = a.Languages,
                            personality = a.Personality,
                            jobTitle = a.JobTitle,
                            education = a.Education,
                            seniority = a.Seniority,
                            industry = a.Industry,
                            orgType = a.OrganizationType,
                            companySize = a.CompanySize,
                            companyStage = a.CompanyStage,
                            bio = a.Bio
                        },
                        person2 = new
                        {
                            id = b.Id,
                            name = b.FullName,
                            goals = b.Goals,
                            meetingPreference = b.MeetingPreference,
                            skills = b.Skills,
                            interests = b.Interests,
                            languages = b.Languages,
                            personality = b.Personality,
                            jobTitle = b.JobTitle,
                            education = b.Education,
                            seniority = b.Seniority,
                            industry = b.Industry,
                            orgType = b.OrganizationType,
                            companySize = b.CompanySize,
                            companyStage = b.CompanyStage,
                            bio = b.Bio
                        }
                    });
                    pairMeta.Add((a.Id, b.Id));
                }
            }

            int totalPairs = allPairs.Count;
            _logger.LogInformation($"[CsvMatching] {profiles.Count} participants -> {totalPairs} pre-filtered pairs (topK={topK}). Processing in batches of 20.");

            const int batchSize = 20;
            const int maxConcurrency = 3; // keep low — each batch is already large
            var allResults = new List<CsvMatchSuggestion>();
            var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            int totalBatches = (int)Math.Ceiling((double)totalPairs / batchSize);

            _logger.LogInformation($"[CsvMatching] Firing {totalBatches} batches (max {maxConcurrency} concurrent).");

            var semaphore = new SemaphoreSlim(maxConcurrency);
            var batchTasks = new List<Task<List<CsvMatchSuggestion>>>();
            int batchesCompleted = 0;

            for (int batchStart = 0; batchStart < allPairs.Count; batchStart += batchSize)
            {
                var batch = allPairs.Skip(batchStart).Take(batchSize).ToList();
                var batchMeta = pairMeta.Skip(batchStart).Take(batchSize).ToList();
                int batchNum = (batchStart / batchSize) + 1;

                batchTasks.Add(Task.Run(async () =>
                {
                    await semaphore.WaitAsync();
                    try 
                    { 
                        var result = await ScoreBatchAsync(_httpClientFactory, provider, model, apiKey, batch, batchMeta, batchNum, jsonOptions, ct);
                        int done = Interlocked.Increment(ref batchesCompleted);
                        _progressService.UpdateProgress(taskKey, (double)done / totalBatches * 100);
                        return result;
                    }
                    finally { semaphore.Release(); }
                }));
            }

            var batchResults = await Task.WhenAll(batchTasks);
            foreach (var result in batchResults)
                allResults.AddRange(result);


            _logger.LogInformation($"[CsvMatching] Total results collected: {allResults.Count} matches.");
            return allResults;
        }

        private async Task<List<CsvMatchSuggestion>> ScoreBatchAsync(
            IHttpClientFactory httpClientFactory, string provider, string model, string apiKey,
            List<object> batch, List<(Guid Id1, Guid Id2)> batchMeta,
            int batchNum, JsonSerializerOptions jsonOptions, CancellationToken ct)
        {
            var batchJson = JsonSerializer.Serialize(batch);

            var prompt = $"You are a professional networking matchmaker.\n" +
                         $"Score the following {batch.Count} pairs of participants for a networking event.\n\n" +
                         "For EACH pair return:\n" +
                         "- \"User1Id\": exact GUID of person1.id\n" +
                         "- \"User2Id\": exact GUID of person2.id\n" +
                         "- \"Score\": integer 1-100 (higher = better match)\n" +
                         "- \"Reasoning\": 1 sentence why they should meet\n\n" +
                         "Consider ALL of these dimensions when scoring:\n" +
                         "goals, meetingPreference, skills, interests, languages, personality, jobTitle, education, seniority, industry, orgType, companySize, companyStage, bio\n\n" +
                         $"You MUST return exactly {batch.Count} entries - one per pair.\n\n" +
                         "Return format:\n" +
                         "{\n  \"matches\": [\n    {\"User1Id\": \"guid\", \"User2Id\": \"guid\", \"Score\": 75, \"Reasoning\": \"...\"}\n  ]\n}\n\n" +
                         $"Pairs:\n{batchJson}";

            try
            {
                var client = httpClientFactory.CreateClient("OpenAI"); // Generic named client with 5min timeout
                string contentString = "";

                if (provider.Equals("Gemini", StringComparison.OrdinalIgnoreCase))
                {
                    var requestBody = new
                    {
                        systemInstruction = new { parts = new[] { new { text = "You are a helpful assistant that outputs valid JSON only." } } },
                        contents = new[] { new { role = "user", parts = new[] { new { text = prompt } } } },
                        generationConfig = new { responseMimeType = "application/json", maxOutputTokens = 8000 }
                    };

                    var formattedModel = model.StartsWith("models/") ? model : $"models/{model}";

                    var httpContent = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
                    var request = new HttpRequestMessage(HttpMethod.Post, $"https://generativelanguage.googleapis.com/v1beta/{formattedModel}:generateContent?key={apiKey}");
                    request.Content = httpContent;

                    var response = await client.SendAsync(request, ct);
                    if (!response.IsSuccessStatusCode)
                    {
                        var errStr = await response.Content.ReadAsStringAsync();
                        _logger.LogError($"[CsvMatching] Batch {batchNum} Gemini API Error: {response.StatusCode} - {errStr}");
                        return new List<CsvMatchSuggestion>();
                    }

                    var jsonResponse = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(jsonResponse);
                    var candidates = doc.RootElement.GetProperty("candidates");
                    if (candidates.GetArrayLength() > 0)
                    {
                        var finishReason = candidates[0].TryGetProperty("finishReason", out var fr) ? fr.GetString() : null;
                        if (finishReason != null && finishReason != "STOP")
                            _logger.LogWarning($"[CsvMatching] Batch {batchNum} Gemini finishReason='{finishReason}' — response may be truncated.");
                        
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
                            new { role = "system", content = "You are a helpful assistant that outputs valid JSON only." },
                            new { role = "user", content = prompt }
                        },
                        response_format = new { type = "json_object" },
                        max_tokens = 8000
                    };

                    var httpContent = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
                    var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions");
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
                    request.Content = httpContent;

                    var response = await client.SendAsync(request, ct);
                    if (!response.IsSuccessStatusCode)
                    {
                        var errStr = await response.Content.ReadAsStringAsync();
                        _logger.LogError($"[CsvMatching] Batch {batchNum} OpenAI API Error: {response.StatusCode} - {errStr}");
                        return new List<CsvMatchSuggestion>();
                    }

                    var jsonResponse = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(jsonResponse);
                    var choice = doc.RootElement.GetProperty("choices")[0];

                    var finishReason = choice.TryGetProperty("finish_reason", out var fr) ? fr.GetString() : null;
                    if (finishReason != null && finishReason != "stop")
                        _logger.LogWarning($"[CsvMatching] Batch {batchNum} OpenAI finish_reason='{finishReason}' — response may be truncated.");

                    contentString = choice.GetProperty("message").GetProperty("content").GetString() ?? "";
                }

                if (string.IsNullOrEmpty(contentString)) return new List<CsvMatchSuggestion>();

                // Strip markdown formatting if present
                contentString = contentString.Trim();
                if (contentString.StartsWith("```json", StringComparison.OrdinalIgnoreCase)) contentString = contentString.Substring(7);
                else if (contentString.StartsWith("```")) contentString = contentString.Substring(3);
                if (contentString.EndsWith("```")) contentString = contentString.Substring(0, contentString.Length - 3);
                contentString = contentString.Trim();

                var resultDoc = JsonDocument.Parse(contentString);
                JsonElement arrayEl;

                if (resultDoc.RootElement.TryGetProperty("matches", out var matchesEl))
                    arrayEl = matchesEl;
                else if (resultDoc.RootElement.TryGetProperty("pairs", out var pairsEl))
                    arrayEl = pairsEl;
                else if (resultDoc.RootElement.ValueKind == JsonValueKind.Array)
                    arrayEl = resultDoc.RootElement;
                else
                    return new List<CsvMatchSuggestion>();

                // Parse safely: use raw DTO with string IDs to avoid GUID format exceptions
                var rawItems = JsonSerializer.Deserialize<List<RawMatchItem>>(arrayEl.GetRawText(), jsonOptions)
                               ?? new List<RawMatchItem>();

                var results = new List<CsvMatchSuggestion>();
                for (int k = 0; k < rawItems.Count; k++)
                {
                    var item = rawItems[k];
                    var id1 = Guid.TryParse(item.User1Id, out var g1) ? g1
                              : (k < batchMeta.Count ? batchMeta[k].Id1 : Guid.Empty);
                    var id2 = Guid.TryParse(item.User2Id, out var g2) ? g2
                              : (k < batchMeta.Count ? batchMeta[k].Id2 : Guid.Empty);

                    if (id1 == Guid.Empty || id2 == Guid.Empty) continue;

                    results.Add(new CsvMatchSuggestion
                    {
                        User1Id = id1,
                        User2Id = id2,
                        Score = item.Score,
                        Reasoning = item.Reasoning
                    });
                }

                if (results.Count > 0)
                {
                    _logger.LogInformation($"[CsvMatching] Batch {batchNum} returned {results.Count} results.");
                    return results;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"[CsvMatching] Error on batch {batchNum}.");
            }

            return new List<CsvMatchSuggestion>();
        }

        private List<string> ParseCsvLine(string line)
        {
            var result = new List<string>();
            var current = new StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];

                if (c == '"')
                {
                    // Handle escaped quotes ("")
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        current.Append('"');
                        i++; // skip next quote
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
    }

    public class CsvProfile
    {
        public Guid Id { get; set; }
        public string FullName { get; set; } = "";
        public string Goals { get; set; } = "";
        public string MeetingPreference { get; set; } = "";
        public string Skills { get; set; } = "";
        public string Interests { get; set; } = "";
        public string Age { get; set; } = "";
        public string Gender { get; set; } = "";
        public string Languages { get; set; } = "";
        public string Personality { get; set; } = "";
        public string Location { get; set; } = "";
        public string JobTitle { get; set; } = "";
        public string Education { get; set; } = "";
        public string Seniority { get; set; } = "";
        public string Industry { get; set; } = "";
        public string OrganizationType { get; set; } = "";
        public string CompanySize { get; set; } = "";
        public string CompanyStage { get; set; } = "";
        public string Bio { get; set; } = "";
    }

    public class CsvMatchSuggestion
    {
        public Guid User1Id { get; set; }
        public Guid User2Id { get; set; }
        public int Score { get; set; }
        public string Reasoning { get; set; } = "";
    }

    // Intermediate DTO: string IDs so JSON deserialization never throws on bad GUID values
    public class RawMatchItem
    {
        public string User1Id { get; set; } = "";
        public string User2Id { get; set; } = "";
        public int Score { get; set; }
        public string Reasoning { get; set; } = "";
    }
}
