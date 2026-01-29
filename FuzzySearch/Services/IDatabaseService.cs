using Dapper;
using FuzzySearch.Models;
using Npgsql;

namespace FuzzySearch.Services
{
    // Services/IDatabaseService.cs
    public interface IDatabaseService
    {
        Task<IEnumerable<SearchResult>> FuzzySearchAsync(string query, int limit, double similarityThreshold);
        Task<bool> AddWordAsync(string word);
        Task<bool> BulkInsertWordsAsync(IEnumerable<string> words);
    }

    // Services/DatabaseService.cs
    public class DatabaseService : IDatabaseService
    {
        private readonly string _connectionString;

        public DatabaseService(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("PostgreSQL");
        }

        public async Task<IEnumerable<SearchResult>> FuzzySearchAsync(string query, int limit, double similarityThreshold)
        {
            using var connection = new NpgsqlConnection(_connectionString);

            var sql = @"
        SELECT 
            ""Word"" AS ""Word"",
            similarity(""Word"", @query) AS ""Similarity"",
            levenshtein(""Word"", @query) AS ""Distance""
        FROM english_words 
        WHERE ""Word"" % @query
           OR similarity(""Word"", @query) > @similarityThreshold
        ORDER BY similarity(""Word"", @query) DESC, levenshtein(""Word"", @query) ASC
        LIMIT @limit";

            return await connection.QueryAsync<SearchResult>(sql, new
            {
                query,
                limit,
                similarityThreshold
            });
        }

        public async Task<bool> AddWordAsync(string Word)
        {
            using var connection = new NpgsqlConnection(_connectionString);

            var sql = @"INSERT INTO english_words (""Word"", ""Length"") 
                VALUES (@Word, @Length)";

            var affected = await connection.ExecuteAsync(sql, new
            {
                Word,
                length = Word.Length
            });

            return affected > 0;
        }

        public async Task<bool> BulkInsertWordsAsync(IEnumerable<string> words)
        {
            using var connection = new NpgsqlConnection(_connectionString);

            var wordList = words.Select(w => new { word = w, length = w.Length }).ToList();

            var sql = @"INSERT INTO english_words (word, length) 
                    VALUES (@Word, @Length) 
                    ON CONFLICT (word) DO NOTHING";

            var affected = await connection.ExecuteAsync(sql, wordList);
            return affected > 0;
        }
    }
}
