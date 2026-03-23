# Datadog Checkpoints

A simple TypeScript app that sends sample/test checkpoints to Datadog's [Data Streams Monitoring](https://docs.datadoghq.com/data_streams/) pipeline stats API.

Once checkpoints are sent, they appear under **Data Streams Monitoring > Transactions > Business Transaction Tracking** in Datadog:

![Business Transaction Tracking](docs/datadog-transactions.png)

## Prerequisites

- [Node.js](https://nodejs.org/) (v16+)
- A [Datadog API key](https://docs.datadoghq.com/account_management/api-app-keys/)

## Setup

```bash
# Install dependencies
npm install
```

## Configuration

| Environment Variable | Description | Default |
|---|---|---|
| `DD_API_KEY` | **(Required)** Your Datadog API key | — |
| `DD_SERVICE` | Service name reported to Datadog | `datadog-checkpoints-app` |
| `DD_ENV` | Environment name reported to Datadog | `local` |

## Usage

```bash
# Set your API key
export DD_API_KEY="your-api-key"

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
  Transaction ID: a1b2c3d4-e5f6-7890-abcd-ef1234567890
  Checkpoint: test-checkpoint
  Service: datadog-checkpoints-app
  Environment: local
  Status: 202
  Response:
Checkpoint sent successfully!
```

## Build

```bash
# Compile TypeScript to JavaScript
npm run build

# Run the compiled version
node dist/send-checkpoint.js
```

## How it works

1. Builds a checkpoint payload containing a transaction ID, checkpoint name, and nanosecond-precision timestamp
2. Gzip-compresses the JSON payload
3. Sends it via HTTPS POST to `https://trace.agent.us3.datadoghq.com/api/v0.1/pipeline_stats`
4. The checkpoint then appears in Datadog under **Data Streams Monitoring > Transactions**

## Creating a Transaction Pipeline

Once your checkpoints are being sent, you can create a Transaction Tracking Pipeline in Datadog:

1. Go to **Data Streams Monitoring > Transactions**
2. Click **Create Transaction Pipeline**
3. Select the **Manual Checkpoints** tab
4. Give your pipeline a name and set an SLO duration
5. Select your checkpoints from the dropdown for the start and end steps

![Create Transaction Pipeline](docs/create-transaction-pipeline.png)
