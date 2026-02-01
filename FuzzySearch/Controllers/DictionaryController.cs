using FuzzySearch.Data;
using FuzzySearch.Models;
using FuzzySearch.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.Data;
using System.Linq;
using Microsoft.AspNetCore.Hosting;

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
                var words = await s3WordService.GetWordsAsync(bucketName, key);

                var qWords = request.Query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                var results = new List<List<SearchResult>>();
                foreach (var qWord in qWords)
                {
                    var tmp = words.Select(word => new SearchResult
                    {
                        Word = word.Split("\t")[0],
                        Distance = FuzzyMatcher.LevenshteinDistance(qWord, word.Split("\t")[0]),
                        Frequency = long.TryParse(word.Split("\t").ElementAtOrDefault(1), out var freq) ? freq : 0
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

                // Set your S3 bucket and key here, or pass via request
                string bucketName = "engdictionary";
                string key = "count_1w.txt";

                var words = await s3WordService.GetWordsAsync(bucketName, key);

                var results = words
                    .Select(word => new SearchResult
                    {
                        Word = word.Split("\t")[0],
                        Distance = FuzzyMatcher.LevenshteinDistance(request.Query, word.Split("\t")[0]),
                        Frequency = int.TryParse(word.Split("\t")[1], out var freq) ? freq : 0
                    })
                    .Where(r => r.Distance <= request.SimilarityThreshold && !r.Word.Equals(request.Query))
                    .OrderBy(r => r.Distance)
                    .ThenByDescending(r => r.Frequency)
                    .ThenBy(r => r.Word)
                    .Take(request.Limit)
                    .ToList();

                // Optionally, add similarity calculation
                results.ForEach(r => r.Similarity = 1.0 - (double)r.Distance / Math.Max(request.Query.Length, r.Word.Length));

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

        [HttpPost("Search-s3-check-exists")]
        public async Task<IActionResult> CheckWordExistsS3(
            [FromBody] string word,
            [FromServices] S3WordService s3WordService)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(word))
                    return BadRequest("Word cannot be empty");
                // Set your S3 bucket and key here, or pass via request
                string bucketName = "engdictionary";
                string key = "count_1w.txt";
                var words = await s3WordService.GetWordsAsync(bucketName, key);
                bool exists = words.Any(w => w.Split("\t")[0].Equals(word.Trim(), StringComparison.OrdinalIgnoreCase));
                return Ok(new
                {
                    Word = word,
                    Exists = exists
                });
            }
            catch (Exception ex)
            {
                return HandleException(ex, "Error checking word existence on S3.");
            }
        }
    }
}
