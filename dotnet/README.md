# Datadog Checkpoints — C# .NET

A C# .NET console app that sends sample/test checkpoints to Datadog's [Data Streams Monitoring](https://docs.datadoghq.com/data_streams/) for Business Transaction Tracking.

Once checkpoints are sent, they appear under **Data Streams Monitoring > Transactions > Business Transaction Tracking** in Datadog.

## Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0) (or later)
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

## Usage

Sends checkpoints directly to the Datadog pipeline stats API endpoint using HTTPS. No Datadog Agent required — just an API key. Zero NuGet dependencies.

```bash
cd SendCheckpoint

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

### Troubleshooting

The app prints the exact URL, HTTP **Status**, and **Response** body—those three lines tell you whether the call reached Datadog and how the API responded.

1. **Set `DD_API_KEY`** (same key you use in the Datadog UI for your organisation).
2. Run `dotnet run` from the `SendCheckpoint/` directory (or pass a checkpoint / transaction id as shown in [Usage](#usage)).
3. **Read the output:**
   - **Status in the 2xx range** — the HTTP request was accepted by that intake host. If checkpoints still do not show up under **Data Streams Monitoring > Transactions**, confirm you are logged into the same Datadog **site** and org, and allow a short delay before refreshing the UI.
   - **401 / 403** — API key rejected or not authorised for that intake; regenerate or copy the key from [API Keys](https://docs.datadoghq.com/account_management/api-app-keys/).
   - **404 or other 4xx / 5xx** — often a **wrong intake host for your site**. The URL in `Program.cs` is hardcoded to the **US3** trace agent (`trace.agent.us3.datadoghq.com`). Your organisation may use another [Datadog site](https://docs.datadoghq.com/getting_started/site/) (for example US1, EU, or another region). Update `PipelineStatsUrl` in `Program.cs` until the **Status** is 2xx.
   - **Network or TLS errors** in the console (no HTTP status) — local firewall, proxy, or DNS blocking `trace.agent.*`; fix connectivity or try from another network.

## Why no dd-trace-dotnet option?

Unlike the Node.js `dd-trace` library which exposes `tracer.dataStreamsCheckpointer.trackTransaction()` for manual transaction checkpoints, the .NET tracer (`Datadog.Trace`) **does not have an equivalent API**.

The .NET tracer's `SpanContextInjector.InjectIncludingDsm()` creates **DSM pathway checkpoints** (queue produce/consume topology tracking), which is a different data path from the **manual transaction checkpoints** used in Business Transaction Tracking. The [official .NET DSM docs](https://docs.datadoghq.com/data_streams/dotnet/) only cover auto-instrumentation for supported queues (Kafka, RabbitMQ, SQS, etc.) and do not include a manual instrumentation section — unlike the Java, Go, Python, and Node.js docs which all do.

For .NET, the Direct HTTP API approach is currently the only way to send manual transaction checkpoints.

## Build

```bash
cd SendCheckpoint

# Build the project
dotnet build

# Run the compiled version
dotnet run --no-build
```

## Testing end-to-end

```bash
cd SendCheckpoint

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
