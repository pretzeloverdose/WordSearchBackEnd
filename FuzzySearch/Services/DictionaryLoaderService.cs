namespace FuzzySearch.Services
{
    // Services/DictionaryLoaderService.cs
    public class DictionaryLoaderService
    {
        private readonly IDatabaseService _databaseService;

        public DictionaryLoaderService(IDatabaseService databaseService)
        {
            _databaseService = databaseService;
        }

        public async Task<int> LoadDictionaryFromFile(string filePath)
        {
            var words = await File.ReadAllLinesAsync(filePath);
            var validWords = words.Where(w => !string.IsNullOrWhiteSpace(w))
                                 .Select(w => w.Trim().ToLower())
                                 .Distinct()
                                 .ToList();

            await _databaseService.BulkInsertWordsAsync(validWords);
            return validWords.Count;
        }
    }
}
