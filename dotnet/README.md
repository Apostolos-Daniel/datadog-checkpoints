# Datadog Checkpoints — C# .NET

C# .NET console apps that send sample/test checkpoints to Datadog's [Data Streams Monitoring](https://docs.datadoghq.com/data_streams/) for Business Transaction Tracking.

Once checkpoints are sent, they appear under **Data Streams Monitoring > Transactions > Business Transaction Tracking** in Datadog.

## Project structure

```
dotnet/
├── SendCheckpoint/           # Option 1: Direct HTTP API (no agent needed)
│   ├── SendCheckpoint.csproj
│   └── Program.cs
└── SendCheckpointDdTrace/    # Option 2: dd-trace-dotnet (requires agent)
    ├── SendCheckpointDdTrace.csproj
    └── Program.cs
```

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
| `DD_API_KEY` | **(Required for Option 1)** Your Datadog API key | — |
| `DD_SERVICE` | Service name reported to Datadog | `datadog-checkpoints-app` |
| `DD_ENV` | Environment name reported to Datadog | `local` |

## Option 1: Direct HTTP API

Sends checkpoints directly to the Datadog pipeline stats API endpoint using HTTPS. No Datadog Agent required — just an API key. Zero NuGet dependencies.

### Usage

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

### Testing Option 1 and when the direct URL fails

Use Option 1 as the smoke test for the **direct pipeline stats intake** (no agent). The app prints the exact URL, HTTP **Status**, and **Response** body—those three lines tell you whether the call reached Datadog and how the API responded.

1. **Set `DD_API_KEY`** (same key you use in the Datadog UI for your organisation).
2. Run `dotnet run` from the `SendCheckpoint/` directory (or pass a checkpoint / transaction id as shown in [Usage](#usage)).
3. **Read the output:**
   - **Status in the 2xx range** — the HTTP request was accepted by that intake host. If checkpoints still do not show up under **Data Streams Monitoring > Transactions**, confirm you are logged into the same Datadog **site** and org, and allow a short delay before refreshing the UI.
   - **401 / 403** — API key rejected or not authorised for that intake; regenerate or copy the key from [API Keys](https://docs.datadoghq.com/account_management/api-app-keys/).
   - **404 or other 4xx / 5xx** — often a **wrong intake host for your site**. The URL in `Program.cs` is hardcoded to the **US3** trace agent (`trace.agent.us3.datadoghq.com`). Your organisation may use another [Datadog site](https://docs.datadoghq.com/getting_started/site/) (for example US1, EU, or another region). Update `PipelineStatsUrl` in `Program.cs` until the **Status** is 2xx.
   - **Network or TLS errors** in the console (no HTTP status) — local firewall, proxy, or DNS blocking `trace.agent.*`; fix connectivity or try from another network.

**Comparing with the agent path:** If you are unsure whether the problem is the direct URL or your account setup, run **Option 2** with a local Agent whose `DD_SITE` matches your organisation. If Option 2 works but Option 1 does not, the direct intake host in code is usually the mismatch—Option 2 relies on the Agent to route to the correct site.

## Option 2: Using dd-trace-dotnet

Uses the [`Datadog.Trace`](https://www.nuget.org/packages/Datadog.Trace) NuGet package with the `SpanContextInjector.InjectIncludingDsm()` API to create a DSM checkpoint within a trace span. This is the recommended approach for production applications that already use `dd-trace-dotnet`.

### Prerequisites (dd-trace)

A running [Datadog Agent](https://docs.datadoghq.com/agent/) is required. The tracer sends data to the agent, which forwards it to Datadog.

You can run the agent locally with Docker:

```bash
docker run -d \
  --name dd-agent \
  -e DD_API_KEY=$DD_API_KEY \
  -e DD_SITE="us3.datadoghq.com" \
  -e DD_HOSTNAME=dd-agent-local \
  -e DD_APM_ENABLED=true \
  -e DD_DATA_STREAMS_ENABLED=true \
  -p 8126:8126 \
  gcr.io/datadoghq/agent:latest
```

### Usage

```bash
cd SendCheckpointDdTrace

# Send a checkpoint with default name
dotnet run

# Send a checkpoint with a custom name
dotnet run -- my-checkpoint

# Send a checkpoint with a custom name and transaction ID
dotnet run -- my-checkpoint my-transaction-123
```

### Example output

```
Sending checkpoint via dd-trace...
  Trace agent: http://localhost:8126
  Transaction ID: a1b2c3d4-e5f6-7890-abcd-ef1234567890
  Checkpoint: test-checkpoint
  Service: datadog-checkpoints-app
  Environment: local
  Checkpoint tracked on span
Checkpoint sent successfully!
Flushing tracer...
Flush complete.
```

### How it works

1. Uses `Tracer.Instance.StartActive()` to create a trace span
2. Calls `SpanContextInjector.InjectIncludingDsm()` to set a DSM checkpoint on the span — this is the .NET equivalent of Node.js `tracer.dataStreamsCheckpointer.trackTransaction()`
3. Calls `Tracer.Instance.FlushAsync()` to force the tracer to immediately send pending traces to the agent
4. The Datadog Agent forwards the data to Datadog

### Additional dd-trace environment variables

| Environment Variable | Description | Default |
|---|---|---|
| `DD_AGENT_HOST` | Datadog Agent hostname | `localhost` |
| `DD_TRACE_AGENT_PORT` | Datadog Agent trace port | `8126` |
| `DD_TRACE_AGENT_URL` | Full URL to a remote Datadog Agent (overrides host/port) | — |
| `DD_DATA_STREAMS_ENABLED` | Enable DSM (must be `true`) | `false` |

## Using a Remote Datadog Agent

### Option 1: Direct HTTP API (remote agent)

No changes needed — Option 1 sends directly to Datadog's intake API, not via an agent.

### Option 2: dd-trace (remote agent)

Set `DD_TRACE_AGENT_URL` to point the tracer at the remote agent instead of `localhost:8126`:

```bash
export DD_TRACE_AGENT_URL="https://az-eun-development-datadog-agents-01.my.flipdishdev.com:443"

cd SendCheckpointDdTrace
dotnet run -- order-placed order-123
```

## Testing end-to-end

```bash
# Option 1 (direct HTTP):
cd SendCheckpoint
dotnet run -- order-placed order-123
dotnet run -- order-completed order-123

# Option 2 (dd-trace, requires agent):
cd SendCheckpointDdTrace
dotnet run -- order-placed order-123
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
