using Amazon.Runtime.Internal.Util;
using Amazon.S3;
using FuzzySearch.Data;
using FuzzySearch.Models;
using FuzzySearch.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.Collections.Immutable;
using System.Data;
using System.Linq;
using System.Collections.Generic;
using System;
using System.Diagnostics;

namespace FuzzySearch.Controllers
{
    // Controllers/DictionaryController.cs
    [ApiController]
    [Route("api/[controller]")]
    public class DictionaryController : ControllerBase
    {
        private readonly IDatabaseService _databaseService;
        private readonly ILogger<DictionaryController> _logger;
        private readonly IWebHostEnvironment _env;

        public DictionaryController(IDatabaseService databaseService, ILogger<DictionaryController> logger, IWebHostEnvironment env)
        {
            _databaseService = databaseService;
            _logger = logger;
            _env = env;
        }

        private IActionResult HandleException(Exception ex, string userMessage)
        {
            // Always log full exception server-side
            _logger.LogError(ex, "{Message}", userMessage);

            // In development include exception details for easier debugging
            if (_env.IsDevelopment())
            {
                return StatusCode(500, new
                {
                    Success = false,
                    Message = userMessage,
                    Error = ex.Message,
                    Exception = ex.GetType().FullName,
                    StackTrace = ex.StackTrace
                });
            }

            // Production: don't leak internals
            return StatusCode(500, new
            {
                Success = false,
                Message = userMessage
            });
        }

        /*
        [HttpPost("search")]
        public async Task<IActionResult> FuzzySearch([FromBody] SearchRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Query))
                {
                    return BadRequest("Query cannot be empty");
                }

                var results = await _databaseService.FuzzySearchAsync(
                    request.Query,
                    request.Limit,
                    request.SimilarityThreshold
                );

                return Ok(new
                {
                    Query = request.Query,
                    Results = results,
                    TotalCount = results.Count()
                });
            }
            catch (Exception ex)
            {
                return HandleException(ex, "Error performing fuzzy search.");
            }
        }
        
        [HttpPost("words")]
        public async Task<IActionResult> AddWord([FromBody] string word)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(word))
                {
                    return BadRequest("Word cannot be empty");
                }

                var result = await _databaseService.AddWordAsync(word.Trim().ToLower());
                return Ok(new { Success = result, Message = result ? "Word added" : "Word already exists" });
            }
            catch (Exception ex)
            {
                return HandleException(ex, "Error adding word.");
            }
        }

        [HttpPost("words/bulk")]
        public async Task<IActionResult> BulkAddWords([FromBody] List<string> words)
        {
            try
            {
                if (words == null || !words.Any())
                {
                    return BadRequest("Word list cannot be empty");
                }

                var cleanedWords = words.Where(w => !string.IsNullOrWhiteSpace(w))
                                      .Select(w => w.Trim().ToLower())
                                      .Distinct()
                                      .ToList();

                var result = await _databaseService.BulkInsertWordsAsync(cleanedWords);
                return Ok(new { Success = true, AddedCount = result, TotalWords = cleanedWords.Count });
            }
            catch (Exception ex)
            {
                return HandleException(ex, "Error bulk adding words.");
            }
        }
        */
        [HttpGet("test-connection")]
        public async Task<IActionResult> TestConnection([FromServices] YourDbContext dbContext)
        {
            try
            {
                // Try to open a connection to the database
                await dbContext.Database.OpenConnectionAsync();
                await dbContext.Database.CloseConnectionAsync();
                return Ok(new { Success = true, Message = "Database connection successful." });
            }
            catch (Exception ex)
            {
                return HandleException(ex, "Database connection test failed.");
            }
        }
        
        [HttpGet("check-pgtrgm")]
        public async Task<IActionResult> CheckPgTrgm([FromServices] IConfiguration configuration)
        {
            var connectionString = configuration.GetConnectionString("PostgreSQL");
            try
            {
                using var connection = new NpgsqlConnection(connectionString);
                await connection.OpenAsync();

                var cmd = new NpgsqlCommand("SELECT COUNT(*) FROM pg_extension WHERE extname = 'pg_trgm';", connection);
                var count = (long)await cmd.ExecuteScalarAsync();

                bool isEnabled = count > 0;
                return Ok(new { PgTrgmEnabled = isEnabled });
            }
            catch (Exception ex)
            {
                return HandleException(ex, "Error checking pg_trgm extension.");
            }
        }

