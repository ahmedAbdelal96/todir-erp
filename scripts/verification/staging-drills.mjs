import { execFile } from "node:child_process";
import { promisify } from "node:util";
import { pbkdf2Sync, randomBytes, randomUUID } from "node:crypto";

const exec = promisify(execFile);
const api = process.env.STAGING_API_URL ?? "http://127.0.0.1:18080";
const rabbitContainer = process.env.STAGING_RABBIT_CONTAINER;
const postgresContainer = process.env.STAGING_POSTGRES_CONTAINER;
const workerContainer = process.env.STAGING_WORKER_CONTAINER;
const workerSecondaryContainer = process.env.STAGING_WORKER_SECONDARY_CONTAINER;
const minioInitContainer = process.env.STAGING_MINIO_INIT_CONTAINER;
const expectWorkerCrash = process.env.STAGING_EXPECT_WORKER_CRASH === "true";
const replayToken = process.env.STAGING_REPLAY_TOKEN;
const databaseName = process.env.STAGING_DATABASE_NAME ?? "todir_staging";
const dockerSql = sql => exec("docker", ["exec", postgresContainer, "psql", "-U", "postgres", "-d", databaseName, "-v", "ON_ERROR_STOP=1", "-c", sql]);
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
const responseCookies = response => typeof response.headers.getSetCookie === "function"
  ? response.headers.getSetCookie().map(cookie => cookie.split(";", 1)[0])
  : (response.headers.get("set-cookie") ?? "").split(/,(?=[^;]+=)/).filter(Boolean).map(cookie => cookie.split(";", 1)[0]);
const rawResponseCookies = response => typeof response.headers.getSetCookie === "function"
  ? response.headers.getSetCookie()
  : (response.headers.get("set-cookie") ?? "").split(/,(?=[^;]+=)/).filter(Boolean);
const mergeCookies = (...cookieLists) => [...new Map(cookieLists.flatMap(cookieList => Array.isArray(cookieList) ? cookieList : cookieList.split(/;\s*(?=[^;=]+=)/).filter(Boolean)).map(cookie => [cookie.split("=", 1)[0], cookie])).values()].join("; ");

if (minioInitContainer) {
  let status = "";
  for (let attempt = 0; attempt < 30; attempt++) {
    status = (await exec("docker", ["inspect", "-f", "{{.State.Status}}", minioInitContainer])).stdout.trim();
    if (status === "exited") break;
    await new Promise(resolve => setTimeout(resolve, 1000));
  }
  const exitCode = (await exec("docker", ["inspect", "-f", "{{.State.ExitCode}}", minioInitContainer])).stdout.trim();
  if (status !== "exited" || exitCode !== "0") throw new Error(`private object-storage bucket initialization failed: status=${status};exit=${exitCode}`);
  console.log("object-storage-bucket=private-files;anonymous-access=disabled");
}

const live = await Promise.all(Array.from({ length: 100 }, () => request("/health/live")));
if (live.some(result => result.status !== 200)) throw new Error("live health load did not remain available");
console.log(`health-load=${JSON.stringify(summarize(live.map(result => result.elapsedMs)))}`);
const shellResponse = await fetch(`${api}/`);
if (shellResponse.status !== 200 || !shellResponse.headers.get("strict-transport-security") || !shellResponse.headers.get("content-security-policy")) throw new Error("external TLS security headers were not enforced by the staging edge");
console.log("external-tls-security-headers=passed");

const modules = await Promise.all(Array.from({ length: 100 }, () => request("/api/v1/modules")));
if (modules.some(result => result.status !== 200)) throw new Error("module registry load did not remain available");
console.log(`module-registry-load=${JSON.stringify(summarize(modules.map(result => result.elapsedMs)))}`);

if (workerSecondaryContainer) {
  const secondaryState = (await exec("docker", ["inspect", "-f", "{{.State.Running}}", workerSecondaryContainer])).stdout.trim();
  if (secondaryState !== "true") throw new Error(`secondary worker was not running for clustered scheduler verification: ${secondaryState}`);
  console.log("clustered-workers=2;secondary=running");
}

const permissionChecks = await Promise.all(Array.from({ length: 20 }, () => request("/api/v1/authorization/check?permission=kernel.read")));
if (permissionChecks.some(result => result.status !== 401)) throw new Error("unauthenticated permission checks were not rejected");
console.log(`authorization-denials=${permissionChecks.length}`);

