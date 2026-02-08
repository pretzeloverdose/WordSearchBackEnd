using Amazon;
using Amazon.S3;
using Amazon.S3.Model;
using System.Text;
using System.Collections.Generic;
using System.Threading.Tasks;
using FuzzySearch.Models;

public partial class S3WordService
{
    private readonly IAmazonS3 _s3Client = new AmazonS3Client(RegionEndpoint.APSoutheast2);

    public S3WordService(IAmazonS3 s3Client)
    {
        var configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables()
            .Build();
        var region = "ap-southeast-2";
        _s3Client = new AmazonS3Client(RegionEndpoint.GetBySystemName(region));
    }

    public async Task<List<string>> GetWordsAsync(string bucketName, string key, int wordLength)
    {
        var words = new List<string>();
        var response = await _s3Client.GetObjectAsync(bucketName, key);
        using var reader = new StreamReader(response.ResponseStream, Encoding.UTF8);
        while (!reader.EndOfStream)
        {
            var line = await reader.ReadLineAsync();
            string lineTrimmed = line.Trim() ?? string.Empty;
            int entryLength = lineTrimmed.IndexOf('\t');
            if (!string.IsNullOrWhiteSpace(line))
            {
                if (entryLength <= wordLength + 2 && entryLength >= wordLength - 2)
                {
                    words.Add(lineTrimmed);
                }
            }
        }
        return words;
    }

    // New helper that returns parsed entries (avoids string.Split allocations)
    public async Task<List<WordEntry>> GetWordEntriesAsync(string bucketName, string key, int wordLength)
    {
        var lines = await GetWordsAsync(bucketName, key, wordLength); // reuse existing fetch
        var result = new List<WordEntry>(lines.Count);

        foreach (var line in lines)
        {
            int tab = line.IndexOf('\t');

            string word;
            long freq = 0;

            if (tab >= 0)
            {
                word = line.Substring(0, tab);
                var freqPart = line.Substring(tab + 1);
                if (!string.IsNullOrEmpty(freqPart) && long.TryParse(freqPart, out var f))
                    freq = f;
            }
            else
            {
                word = line;
            }

            result.Add(new WordEntry(
                word,           // object word
                freq,           // object freq
                word.Length,    // int length
                DateTime.UtcNow // object createdAt
            ) { Frequency = freq, word = word });
        }

        return result;
    }
}