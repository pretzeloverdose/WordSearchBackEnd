using FuzzySearch.Data;
using FuzzySearch.Models;
using FuzzySearch.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.Data;
using System.Linq;

namespace FuzzySearch.Controllers
{
    // Controllers/DictionaryController.cs
    [ApiController]
    [Route("api/[controller]")]
    public class DictionaryController : ControllerBase
    {
        private readonly IDatabaseService _databaseService;
        private readonly ILogger<DictionaryController> _logger;

        public DictionaryController(IDatabaseService databaseService, ILogger<DictionaryController> logger)
        {
            _databaseService = databaseService;
            _logger = logger;
        }

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
                _logger.LogError(ex, "Error performing fuzzy search for query: {Query}", request.Query);
                return StatusCode(500, "An error occurred while processing your request");
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
                _logger.LogError(ex, "Error adding word: {Word}", word);
                return StatusCode(500, "An error occurred while adding the word");
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
                _logger.LogError(ex, "Error bulk adding words");
                return StatusCode(500, "An error occurred while bulk adding words");
            }
        }

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
                _logger.LogError(ex, "Database connection test failed.");
                return StatusCode(500, new { Success = false, Message = "Database connection failed.", Error = ex.Message });
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
                _logger.LogError(ex, "Error checking pg_trgm extension.");
                return StatusCode(500, new { Success = false, Message = "Error checking pg_trgm extension.", Error = ex.Message });
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
                foreach (var qWord in qWords) {
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
                _logger.LogError(ex, "Error performing fuzzy search on S3 for query: {Query}", request.Query);
                return StatusCode(500, "An error occurred while processing your request");
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
                    .Where(r => r.Distance <= request.SimilarityThreshold)
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
                _logger.LogError(ex, "Error performing fuzzy search on S3 for query: {Query}", request.Query);
                return StatusCode(500, "An error occurred while processing your request");
            }
        }
    }
}
