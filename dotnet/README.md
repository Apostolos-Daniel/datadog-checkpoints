# Datadog Checkpoints — C# .NET

A C# .NET console app that sends sample/test checkpoints to Datadog's [Data Streams Monitoring](https://docs.datadoghq.com/data_streams/) for Business Transaction Tracking.

Once checkpoints are sent, they appear under **Data Streams Monitoring > Transactions > Business Transaction Tracking** in Datadog.

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (or later)
- A [Datadog API key](https://docs.datadoghq.com/account_management/api-app-keys/)

## Configuration

Set your Datadog API key in your terminal — all commands below will pick it up automatically:

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
  Endpoint (pipeline stats API): https://trace.agent.us3.datadoghq.com/api/v0.1/pipeline_stats
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

### Testing Option 1 and when the direct URL fails

Use Option 1 as the smoke test for the **direct pipeline stats intake** (no agent). The app prints the exact URL, HTTP **Status**, and **Response** body—those three lines tell you whether the call reached Datadog and how the API responded.

1. **Set `DD_API_KEY`** (same key you use in the Datadog UI for your organisation).
2. Run `dotnet run` (or pass a checkpoint / transaction id as shown in [Usage](#usage)).
3. **Read the output:**
   - **Status in the 2xx range** — the HTTP request was accepted by that intake host. If checkpoints still do not show up under **Data Streams Monitoring > Transactions**, confirm you are logged into the same Datadog **site** and org, and allow a short delay before refreshing the UI.
   - **401 / 403** — API key rejected or not authorised for that intake; regenerate or copy the key from [API Keys](https://docs.datadoghq.com/account_management/api-app-keys/).
   - **404 or other 4xx / 5xx** — often a **wrong intake host for your site**. The URL in `SendCheckpoint.cs` is hardcoded to the **US3** trace agent (`trace.agent.us3.datadoghq.com`). Your organisation may use another [Datadog site](https://docs.datadoghq.com/getting_started/site/) (for example US1, EU, or another region). Update `PipelineStatsUrl` in `SendCheckpoint.cs` until the **Status** is 2xx.
   - **Network or TLS errors** in the console (no HTTP status) — local firewall, proxy, or DNS blocking `trace.agent.*`; fix connectivity or try from another network.

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

## Creating a Transaction Pipeline

Once your checkpoints are being sent, you can create a Transaction Tracking Pipeline in Datadog:

1. Go to **Data Streams Monitoring > Transactions**
2. Click **Create Transaction Pipeline**
3. Select the **Manual Checkpoints** tab
4. Give your pipeline a name and set an SLO duration
5. Select your checkpoints from the dropdown for the start and end steps
