# Datadog Checkpoints

Sample apps that send test checkpoints to Datadog's [Data Streams Monitoring](https://docs.datadoghq.com/data_streams/) for Business Transaction Tracking.

Available in **TypeScript/Node.js** (this directory) and **C# .NET** ([`dotnet/`](dotnet/)).

Once checkpoints are sent, they appear under **Data Streams Monitoring > Transactions > Business Transaction Tracking** in Datadog:

![Business Transaction Tracking](docs/datadog-transactions.png)

## Prerequisites (TypeScript)

- [Node.js](https://nodejs.org/) (v16+)
- A [Datadog API key](https://docs.datadoghq.com/account_management/api-app-keys/)

> For the C# .NET version, see [`dotnet/README.md`](dotnet/README.md).

## Setup

```bash
# Install dependencies
npm install
```

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
npm run send

# Send a checkpoint with a custom name
npm run send -- my-checkpoint

# Send a checkpoint with a custom name and transaction ID
npm run send -- my-checkpoint my-transaction-123
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

Use Option 1 as the smoke test for the **direct pipeline stats intake** (no agent). The script prints the exact URL, HTTP **Status**, and **Response** body—those three lines tell you whether the call reached Datadog and how the API responded.

1. **Set `DD_API_KEY`** (same key you use in the Datadog UI for your organisation).
2. Run `npm run send` (or pass a checkpoint / transaction id as shown in [Usage](#usage)).
3. **Read the output:**
   - **Status in the 2xx range** — the HTTP request was accepted by that intake host. If checkpoints still do not show up under **Data Streams Monitoring > Transactions**, confirm you are logged into the same Datadog **site** and org, and allow a short delay before refreshing the UI.
   - **401 / 403** — API key rejected or not authorised for that intake; regenerate or copy the key from [API Keys](https://docs.datadoghq.com/account_management/api-app-keys/).
   - **404 or other 4xx / 5xx** — often a **wrong intake host for your site**. The URL in `src/send-checkpoint.ts` is hardcoded to the **US3** trace agent (`trace.agent.us3.datadoghq.com`). Your organisation may use another [Datadog site](https://docs.datadoghq.com/getting_started/site/) (for example US1, EU, or another region). The trace intake hostname must match that site (same pattern as your Agent `DD_SITE`: use the corresponding `trace.agent.*` host from Datadog’s documentation, or align with the host your Agent uses for trace traffic). Update `PIPELINE_STATS_URL` in `send-checkpoint.ts` until the **Status** is 2xx.
   - **Network or TLS errors** in the console (no HTTP status) — local firewall, proxy, or DNS blocking `trace.agent.*`; fix connectivity or try from another network.

**Comparing with the agent path:** If you are unsure whether the problem is the direct URL or your account setup, run **Option 2** with a local Agent whose `DD_SITE` matches your organisation (see [Option 2](#option-2-using-dd-trace) and [Testing with dd-trace](#testing-with-dd-trace)). If Option 2 works but Option 1 does not, the direct intake host in code is usually the mismatch—Option 2 relies on the Agent to route to the correct site.

## Option 2: Using dd-trace

Uses the official [`dd-trace`](https://github.com/DataDog/dd-trace-js) library with the `trackTransaction` API from the Data Streams Monitoring checkpointer. This is the recommended approach for production applications that already use `dd-trace`.

### Prerequisites (dd-trace)

A running [Datadog Agent](https://docs.datadoghq.com/agent/) is required. The `dd-trace` library sends data to the local agent, which forwards it to Datadog.

**Docker:** To run the agent in a container, install [Docker Desktop](https://www.docker.com/products/docker-desktop/) (macOS and Windows) or [Docker Engine](https://docs.docker.com/engine/install/) (Linux) if you do not have Docker yet. Open **Docker Desktop** (or start your Docker daemon) and wait until it is running before you use `docker run`. If you see `Cannot connect to the Docker daemon`, the daemon is not running—start Docker Desktop and try again.

On Docker Desktop (especially macOS), the agent can exit immediately if it cannot determine a hostname inside the container. The example below sets `DD_HOSTNAME` so the agent stays up.

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

# Send a checkpoint with default name
npm run send:ddtrace

# Send a checkpoint with a custom name
npm run send:ddtrace -- my-checkpoint

# Send a checkpoint with a custom name and transaction ID
npm run send:ddtrace -- my-checkpoint my-transaction-123
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
Waiting for tracer to flush...
```

### How it works

1. Initialises `dd-trace` with `dsmEnabled: true` to enable Data Streams Monitoring
2. Creates a trace span and calls `tracer.dataStreamsCheckpointer.trackTransaction()` to record the checkpoint
3. The tracer sends the data to the local Datadog Agent, which forwards it to Datadog

### Additional dd-trace environment variables

| Environment Variable | Description | Default |
|---|---|---|
| `DD_AGENT_HOST` | Datadog Agent hostname | `localhost` |
| `DD_TRACE_AGENT_PORT` | Datadog Agent trace port | `8126` |
| `DD_TRACE_AGENT_URL` | Full URL to a remote Datadog Agent (overrides host/port) | — |
| `DD_DATA_STREAMS_ENABLED` | Enable DSM (alternative to code config) | `false` |

## Using a Remote Datadog Agent

Instead of running a local Datadog Agent, you can point to a remote/shared agent. This is useful when your team has a centralised agent running in a cloud environment.

### Option 1: Direct HTTP API (remote agent)

No changes needed — Option 1 sends directly to Datadog's intake API, not via an agent. It works the same regardless of whether you have a local agent.

### Option 2: dd-trace (remote agent)

Set `DD_TRACE_AGENT_URL` to point `dd-trace` at the remote agent instead of `localhost:8126`:

```bash
export DD_TRACE_AGENT_URL="https://az-eun-development-datadog-agents-01.my.flipdishdev.com:443"

npm run send:ddtrace -- order-placed order-123
```

This is equivalent to initialising `dd-trace` with the `url` option in code:

```javascript
require('dd-trace').init({
  logInjection: true,
  service: process.env.DD_SERVICE || 'RMS.SERVICES.JOBS',
  env: process.env.DD_ENV || 'local',
  url: process.env.DD_TRACE_AGENT_URL || 'https://az-eun-development-datadog-agents-01.my.flipdishdev.com:443',
});
```

When `DD_TRACE_AGENT_URL` is set, it overrides `DD_AGENT_HOST` and `DD_TRACE_AGENT_PORT`. No local Docker agent is needed.

## Testing with dd-trace

To test the `dd-trace` approach end-to-end:

### 1. Start a Datadog Agent

Ensure Docker is running (install or start Docker Desktop as described under **Docker** in Option 2 above).

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

### 2. Verify the agent is running

```bash
curl -s http://localhost:8126/info | head
```

### 3. Send a test checkpoint

```bash
npm run send:ddtrace -- order-placed order-123
```

### 4. Send a second checkpoint for the same transaction

```bash
npm run send:ddtrace -- order-completed order-123
```

### 5. Verify in Datadog

1. Go to **Data Streams Monitoring > Transactions** in the Datadog UI
2. You should see your manual checkpoints listed
3. Click **Create Transaction Pipeline** to build a pipeline from your checkpoints

### Testing without a Datadog Agent

If you don't have a Datadog Agent running, you can use **Option 1** (Direct HTTP API) — it sends checkpoints straight to Datadog’s pipeline stats intake without an agent. That only works when the **endpoint in `send-checkpoint.ts` matches your Datadog site**; if `npm run send` does not return a 2xx status, follow [Testing Option 1 and when the direct URL fails](#testing-option-1-and-when-the-direct-url-fails) or use Option 2 with an Agent (including a [remote agent](#option-2-dd-trace-remote-agent)) so `DD_SITE` selects the correct region.

```bash
npm run send -- order-placed order-123
npm run send -- order-completed order-123
```

## C# .NET Version

The same checkpoint app is available as a standalone .NET 9 console app in [`dotnet/`](dotnet/). It implements the Direct HTTP API approach with zero NuGet dependencies:

```bash
cd dotnet
dotnet run -- order-placed order-123
```

See [`dotnet/README.md`](dotnet/README.md) for full setup, usage, and troubleshooting.

## Build (TypeScript)

```bash
# Compile TypeScript to JavaScript
npm run build

# Run the compiled version
node dist/send-checkpoint.js
node dist/send-checkpoint-ddtrace.js
```

## Creating a Transaction Pipeline

Once your checkpoints are being sent, you can create a Transaction Tracking Pipeline in Datadog:

1. Go to **Data Streams Monitoring > Transactions**
2. Click **Create Transaction Pipeline**
3. Select the **Manual Checkpoints** tab
4. Give your pipeline a name and set an SLO duration
5. Select your checkpoints from the dropdown for the start and end steps

![Create Transaction Pipeline](docs/create-transaction-pipeline.png)
