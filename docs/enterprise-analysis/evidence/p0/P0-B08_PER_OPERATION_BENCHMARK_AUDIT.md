# P0-B08 — Per-Operation Benchmark Audit

**Result:** `PASS — ARCHIVED SAMPLES RECONCILED`  
**Runs:** diagnostic `20261125103326_38136_663ae499`; controlled load `20261125103950_28700_b2a9b5cf`; repeat `20261125091708_30864_85500d9a`  
**Source commit recorded by the harness:** `ab49df8b95a16c92804cce7405c42a9f95b0beb7`  
**Evidence boundary:** Development Seed and a disposable single-host PostgreSQL/API environment. These are not production SLOs or capacity certification.

## Reconciliation

- Diagnostic: 42 read result rows × 5 measured requests = 210 measured requests; p95/p99 are correctly labelled `INSUFFICIENT_SAMPLES`.
- Final controlled load: 63 read rows × 100 + 3 sustained-write rows × 100 = 6,600 measured requests, plus 100 two-instance samples; zero measured errors.
- Final load: `21/21` functional smoke, `3/3` security smoke and `8/8` write smoke checks.
- Each final-load cell below is `p50/p95/p99 ms; RPS; SQL statements/request; successes/requests`.
- Percentiles use nearest-rank `ceil(p*n)`, one-based. `LatencySamplesMs` in the archived JSON contains one duration per measured request and was independently reconciled against `Requests`.

## Final controlled load per-operation values

| Operation | c1 | c4 | c8 |
|---|---:|---:|---:|
| atlas.accounts | 19.24/25.49/27.67; 38.43; 8; 100/100 | 24.4/36.13/53.8; 101.83; 8; 100/100 | 26.48/51.63/74.15; 101.38; 8; 100/100 |
| atlas.balances | 32.44/44.4/50.09; 24.75; 16; 100/100 | 38.28/57.55/71.71; 76.38; 16; 100/100 | 53.67/80.58/93.76; 74.64; 16; 100/100 |
| atlas.customer-invoices | 22.14/27.6/29.93; 34.62; 9; 100/100 | 31.51/52.83/68.39; 82.9; 9; 100/100 | 32.93/49.41/71.89; 92.56; 9; 100/100 |
| atlas.effective-access | 35.36/51.76/59.26; 22.01; 10; 100/100 | 40.69/60.77/99.37; 68.9; 10; 100/100 | 47.89/102.64/129.31; 73.37; 10.03; 100/100 |
| atlas.employees | 21.48/28.77/32.68; 34.59; 8; 100/100 | 27.21/45.14/58; 96.11; 8; 100/100 | 30.6/59.58/73.2; 91.96; 8; 100/100 |
| atlas.groups | 20.16/26.22/30.39; 37.12; 8; 100/100 | 26.32/36.77/59.32; 99.64; 8; 100/100 | 31.92/55.88/80.32; 96.11; 8; 100/100 |
| atlas.health | 96.14/861.68/930.72; 4.98; 52; 100/100 | 110.28/144.2/170.81; 32.18; 52; 100/100 | 169.42/267.24/286.78; 37.98; 52.08; 100/100 |
| atlas.party-exposure | 47.01/64.18/71.39; 18.63; 26; 100/100 | 63.54/83.02/91.8; 51.05; 26; 100/100 | 87.36/120.87/144.16; 59.99; 26; 100/100 |
| atlas.projects | 21.49/29.14/36.34; 34.65; 9; 100/100 | 27.97/40.88/78.57; 89.65; 9; 100/100 | 30.8/82.1/100.66; 93.33; 9; 100/100 |
| atlas.quotations | 25.04/35.95/46.6; 30.48; 11; 100/100 | 38.62/53.51/60.78; 73.4; 11; 100/100 | 38.84/75.1/83.73; 80.28; 11; 100/100 |
| atlas.registers | 29.41/38.35/49.14; 26.35; 11; 100/100 | 35.79/58.2/64.61; 76.47; 11; 100/100 | 39.69/70.47/127.19; 78.87; 11; 100/100 |
| delta.accounts | 19.35/25.31/29.27; 38.05; 8; 100/100 | 26.33/39.87/51.97; 96.88; 8; 100/100 | 30.35/51.57/80.2; 88.62; 8; 100/100 |
| delta.balances | 31.34/41.63/44; 25.92; 16; 100/100 | 40.45/66.47/88.36; 70.78; 16; 100/100 | 50.05/88.49/102.46; 83.29; 16; 100/100 |
| delta.customer-invoices | 25.04/32.57/34.99; 30.54; 9; 100/100 | 30.72/46.45/77.94; 82.11; 9; 100/100 | 43.98/76.96/94.91; 72.34; 9; 100/100 |
| delta.effective-access | 25.74/33.18/41.67; 29.4; 10; 100/100 | 29.71/41.06/62.67; 88.58; 10; 100/100 | 35.67/54.73/68.81; 82.69; 10; 100/100 |
| delta.employees | 23.96/31.6/36.16; 32.03; 8; 100/100 | 30.41/48.96/64.66; 80.69; 8; 100/100 | 35.74/61.92/78.48; 80.33; 8; 100/100 |
| delta.groups | 21.33/27.43/30.72; 34.71; 8; 100/100 | 27.71/39.28/51.03; 92.95; 8; 100/100 | 30.07/56.82/87.56; 92.13; 8; 100/100 |
| delta.health | 123.29/167.23/183.42; 7.42; 52; 100/100 | 137.9/174.17/199.79; 26.15; 52; 100/100 | 184.23/238.93/288.63; 36.26; 52; 100/100 |
| delta.projects | 21.14/28.51/33.08; 35.42; 9; 100/100 | 26.46/41.6/61.23; 94.73; 9; 100/100 | 34.32/59.08/74.38; 93.02; 9; 100/100 |
| delta.quotations | 26.42/36.12/39.35; 29.04; 11; 100/100 | 36.54/55.91/80.42; 76.92; 11; 100/100 | 40.51/75.65/102.38; 83.8; 11; 100/100 |
| delta.registers | 25.97/41.8/53.07; 28.75; 11; 100/100 | 33.31/54/67.46; 80.21; 11; 100/100 | 37/58.19/87.29; 89.55; 11; 100/100 |
| Projects/atlas/create-draft-sustained | 35.27/46.19/52.74; 23.47; 15; 100/100 | 53.89/76.16/85.21; 59.79; 15; 100/100 | 96.7/255.57/392.77; 56.5; 15; 100/100 |

## Interpretation and limits

The final load's highest observed read p95/p99 is Atlas Accounting health at concurrency 1 (`861.68/930.72 ms`); the highest sustained-write p95/p99 is Projects draft creation at concurrency 8 (`255.57/392.77 ms`). The four retained plans show index-backed access for Inventory stock balance, Projects and Subledger exposure; Sales quotations use a sequential scan on the small seeded table. These are seeded-data observations, not production query certification.

The historical repeat is directionally consistent with the final load and retains aggregate values plus resource CSVs, but it predates the `LatencySamplesMs` field. Its lack of individual samples is an explicit archive limitation; the final load is the authoritative independently recomputable p99 run. Raw JSON, CSV and process-resource artifacts are in [`benchmark-archive`](benchmark-archive), and its manifest/checksums are audited in [P0-B09](P0-B09_ARTIFACT_VERIFICATION_AND_REPRODUCTION.md).