const drillTenantId = randomUUID();
const secondaryTenantId = randomUUID();
const drillIdentityId = randomUUID();
const maintenanceGrantId = randomUUID();
const restartedMaintenanceGrantId = randomUUID();
const drillEmail = `drill-${drillIdentityId.replaceAll("-", "")}@example.com`;
const drillPassword = "DrillPassword-Only-For-CI";
if (postgresContainer) {
  const salt = randomBytes(16).toString("base64");
  const hash = pbkdf2Sync(drillPassword, Buffer.from(salt, "base64"), 120_000, 32, "sha256").toString("base64");
  await dockerSql(`insert into platform_tenancy.tenant (id, slug, display_name, status, created_at, version) values ('${drillTenantId}', 'auth-${drillTenantId.replaceAll("-", "")}', 'Auth Drill', 'Active', now(), 1), ('${secondaryTenantId}', 'auth-${secondaryTenantId.replaceAll("-", "")}', 'Secondary Drill', 'Active', now(), 1); insert into platform_iam.identity (id, normalized_email, email_verified, password_hash, status, created_at, security_stamp, version) values ('${drillIdentityId}', '${drillEmail.toUpperCase()}', true, 'pbkdf2-sha256$120000$${salt}$${hash}', 'Active', now(), '${randomUUID()}', 1); insert into platform_iam.membership (id, tenant_id, identity_id, display_name, status, created_at, version) values ('${randomUUID()}', '${drillTenantId}', '${drillIdentityId}', 'Auth Drill', 'Active', now(), 1), ('${randomUUID()}', '${secondaryTenantId}', '${drillIdentityId}', 'Secondary Drill', 'Active', now(), 1); insert into platform_iam.support_access_grant (id, tenant_id, support_actor_id, reason, expires_at, status, created_at) values ('${maintenanceGrantId}', '${drillTenantId}', '${drillIdentityId}', 'scheduler expiry verification', now() - interval '1 minute', 'Approved', now() - interval '2 minutes');`);
  const csrfResponse = await fetch(`${api}/api/v1/auth/csrf`);
  if (csrfResponse.status !== 200) throw new Error(`csrf bootstrap failed: ${csrfResponse.status}`);
  const csrf = (await csrfResponse.json()).token;
  const csrfCookie = mergeCookies(responseCookies(csrfResponse));
  const loginResponse = await fetch(`${api}/api/v1/auth/login`, {
    method: "POST",
    headers: { "content-type": "application/json", "x-csrf-token": csrf, Cookie: csrfCookie },
    body: JSON.stringify({ email: drillEmail, password: drillPassword, tenantId: drillTenantId })
  });
  if (loginResponse.status !== 200) throw new Error(`authenticated login failed: ${loginResponse.status}`);
  const sessionSetCookie = rawResponseCookies(loginResponse).find(cookie => cookie.toLowerCase().startsWith("todir_session="));
  if (!sessionSetCookie || !/secure/i.test(sessionSetCookie) || !/httponly/i.test(sessionSetCookie) || !/samesite=strict/i.test(sessionSetCookie)) throw new Error("session cookie security attributes were not enforced");
  console.log("session-cookie-security=passed");
  let sessionCookie = mergeCookies(responseCookies(loginResponse), responseCookies(csrfResponse));
  const session = await fetch(`${api}/api/v1/session`, { headers: { Cookie: sessionCookie } });
  if (session.status !== 200) throw new Error(`authenticated session failed: ${session.status}`);
  const primaryTenantSessionCookie = sessionCookie;
  const switchCsrfResponse = await fetch(`${api}/api/v1/auth/csrf`, { headers: { Cookie: sessionCookie } });
  if (switchCsrfResponse.status !== 200) throw new Error(`tenant switch csrf bootstrap failed: ${switchCsrfResponse.status}`);
  const switchCsrf = (await switchCsrfResponse.json()).token;
  const switchCookie = mergeCookies(sessionCookie, responseCookies(switchCsrfResponse));
  const switchResponse = await fetch(`${api}/api/v1/context/tenant`, {
    method: "POST",
    headers: { "content-type": "application/json", "x-csrf-token": switchCsrf, Cookie: switchCookie },
    body: JSON.stringify({ tenantId: secondaryTenantId })
  });
  if (switchResponse.status !== 204) throw new Error(`tenant context switch failed: ${switchResponse.status};detail=${(await switchResponse.text()).slice(0, 200)}`);
  sessionCookie = mergeCookies(switchCookie, responseCookies(switchResponse));
  const switched = await fetch(`${api}/api/v1/session`, { headers: { Cookie: sessionCookie } });
  if (switched.status !== 200 || (await switched.json()).tenantId !== secondaryTenantId) throw new Error("tenant context did not switch to the second tenant");
  const authenticatedPermissions = await Promise.all(Array.from({ length: 20 }, () => fetch(`${api}/api/v1/authorization/check?permission=kernel.read`, { headers: { Cookie: sessionCookie } })));
  if (authenticatedPermissions.some(result => result.status !== 403)) throw new Error("backend permission evaluation did not reject the unassigned permission");
  const noisyTenantRequests = await Promise.all(Array.from({ length: 100 }, () => fetch(`${api}/api/v1/authorization/check?permission=kernel.read`, { headers: { Cookie: primaryTenantSessionCookie } })));
  const quietTenantRequests = await Promise.all(Array.from({ length: 20 }, () => fetch(`${api}/api/v1/session`, { headers: { Cookie: sessionCookie } })));
  if (noisyTenantRequests.some(result => result.status !== 403)) throw new Error("noisy tenant permission workload was not isolated");
  if (quietTenantRequests.some(result => result.status !== 200)) throw new Error("quiet tenant session workload was degraded during noisy tenant load");
  console.log(`authentication-load=${authenticatedPermissions.length};tenant-switch=isolated;permission-evaluation=forbidden;noisy-tenant=${noisyTenantRequests.length};quiet-tenant=${quietTenantRequests.length}`);
}

