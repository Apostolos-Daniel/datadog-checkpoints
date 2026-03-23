using System.IO.Compression;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DatadogCheckpoints;

/// <summary>
/// Sends DSM checkpoints directly to the Datadog pipeline stats API.
/// No Datadog Agent required — just an API key.
/// </summary>
public static class SendCheckpoint
{
    private const string PipelineStatsUrl = "https://trace.agent.us3.datadoghq.com/api/v0.1/pipeline_stats";

    public static async Task<int> Main(string[] args)
    {
        var apiKey = Environment.GetEnvironmentVariable("DD_API_KEY");
        if (string.IsNullOrEmpty(apiKey))
        {
            Console.Error.WriteLine("Error: DD_API_KEY environment variable is required");
            return 1;
        }

        var service = Environment.GetEnvironmentVariable("DD_SERVICE") ?? "datadog-checkpoints-app";
        var environment = Environment.GetEnvironmentVariable("DD_ENV") ?? "local";
        var checkpoint = args.Length > 0 ? args[0] : "test-checkpoint";
        var transactionId = args.Length > 1 ? args[1] : Guid.NewGuid().ToString();

        var payload = BuildPayload(transactionId, checkpoint, service, environment);

        Console.WriteLine("Sending checkpoint to Datadog...");
        Console.WriteLine($"  Transaction ID: {transactionId}");
        Console.WriteLine($"  Checkpoint: {checkpoint}");
        Console.WriteLine($"  Service: {service}");
        Console.WriteLine($"  Environment: {environment}");

        var (status, body) = await SendAsync(payload, apiKey);

        Console.WriteLine($"  Status: {(int)status}");
        Console.WriteLine($"  Response: {body}");

        if ((int)status >= 200 && (int)status < 300)
        {
            Console.WriteLine("Checkpoint sent successfully!");
            return 0;
        }

        Console.Error.WriteLine("Failed to send checkpoint");
        return 1;
    }

    private static CheckpointPayload BuildPayload(
        string transactionId, string checkpoint, string service, string environment)
    {
        var timestampNanos = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 1_000_000L;

        return new CheckpointPayload
        {
            Transactions = new[]
            {
                new TransactionEvent
                {
                    TransactionId = transactionId,
                    Checkpoint = checkpoint,
                    TimestampNanos = timestampNanos.ToString()
                }
            },
            Service = service,
            Environment = environment
        };
    }

    private static async Task<(HttpStatusCode Status, string Body)> SendAsync(
        CheckpointPayload payload, string apiKey)
    {
        var json = JsonSerializer.Serialize(payload);
        var jsonBytes = Encoding.UTF8.GetBytes(json);

        using var compressedStream = new MemoryStream();
        using (var gzip = new GZipStream(compressedStream, CompressionMode.Compress, leaveOpen: true))
        {
            await gzip.WriteAsync(jsonBytes);
        }
        var gzipBytes = compressedStream.ToArray();

        using var client = new HttpClient();
        using var content = new ByteArrayContent(gzipBytes);
        content.Headers.Add("Content-Type", "application/json");
        content.Headers.Add("Content-Encoding", "gzip");

        using var request = new HttpRequestMessage(HttpMethod.Post, PipelineStatsUrl);
        request.Content = content;
        request.Headers.Add("DD-API-KEY", apiKey);

        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        return (response.StatusCode, body);
    }
}

public class TransactionEvent
{
    [JsonPropertyName("transaction_id")]
    public string TransactionId { get; set; } = "";

    [JsonPropertyName("checkpoint")]
    public string Checkpoint { get; set; } = "";

    [JsonPropertyName("timestamp_nanos")]
    public string TimestampNanos { get; set; } = "";
}

public class CheckpointPayload
{
    [JsonPropertyName("transactions")]
    public TransactionEvent[] Transactions { get; set; } = Array.Empty<TransactionEvent>();

    [JsonPropertyName("service")]
    public string Service { get; set; } = "";

    [JsonPropertyName("environment")]
    public string Environment { get; set; } = "";
}
