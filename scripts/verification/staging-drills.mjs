import { execFile } from "node:child_process";
import { promisify } from "node:util";
import { randomUUID } from "node:crypto";

const exec = promisify(execFile);
const api = process.env.STAGING_API_URL ?? "http://127.0.0.1:18080";
const request = async path => {
  const started = performance.now();
  const response = await fetch(`${api}${path}`);
  return { status: response.status, elapsedMs: performance.now() - started };
};
const summarize = values => ({
  count: values.length,
  minMs: Math.round(Math.min(...values)),
  maxMs: Math.round(Math.max(...values)),
  averageMs: Math.round(values.reduce((sum, value) => sum + value, 0) / values.length)
});

const live = await Promise.all(Array.from({ length: 100 }, () => request("/health/live")));
if (live.some(result => result.status !== 200)) throw new Error("live health load did not remain available");
console.log(`health-load=${JSON.stringify(summarize(live.map(result => result.elapsedMs)))}`);

const modules = await Promise.all(Array.from({ length: 100 }, () => request("/api/v1/modules")));
if (modules.some(result => result.status !== 200)) throw new Error("module registry load did not remain available");
console.log(`module-registry-load=${JSON.stringify(summarize(modules.map(result => result.elapsedMs)))}`);

const permissionChecks = await Promise.all(Array.from({ length: 20 }, () => request("/api/v1/authorization/check?permission=kernel.read")));
if (permissionChecks.some(result => result.status !== 401)) throw new Error("unauthenticated permission checks were not rejected");
console.log(`authorization-denials=${permissionChecks.length}`);

const authBurst = await Promise.all(Array.from({ length: 20 }, () => request("/api/v1/auth/csrf")));
const limited = authBurst.filter(result => result.status === 429).length;
if (limited === 0) throw new Error("authentication rate limiting did not activate");
console.log(`auth-rate-limit-429=${limited}`);

const rabbitContainer = process.env.STAGING_RABBIT_CONTAINER;
const postgresContainer = process.env.STAGING_POSTGRES_CONTAINER;
const databaseName = process.env.STAGING_DATABASE_NAME ?? "todir_staging";
if (rabbitContainer && postgresContainer) {
  const tenantId = randomUUID();
  const messageId = randomUUID();
  const sql = `insert into platform_tenancy.tenant (id, slug, display_name, status, created_at, version) values ('${tenantId}', 'drill-${tenantId.replaceAll("-", "")}', 'Drill', 'Active', now(), 1); insert into platform_messaging.outbox_message (id, message_type, payload, tenant_id, occurred_at, correlation_id, causation_id, source_module, status) values ('${messageId}', 'StagingDrillV1', '{}'::jsonb, '${tenantId}', now(), '${randomUUID()}', '${randomUUID()}', 'kernel', 'Pending');`;
  await exec("docker", ["pause", rabbitContainer]);
  try {
    await exec("docker", ["exec", postgresContainer, "psql", "-U", "postgres", "-d", databaseName, "-v", "ON_ERROR_STOP=1", "-c", sql]);
    await new Promise(resolve => setTimeout(resolve, 8000));
    const during = (await exec("docker", ["exec", postgresContainer, "psql", "-U", "postgres", "-d", databaseName, "-At", "-c", `select status from platform_messaging.outbox_message where id = '${messageId}'`])).stdout.trim();
    if (!/^Pending|Failed$/.test(during)) throw new Error(`outbox did not remain durable during broker outage: ${during}`);
    console.log(`broker-outage-outbox-status=${during}`);
  } finally {
    await exec("docker", ["unpause", rabbitContainer]);
  }
  let recovered = "";
  for (let attempt = 0; attempt < 30; attempt++) {
    recovered = (await exec("docker", ["exec", postgresContainer, "psql", "-U", "postgres", "-d", databaseName, "-At", "-c", `select status from platform_messaging.outbox_message where id = '${messageId}'`])).stdout.trim();
    if (recovered === "Published") break;
    await new Promise(resolve => setTimeout(resolve, 2000));
  }
  if (recovered !== "Published") throw new Error(`outbox did not recover after broker restart: ${recovered}`);
  console.log("broker-recovery=published");
  const workerContainer = process.env.STAGING_WORKER_CONTAINER;
  if (workerContainer) {
    await exec("docker", ["restart", workerContainer]);
    await new Promise(resolve => setTimeout(resolve, 5000));
    const running = (await exec("docker", ["inspect", "-f", "{{.State.Running}}", workerContainer])).stdout.trim();
    if (running !== "true") throw new Error("worker did not recover after controlled restart");
    let inboxCount = 0;
    for (let attempt = 0; attempt < 15; attempt++) {
      inboxCount = Number((await exec("docker", ["exec", postgresContainer, "psql", "-U", "postgres", "-d", databaseName, "-At", "-c", `select count(*) from platform_messaging.inbox_message where message_id = '${messageId}'`])).stdout.trim());
      if (inboxCount === 1) break;
      await new Promise(resolve => setTimeout(resolve, 1000));
    }
    if (inboxCount !== 1) throw new Error(`consumer did not record one idempotent inbox row after worker restart: ${inboxCount}`);
    console.log(`worker-restart=running;inbox-rows=${inboxCount}`);
  }
  await exec("docker", ["exec", postgresContainer, "psql", "-U", "postgres", "-d", databaseName, "-v", "ON_ERROR_STOP=1", "-c", `delete from platform_messaging.outbox_message where id = '${messageId}'; delete from platform_tenancy.tenant where id = '${tenantId}';`]);
} else {
  console.log("broker-recovery=not-run-no-staging-container-identifiers");
}
