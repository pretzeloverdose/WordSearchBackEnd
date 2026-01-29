namespace FuzzySearch.Models
{
    // Models/Word.cs
    public class Word
    {
        public int Id { get; set; }
        public string Text { get; set; } = string.Empty;
        public int Length { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    // DTOs/SearchRequest.cs
    public class SearchRequest
    {
        public string Query { get; set; } = string.Empty;
        public int Limit { get; set; } = 10;
        public double SimilarityThreshold { get; set; } = 0.3;
    }

    // DTOs/SearchResult.cs
    public class SearchResult
    {
        public string Word { get; set; } = string.Empty;
        public double Similarity { get; set; }
        public int Distance { get; set; }
        public long Frequency { get; set; }
    }
}
