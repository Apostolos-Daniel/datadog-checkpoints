import * as https from 'https';
import * as zlib from 'zlib';
import * as crypto from 'crypto';

interface TransactionEvent {
  transaction_id: string;
  checkpoint: string;
  timestamp_nanos: string;
}

interface CheckpointPayload {
  transactions: TransactionEvent[];
  service: string;
  environment: string;
}

const PIPELINE_STATS_URL = 'https://trace.agent.us3.datadoghq.com/api/v0.1/pipeline_stats';

function generateTransactionId(): string {
  return crypto.randomUUID();
}

function buildPayload(
  transactionId: string,
  checkpoint: string,
  service: string,
  environment: string
): CheckpointPayload {
  return {
    transactions: [
      {
        transaction_id: transactionId,
        checkpoint,
        timestamp_nanos: (BigInt(Date.now()) * BigInt(1_000_000)).toString(),
      },
    ],
    service,
    environment,
  };
}

function sendCheckpoint(payload: CheckpointPayload, apiKey: string): Promise<{ status: number; body: string }> {
  const jsonPayload = Buffer.from(JSON.stringify(payload));
  const gzipPayload = zlib.gzipSync(jsonPayload);
  const url = new URL(PIPELINE_STATS_URL);

  return new Promise((resolve, reject) => {
    const req = https.request(
      {
        hostname: url.hostname,
        port: 443,
        path: url.pathname,
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          'Content-Encoding': 'gzip',
          'DD-API-KEY': apiKey,
          'Content-Length': String(gzipPayload.length),
        },
      },
      (res) => {
        let data = '';
        res.on('data', (chunk) => (data += chunk));
        res.on('end', () => {
          resolve({ status: res.statusCode ?? 0, body: data });
        });
      }
    );
    req.on('error', reject);
    req.write(gzipPayload);
    req.end();
  });
}

async function main() {
  const apiKey = process.env.DD_API_KEY;
  if (!apiKey) {
    console.error('Error: DD_API_KEY environment variable is required');
    process.exit(1);
  }

  const service = process.env.DD_SERVICE || 'datadog-checkpoints-app';
  const environment = process.env.DD_ENV || 'local';
  const checkpoint = process.argv[2] || 'test-checkpoint';
  const transactionId = process.argv[3] || generateTransactionId();

  const payload = buildPayload(transactionId, checkpoint, service, environment);

  console.log('Sending checkpoint to Datadog...');
  console.log('  Transaction ID:', transactionId);
  console.log('  Checkpoint:', checkpoint);
  console.log('  Service:', service);
  console.log('  Environment:', environment);

  const { status, body } = await sendCheckpoint(payload, apiKey);

  console.log('  Status:', status);
  console.log('  Response:', body);

  if (status >= 200 && status < 300) {
    console.log('Checkpoint sent successfully!');
  } else {
    console.error('Failed to send checkpoint');
    process.exit(1);
  }
}

main();