        [HttpPost("search-s3-sentence")]
        public async Task<IActionResult> FuzzySearchS3Sentence(
            [FromBody] SearchRequest request,
            [FromServices] S3WordService s3WordService)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Query))
                    return BadRequest("Query cannot be empty");
                // Set your S3 bucket and key here, or pass via request
                string bucketName = "engdictionary";
                string key = "count_1w.txt";
                var words = await s3WordService.GetWordsAsync(bucketName, key, request.Query.Length);

                var qWords = request.Query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                var results = new List<List<SearchResult>>();
                foreach (var qWord in qWords)
                {
                    var tmp = words.Select(word => new SearchResult
                    {
                        Word = word.Word,
                        Distance = FuzzyMatcher.LevenshteinDistance(qWord, word.Word),
                        Frequency = word.Freq ?? 0
                    })
                    .Where(r => r.Distance <= request.SimilarityThreshold && r.Frequency > 10000000)
                    .OrderBy(r => r.Distance)
                    .ThenByDescending(r => r.Frequency)
                    .ThenBy(r => r.Word)
                    .Take(request.Limit)
                    .ToList();
                    // Optionally, add similarity calculation
                    tmp.ForEach(r => r.Similarity = 1.0 - (double)r.Distance / Math.Max(request.Query.Length, r.Word.Length));
                    results.Add(tmp);
                }
                return Ok(new
                {
                    Query = request.Query,
                    Results = results,
                    TotalCount = results.Count
                });
            }
            catch (Exception ex)
            {
                return HandleException(ex, "Error performing fuzzy search on S3 for query.");
            }
        }

        [HttpPost("search-s3")]
        public async Task<IActionResult> FuzzySearchS3(
            [FromBody] SearchRequest request,
            [FromServices] S3WordService s3WordService)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Query))
                    return BadRequest("Query cannot be empty");

                string bucketName = "engdictionary";
                string key = "count_1w.txt";

                using (new FuzzySearch.Utilities.OperationTimer(_logger, "Total FuzzySearchS3"))
                {
                    // 1) Fetch and parse
                    using (new FuzzySearch.Utilities.OperationTimer(_logger, "GetWordEntriesAsync"))
                    {
                        // returns parsed WordEntry (Word + Frequency)
                    }
                    var gScan = Stopwatch.StartNew();
                    var entries = await s3WordService.GetWordEntriesAsync(bucketName, key, request.Query.Length);

                    // 2) Scan & compute distances (hot loop)
                    int limit = Math.Max(1, request.Limit);
                    int threshold = (int)Math.Floor(request.SimilarityThreshold);

                    var set = new SortedSet<Candidate>(new CandidateComparer());
                    long seq = 0;
                    int scanned = 0, passedThreshold = 0;
                    gScan.Stop();
                    _logger.LogInformation("s3WordService took " + gScan.Elapsed.TotalMilliseconds + "ms");
                    var swScan = Stopwatch.StartNew();
                    foreach (var e in entries)
                    {
                        scanned++;
                        var word = e.Word;
                        int dist = FuzzyMatcher.LevenshteinDistance(request.Query, (string)word);
                        if (dist > threshold)
                            continue;

                        passedThreshold++;
                        var sr = new SearchResult
                        {
                            Word = (string)word,
                            Distance = dist,
                            Frequency = (int)e.Frequency
                        };
                        var cand = new Candidate(sr, seq++);
                        set.Add(cand);
                        if (set.Count > limit)
                            set.Remove(set.Min);
                    }
                    swScan.Stop();
                    _logger.LogInformation("Scanned {Scanned} entries, {Passed} candidates in {Ms} ms", scanned, passedThreshold, swScan.Elapsed.TotalMilliseconds);

                    // 3) Materialize results (small)
                    using (new FuzzySearch.Utilities.OperationTimer(_logger, "Materialize/Compute Similarity"))
                    {
                        var results = set.Reverse().Select(c => c.Result).ToList();
                        results.ForEach(r => r.Similarity = 1.0 - (double)r.Distance / Math.Max(request.Query.Length, r.Word.Length));
                        return Ok(new { Query = request.Query, Results = results, TotalCount = results.Count });
                    }
                }
            }
            catch (Exception ex)
            {
                return HandleException(ex, "Error performing fuzzy search on S3 for query.");
            }
        }

        // Helper candidate wrapper and comparer for bounding top-K
        private sealed class Candidate
        {
            public SearchResult Result { get; }
            public long Seq { get; }

            public Candidate(SearchResult result, long seq)
            {
                Result = result;
                Seq = seq;
            }
        }

        private sealed class CandidateComparer : IComparer<Candidate>
        {
            // Compare such that "worse" candidates sort before "better" candidates:
            // worse = larger distance, lower frequency, lexicographically larger word.
            // This makes SortedSet.Min the worst candidate and easy to remove when size > K.
            public int Compare(Candidate? x, Candidate? y)
            {
                if (ReferenceEquals(x, y)) return 0;
                if (x is null) return -1;
                if (y is null) return 1;

                // 1) distance: larger distance is worse -> should come first (smaller in SortedSet ordering)
                int cmp = y!.Result.Distance.CompareTo(x!.Result.Distance);
                if (cmp != 0) return cmp;

                // 2) frequency: lower frequency is worse -> should come first
                cmp = x.Result.Frequency.CompareTo(y.Result.Frequency);
                if (cmp != 0) return cmp;

                // 3) word lexicographic: lexicographically larger is worse -> should come first
                cmp = y.Result.Word.CompareTo(x.Result.Word);
                if (cmp != 0) return cmp;

                // 4) sequence to ensure deterministic uniqueness
                return x.Seq.CompareTo(y.Seq);
            }
        }

        // ... remaining endpoints ...
    }
}
