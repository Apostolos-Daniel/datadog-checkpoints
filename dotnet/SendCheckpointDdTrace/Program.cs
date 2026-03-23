using Datadog.Trace;

namespace DatadogCheckpoints;

/// <summary>
/// Sends DSM checkpoints via the dd-trace-dotnet library.
/// Requires a running Datadog Agent (local or remote).
/// </summary>
public static class SendCheckpointDdTrace
{
    public static async Task<int> Main(string[] args)
    {
        var checkpoint = args.Length > 0 ? args[0] : "test-checkpoint";
        var transactionId = args.Length > 1 ? args[1] : Guid.NewGuid().ToString();

        var service = Environment.GetEnvironmentVariable("DD_SERVICE") ?? "datadog-checkpoints-app";
        var environment = Environment.GetEnvironmentVariable("DD_ENV") ?? "local";
        var traceAgentUrl = GetTraceAgentDestination();

        Console.WriteLine("Sending checkpoint via dd-trace...");
        Console.WriteLine($"  Trace agent: {traceAgentUrl}");
        Console.WriteLine($"  Transaction ID: {transactionId}");
        Console.WriteLine($"  Checkpoint: {checkpoint}");
        Console.WriteLine($"  Service: {service}");
        Console.WriteLine($"  Environment: {environment}");

        // Create a span and inject a DSM checkpoint using SpanContextInjector.
        // The carrier is a simple dictionary — we only need the DSM side-effect
        // (SetCheckpoint), not the propagated headers.
        using (var scope = Tracer.Instance.StartActive("checkpoint.send"))
        {
            var span = scope.Span;
            span.SetTag("checkpoint.name", checkpoint);
            span.SetTag("transaction.id", transactionId);

            var injector = new SpanContextInjector();
            var carrier = new Dictionary<string, string>();
            injector.InjectIncludingDsm(
                carrier,
                (c, key, value) => c[key] = value,
                span.Context,
                messageType: "manual-checkpoint",
                target: checkpoint);

            Console.WriteLine("  Checkpoint tracked on span");
        }

        Console.WriteLine("Checkpoint sent successfully!");

        Console.WriteLine("Flushing tracer...");
        await Tracer.Instance.FlushAsync();
        Console.WriteLine("Flush complete.");

        return 0;
    }

    private static string GetTraceAgentDestination()
    {
        var fromEnv = Environment.GetEnvironmentVariable("DD_TRACE_AGENT_URL")?.Trim();
        if (!string.IsNullOrEmpty(fromEnv))
            return fromEnv;

        var host = Environment.GetEnvironmentVariable("DD_AGENT_HOST") ?? "localhost";
        var port = Environment.GetEnvironmentVariable("DD_TRACE_AGENT_PORT") ?? "8126";
        return $"http://{host}:{port}";
    }
}
