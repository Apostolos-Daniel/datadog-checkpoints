# Datadog Checkpoints — C# .NET

A C# .NET console app that sends sample/test checkpoints to Datadog's [Data Streams Monitoring](https://docs.datadoghq.com/data_streams/) for Business Transaction Tracking.

This is the .NET equivalent of the [Node.js/TypeScript version](../README.md) in the parent directory.

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (or later)
- A [Datadog API key](https://docs.datadoghq.com/account_management/api-app-keys/)

## Configuration

```bash
export DD_API_KEY="your-api-key"
```

| Environment Variable | Description | Default |
|---|---|---|
| `DD_API_KEY` | **(Required)** Your Datadog API key | — |
| `DD_SERVICE` | Service name reported to Datadog | `datadog-checkpoints-app` |
| `DD_ENV` | Environment name reported to Datadog | `local` |

## Option 1: Direct HTTP API

Sends checkpoints directly to the Datadog pipeline stats API endpoint using HTTPS. No Datadog Agent required — just an API key.

### Usage

```bash
# Send a checkpoint with default name
dotnet run

# Send a checkpoint with a custom name
dotnet run -- my-checkpoint

# Send a checkpoint with a custom name and transaction ID
dotnet run -- my-checkpoint my-transaction-123
```

### Example output

```
Sending checkpoint to Datadog...
  Transaction ID: a1b2c3d4-e5f6-7890-abcd-ef1234567890
  Checkpoint: test-checkpoint
  Service: datadog-checkpoints-app
  Environment: local
  Status: 202
  Response:
Checkpoint sent successfully!
```

### How it works

1. Builds a checkpoint payload containing a transaction ID, checkpoint name, and nanosecond-precision timestamp
2. Gzip-compresses the JSON payload
3. Sends it via HTTPS POST to `https://trace.agent.us3.datadoghq.com/api/v0.1/pipeline_stats`

## Option 2: Using dd-trace-dotnet (Auto-Instrumentation)

The .NET Datadog tracer uses **auto-instrumentation** for Data Streams Monitoring — it automatically instruments supported messaging libraries (Kafka, RabbitMQ, SQS, SNS, Kinesis, IBM MQ, Azure Service Bus) without manual API calls.

Unlike the Node.js `dd-trace` library which exposes a `trackTransaction()` method, the .NET tracer handles checkpoint tracking internally when messages flow through supported libraries.

### Setup

1. Install the [Datadog .NET Tracer](https://docs.datadoghq.com/tracing/trace_collection/automatic_instrumentation/dd_libraries/dotnet-core/)
2. Set the required environment variables:

```bash
export DD_DATA_STREAMS_ENABLED=true
export DD_TRACE_REMOVE_INTEGRATION_SERVICE_NAMES_ENABLED=true
```

3. Run your application with the tracer attached — DSM checkpoints are created automatically when messages are produced/consumed through supported libraries.

> **Note:** Starting with .NET tracer v3.22.0, DSM is in a default-enabled state. Setting `DD_DATA_STREAMS_ENABLED=true` explicitly enables additional features like schema tracking.

### Supported libraries

| Technology | NuGet Package |
|---|---|
| Kafka | `Confluent.Kafka` |
| RabbitMQ | `RabbitMQ.Client` |
| Amazon SQS | `AWSSDK.SQS` |
| Amazon SNS | `AWSSDK.SimpleNotificationService` |
| Amazon Kinesis | `AWSSDK.Kinesis` |
| IBM MQ | `IBMMQDotnetClient` |
| Azure Service Bus | `Azure.Messaging.ServiceBus` |

## Build

```bash
# Build the project
dotnet build

# Run the compiled version
dotnet run --no-build
```

## Testing end-to-end

```bash
# Send a first checkpoint
dotnet run -- order-placed order-123

# Send a second checkpoint for the same transaction
dotnet run -- order-completed order-123
```

Then verify in Datadog under **Data Streams Monitoring > Transactions**.