const authBurst = await Promise.all(Array.from({ length: 20 }, () => request("/api/v1/auth/csrf")));
const limited = authBurst.filter(result => result.status === 429).length;
if (limited === 0) throw new Error("authentication rate limiting did not activate");
console.log(`auth-rate-limit-429=${limited}`);

if (rabbitContainer && postgresContainer) {
  let queueType = "";
  for (let attempt = 0; attempt < 30; attempt++) {
    try {
      const queues = (await exec("docker", ["exec", rabbitContainer, "rabbitmqctl", "-p", "/erp-staging", "list_queues", "name", "type"])).stdout.trim().split("\n").map(line => line.trim());
      const queue = queues.find(line => line.startsWith("platform-outbox-probe\t") || line.startsWith("platform-outbox-probe "));
      if (queue) {
        queueType = queue.split(/\s+/).at(-1) ?? "";
        break;
      }
    } catch (error) {
      if (attempt === 29) throw error;
    }
    await new Promise(resolve => setTimeout(resolve, 1000));
  }
  if (queueType !== "quorum") throw new Error(`critical consumer endpoint is not a quorum queue: ${queueType || "missing"}`);
  console.log("critical-queue=platform-outbox-probe;type=quorum");
  const tenantId = drillTenantId;
  const messageIds = Array.from({ length: 20 }, () => randomUUID());
  const sampleMessageId = randomUUID();
  const sampleItemId = randomUUID();
  const poisonMessageId = randomUUID();
  const messageValues = [
    ...messageIds.map(messageId => `('${messageId}', 'StagingDrillV1', '{}'::jsonb, '${tenantId}', now(), '${randomUUID()}', '${randomUUID()}', 'kernel', 'Pending')`),
    `('${sampleMessageId}', 'SampleEntityCreatedV1', '{"itemId":"${sampleItemId}","Name":"staging-sample"}'::jsonb, '${tenantId}', now(), '${randomUUID()}', '${sampleItemId}', 'sample', 'Pending')`,
    `('${poisonMessageId}', 'StagingPoisonV1', '{}'::jsonb, '${tenantId}', now(), '${randomUUID()}', '${randomUUID()}', 'kernel', 'Pending')`
  ].join(", ");
  const normalMessageIds = [...messageIds, sampleMessageId];
  const sql = `insert into platform_messaging.outbox_message (id, message_type, payload, tenant_id, occurred_at, correlation_id, causation_id, source_module, status) values ${messageValues};`;
  await exec("docker", ["stop", "--time", "1", rabbitContainer]);
  try {
    await dockerSql(sql);
    await new Promise(resolve => setTimeout(resolve, 8000));
    const during = Number((await exec("docker", ["exec", postgresContainer, "psql", "-U", "postgres", "-d", databaseName, "-At", "-c", `select count(*) from platform_messaging.outbox_message where tenant_id = '${tenantId}' and status in ('Pending', 'Failed')`])).stdout.trim());
    if (during !== normalMessageIds.length + 1) throw new Error(`outbox did not remain durable during broker outage: ${during}/${normalMessageIds.length + 1}`);
    console.log(`broker-outage-outbox-pending=${during}`);
  } finally {
    await exec("docker", ["start", rabbitContainer]);
  }
  let recovered = 0;
  for (let attempt = 0; attempt < 30; attempt++) {
    recovered = Number((await exec("docker", ["exec", postgresContainer, "psql", "-U", "postgres", "-d", databaseName, "-At", "-c", `select count(*) from platform_messaging.outbox_message where tenant_id = '${tenantId}' and status = 'Published'`])).stdout.trim());
    if (recovered === normalMessageIds.length + 1) break;
    await new Promise(resolve => setTimeout(resolve, 2000));
  }
  if (recovered !== normalMessageIds.length + 1) throw new Error(`outbox did not recover after broker restart: ${recovered}/${normalMessageIds.length + 1}`);
  console.log(`broker-recovery=published;outbox-throughput=${recovered}`);
  if (workerContainer) {
    if (expectWorkerCrash) {
      let workerState = "";
      for (let attempt = 0; attempt < 30; attempt++) {
        workerState = (await exec("docker", ["inspect", "-f", "{{.State.Status}}", workerContainer])).stdout.trim();
        if (workerState === "exited") break;
        await new Promise(resolve => setTimeout(resolve, 1000));
      }
      if (workerState !== "exited") throw new Error(`worker did not stop after controlled post-commit failure injection: ${workerState}`);
      await exec("docker", ["start", workerContainer]);
    } else {
      await exec("docker", ["restart", workerContainer]);
    }
    await new Promise(resolve => setTimeout(resolve, 5000));
    const running = (await exec("docker", ["inspect", "-f", "{{.State.Running}}", workerContainer])).stdout.trim();
    if (running !== "true") throw new Error("worker did not recover after controlled restart");
    await dockerSql(`insert into platform_iam.support_access_grant (id, tenant_id, support_actor_id, reason, expires_at, status, created_at) values ('${restartedMaintenanceGrantId}', '${tenantId}', '${drillIdentityId}', 'scheduler restart verification', now() - interval '1 minute', 'Approved', now() - interval '2 minutes');`);
    let schedulerStatus = "";
    for (let attempt = 0; attempt < 30; attempt++) {
      schedulerStatus = (await exec("docker", ["exec", postgresContainer, "psql", "-U", "postgres", "-d", databaseName, "-At", "-c", `select status from platform_iam.support_access_grant where id = '${restartedMaintenanceGrantId}'`])).stdout.trim();
      if (schedulerStatus === "Expired") break;
      await new Promise(resolve => setTimeout(resolve, 1000));
    }
    if (schedulerStatus !== "Expired") throw new Error(`persistent scheduler did not expire the controlled grant: ${schedulerStatus || "missing"}`);
    console.log("scheduler-expiry=passed;clustered-workers=2");
    let inboxCount = 0;
    let sampleInbox = 0;
    for (let attempt = 0; attempt < 15; attempt++) {
      inboxCount = Number((await exec("docker", ["exec", postgresContainer, "psql", "-U", "postgres", "-d", databaseName, "-At", "-c", `select count(*) from platform_messaging.inbox_message where tenant_id = '${tenantId}' and consumer_endpoint = 'platform-outbox-probe' and message_id in (${normalMessageIds.map(id => `'${id}'`).join(",")})`])).stdout.trim());
      sampleInbox = Number((await exec("docker", ["exec", postgresContainer, "psql", "-U", "postgres", "-d", databaseName, "-At", "-c", `select count(*) from platform_messaging.inbox_message where tenant_id = '${tenantId}' and consumer_endpoint = 'sample-projection' and message_id = '${sampleMessageId}' and processed_at is not null`])).stdout.trim());
      if (inboxCount === normalMessageIds.length && sampleInbox === 1) break;
      await new Promise(resolve => setTimeout(resolve, 1000));
    }
    const duplicateInbox = Number((await exec("docker", ["exec", postgresContainer, "psql", "-U", "postgres", "-d", databaseName, "-At", "-c", `select coalesce(max(message_count), 0) from (select message_id, count(*) as message_count from platform_messaging.inbox_message where tenant_id = '${tenantId}' and consumer_endpoint = 'platform-outbox-probe' and message_id in (${normalMessageIds.map(id => `'${id}'`).join(",")}) group by message_id) counts`])).stdout.trim());
    const sampleProjection = Number((await exec("docker", ["exec", postgresContainer, "psql", "-U", "postgres", "-d", databaseName, "-At", "-c", `select count(*) from sample.sample_projection where tenant_id = '${tenantId}' and item_id = '${sampleItemId}'`])).stdout.trim());
    if (inboxCount !== normalMessageIds.length || sampleInbox !== 1 || duplicateInbox > 1 || sampleProjection !== 1) throw new Error(`consumer did not preserve idempotency or sample projection after worker restart: inbox=${inboxCount}/${normalMessageIds.length};sample-inbox=${sampleInbox};max=${duplicateInbox};sample-projection=${sampleProjection}`);
    console.log(`worker-restart=running;inbox-rows=${inboxCount};sample-inbox=${sampleInbox};sample-projection=${sampleProjection}`);

    let poisonErrors = 0;
    for (let attempt = 0; attempt < 30; attempt++) {
      poisonErrors = Number((await exec("docker", ["exec", postgresContainer, "psql", "-U", "postgres", "-d", databaseName, "-At", "-c", `select count(*) from platform_messaging.message_error where tenant_id = '${tenantId}' and message_id = '${poisonMessageId}' and resolved = false`])).stdout.trim());
      if (poisonErrors > 0) break;
      await new Promise(resolve => setTimeout(resolve, 1000));
    }
    if (poisonErrors !== 1 || !replayToken) throw new Error(`poison message did not reach the durable error path: errors=${poisonErrors};replay-token=${Boolean(replayToken)}`);
    await exec("docker", ["exec", workerContainer, "touch", "/tmp/poison-message.allow"]);
    const csrfForReplay = await fetch(`${api}/api/v1/auth/csrf`, { headers: { Cookie: sessionCookie } });
    if (csrfForReplay.status !== 200) throw new Error(`replay csrf bootstrap failed: ${csrfForReplay.status}`);
    const replayCsrf = (await csrfForReplay.json()).token;
    const replayResponse = await fetch(`${api}/api/v1/internal/messages/${poisonMessageId}/replay`, {
      method: "POST",
      headers: { "content-type": "application/json", "x-csrf-token": replayCsrf, "x-staging-replay-token": replayToken, Cookie: mergeCookies(sessionCookie, responseCookies(csrfForReplay)) },
      body: JSON.stringify({ tenantId, operatorId: drillIdentityId, reason: "controlled poison-message replay verification" })
    });
    if (replayResponse.status !== 204) throw new Error(`message replay endpoint failed: ${replayResponse.status}`);
    let poisonInbox = 0;
    let resolvedErrors = 0;
    let replayAudits = 0;
    for (let attempt = 0; attempt < 30; attempt++) {
      poisonInbox = Number((await exec("docker", ["exec", postgresContainer, "psql", "-U", "postgres", "-d", databaseName, "-At", "-c", `select count(*) from platform_messaging.inbox_message where tenant_id = '${tenantId}' and message_id = '${poisonMessageId}' and processed_at is not null`])).stdout.trim());
      resolvedErrors = Number((await exec("docker", ["exec", postgresContainer, "psql", "-U", "postgres", "-d", databaseName, "-At", "-c", `select count(*) from platform_messaging.message_error where tenant_id = '${tenantId}' and message_id = '${poisonMessageId}' and resolved = true`])).stdout.trim());
      replayAudits = Number((await exec("docker", ["exec", postgresContainer, "psql", "-U", "postgres", "-d", databaseName, "-At", "-c", `select count(*) from platform_audit.audit_entry where tenant_id = '${tenantId}' and entity_id = '${poisonMessageId}' and action = 'message_replayed'`])).stdout.trim());
      if (poisonInbox === 1 && resolvedErrors === 1 && replayAudits === 1) break;
      await new Promise(resolve => setTimeout(resolve, 1000));
    }
    if (poisonInbox !== 1 || resolvedErrors !== 1 || replayAudits !== 1) throw new Error(`message replay was not durable and audited: inbox=${poisonInbox};resolved=${resolvedErrors};audits=${replayAudits}`);
    console.log(`poison-error=recorded;replay=audited;inbox-rows=${poisonInbox}`);
  }
  await dockerSql(`delete from sample.sample_projection where tenant_id = '${tenantId}'; delete from platform_messaging.message_error where tenant_id = '${tenantId}'; delete from platform_audit.audit_entry where tenant_id = '${tenantId}' and entity_id = '${poisonMessageId}'; delete from platform_messaging.inbox_message where tenant_id = '${tenantId}'; delete from platform_messaging.outbox_message where tenant_id = '${tenantId}'; delete from platform_iam.support_access_grant where id in ('${maintenanceGrantId}', '${restartedMaintenanceGrantId}'); delete from platform_iam.membership where tenant_id in ('${tenantId}', '${secondaryTenantId}'); delete from platform_iam.identity where id = '${drillIdentityId}'; delete from platform_tenancy.tenant where id in ('${tenantId}', '${secondaryTenantId}');`);
} else {
  console.log("broker-recovery=not-run-no-staging-container-identifiers");
}
