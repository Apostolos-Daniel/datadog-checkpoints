import tracer from 'dd-trace';
import * as crypto from 'crypto';

function traceAgentDestination(): string {
  const fromEnv = process.env.DD_TRACE_AGENT_URL?.trim();
  if (fromEnv) return fromEnv;
  const host = process.env.DD_AGENT_HOST || 'localhost';
  const port = process.env.DD_TRACE_AGENT_PORT || '8126';
  return `http://${host}:${port}`;
}

// Initialize dd-trace with Data Streams Monitoring enabled
tracer.init({
  service: process.env.DD_SERVICE || 'datadog-checkpoints-app',
  env: process.env.DD_ENV || 'local',
  url: process.env.DD_TRACE_AGENT_URL || undefined,
  dsmEnabled: true,
});

function generateTransactionId(): string {
  return crypto.randomUUID();
}

async function main() {
  const checkpoint = process.argv[2] || 'test-checkpoint';
  const transactionId = process.argv[3] || generateTransactionId();

  console.log('Sending checkpoint via dd-trace...');
  console.log('  Trace agent:', traceAgentDestination());
  console.log('  Transaction ID:', transactionId);
  console.log('  Checkpoint:', checkpoint);
  console.log('  Service:', process.env.DD_SERVICE || 'datadog-checkpoints-app');
  console.log('  Environment:', process.env.DD_ENV || 'local');

  // Create a span and track the checkpoint within it
  await tracer.trace('checkpoint.send', async (span) => {
    tracer.dataStreamsCheckpointer.trackTransaction(transactionId, checkpoint, span);
    console.log('  Checkpoint tracked on span');
  });

  console.log('Checkpoint sent successfully!');

  // Allow time for the dd-trace agent to flush before exiting
  console.log('Waiting for tracer to flush...');
  await new Promise<void>((resolve) => setTimeout(resolve, 3000));
}

main();
