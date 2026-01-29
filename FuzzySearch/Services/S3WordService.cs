using Amazon;
using Amazon.S3;
using Amazon.S3.Model;
using System.Text;

public class S3WordService
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

    public async Task<List<string>> GetWordsAsync(string bucketName, string key)
    {
        var words = new List<string>();
        var response = await _s3Client.GetObjectAsync(bucketName, key);
        using var reader = new StreamReader(response.ResponseStream, Encoding.UTF8);
        while (!reader.EndOfStream)
        {
            var line = await reader.ReadLineAsync();
            if (!string.IsNullOrWhiteSpace(line))
                words.Add(line.Trim());
        }
        return words;
    }
}