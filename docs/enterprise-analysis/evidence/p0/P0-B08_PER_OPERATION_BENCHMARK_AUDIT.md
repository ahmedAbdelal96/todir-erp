# P0-B08 — Per-Operation Benchmark Audit

**Result:** `PASS — RAW ARTIFACTS RECONCILED`  
**Runs:** diagnostic `20261125090812_10848_4c9d0e78`; controlled load `20261125091019_41032_6b11ec83`; repeat `20261125091708_30864_85500d9a`  
**Source commit recorded by the harness:** `c85f59121dc06e8c4f2d5a25590a534c82e836af`  
**Evidence boundary:** the later P0-D DbMigrator advisory-lock correction is startup/migration coordination only and does not alter API request paths, SQL query plans, or benchmark workload code. The load measurements therefore remain valid for the measured request path; the fresh P0-D migration, seed, startup and security runs are recorded separately.

## Reconciliation

- Diagnostic: 42 read result rows × 5 measured requests = 210 measured requests; p95/p99 are labelled `INSUFFICIENT_SAMPLES` by the harness.
- Each controlled load run: 63 read rows × 100 + 3 sustained-write rows × 100 = 6,600 measured requests; `6,300` reads + `300` writes, zero errors.
- Each load run recorded `21/21` functional smoke, `3/3` security smoke and `8/8` write smoke checks.
- Every row below is `100/100` successful requests. Values in each cell are `p50/p95/p99 ms; RPS; SQL statements/request; successes/requests`.
- Percentiles use the harness nearest-rank method: `ceil(p*n)`, one-based.

## Controlled load per-operation values

| Operation | First c1 | First c4 | First c8 | Repeat c1 | Repeat c4 | Repeat c8 |
|---|---:|---:|---:|---:|---:|---:|
| Accounting/atlas/atlas.health | 120.64/141.88/155.63; 7.86; 52; 100/100 | 133.91/156.37/166.61; 27.68; 52; 100/100 | 172.08/218.26/253.23; 39.61; 52.04; 100/100 | 115.37/131.05/140.96; 8.22; 52; 100/100 | 132.57/164.39/166.47; 27.81; 52; 100/100 | 183.32/209.6/228.5; 37.85; 52.03; 100/100 |
| Accounting/delta/delta.health | 115.95/134.39/144.66; 8.15; 52; 100/100 | 136.08/154.16/159.55; 27.48; 52; 100/100 | 184.06/251.63/266.6; 35.92; 52; 100/100 | 114.04/134.17/148.76; 8.2; 52; 100/100 | 135.34/175.28/187.99; 26.89; 52; 100/100 | 177.86/219.46/242.18; 38.94; 52; 100/100 |
| Authorization/atlas/atlas.effective-access | 39.88/51.27/62.17; 20.69; 10; 100/100 | 54.39/83.16/94.91; 55.05; 10; 100/100 | 78.55/209.71/222.43; 51.63; 10.04; 100/100 | 40.19/49.76/55.01; 20.82; 10; 100/100 | 44.93/78.61/90.52; 63.21; 10.01; 100/100 | 56.53/169.63/181.86; 63.19; 10.03; 100/100 |
| Authorization/delta/delta.effective-access | 29.44/38.29/57.38; 26.66; 10; 100/100 | 37.55/54.08/57.85; 77.15; 10; 100/100 | 42.53/78.8/93.44; 84.44; 10; 100/100 | 29/36.42/42.41; 28.05; 10; 100/100 | 33.33/46.95/68.68; 83.21; 10; 100/100 | 43.5/68.86/97.32; 86.87; 10; 100/100 |
| HR/atlas/atlas.employees | 26.72/31.73/39.12; 30.22; 8; 100/100 | 35.05/48.5/66.89; 81.2; 8; 100/100 | 41.55/66.61/81.77; 88.82; 8; 100/100 | 27.24/38.12/42.54; 28.47; 8; 100/100 | 31.56/46.48/63.01; 88.43; 8; 100/100 | 38.84/67.26/109.47; 88.26; 8; 100/100 |
| HR/delta/delta.employees | 26.45/32.51/33.65; 30.67; 8; 100/100 | 33.33/47.29/62.1; 81.11; 8; 100/100 | 39.79/63.09/96.42; 87.91; 8; 100/100 | 25.51/32.2/37.41; 31.09; 8; 100/100 | 34.34/52.62/62.05; 81.43; 8; 100/100 | 40.03/78.12/114.28; 88.39; 8; 100/100 |
| Inventory/atlas/atlas.balances | 38.99/53.81/56.17; 21.7; 16; 100/100 | 47.56/60.77/83.41; 67.48; 16; 100/100 | 61.05/111.14/149.1; 72.12; 16; 100/100 | 38.79/46.73/51.75; 21.84; 16; 100/100 | 47.61/72.21/79.05; 65.49; 16; 100/100 | 60.45/91.98/114.27; 72.39; 16.01; 100/100 |
| Inventory/delta/delta.balances | 39.1/49.41/75.91; 21.51; 16; 100/100 | 47.13/81.21/89.51; 62.56; 16; 100/100 | 60.31/82.25/97.02; 77.26; 16; 100/100 | 39.54/51.2/54.92; 21.55; 16; 100/100 | 44.62/57.93/73.6; 68.56; 16; 100/100 | 57.96/79.9/97.81; 79.46; 16; 100/100 |
| Invoicing/atlas/atlas.customer-invoices | 27.79/31.94/35.33; 29.51; 9; 100/100 | 35.46/45.72/56.69; 81.3; 9; 100/100 | 42.26/81.91/116.54; 78.34; 9; 100/100 | 27.85/33.71/36.42; 29.2; 9; 100/100 | 35.4/48.04/75.86; 81.18; 9; 100/100 | 40.19/68.8/87.89; 90.58; 9; 100/100 |
| Invoicing/delta/delta.customer-invoices | 26.79/32.4/39.73; 29.52; 9; 100/100 | 35.48/49.6/60.99; 80.16; 9; 100/100 | 40.78/80.63/96.59; 88.66; 9; 100/100 | 27.76/37.26/42.49; 28.34; 9; 100/100 | 33.17/55.22/75.72; 80.09; 9; 100/100 | 41.5/66.18/82.55; 89.16; 9; 100/100 |
| Payments/atlas/atlas.accounts | 24.63/31.58/35.5; 31.16; 8; 100/100 | 33.98/50.37/73.94; 78.74; 8; 100/100 | 41.47/73.26/99; 83.43; 8; 100/100 | 22.94/27.46/29.67; 33.87; 8; 100/100 | 30.08/39.54/55.43; 92.51; 8; 100/100 | 34.82/68.79/91.81; 96.86; 8; 100/100 |
| Payments/delta/delta.accounts | 22.95/28.79/34.88; 33.75; 8; 100/100 | 28.49/38.31/44.13; 95.99; 8; 100/100 | 34.53/52.57/69.65; 96.44; 8; 100/100 | 22.76/28.64/33.89; 33.9; 8; 100/100 | 28.36/41.94/61.76; 93.76; 8; 100/100 | 34.58/62.3/77.17; 92.35; 8; 100/100 |
| Payroll/atlas/atlas.groups | 24.55/29.8/31.13; 32.53; 8; 100/100 | 30.61/42.33/59.89; 87.83; 8; 100/100 | 37.41/61.1/73.15; 96.31; 8; 100/100 | 23.7/28.76/37.35; 32.53; 8; 100/100 | 31.05/43.4/52.19; 90.11; 8; 100/100 | 36.1/66.81/98.14; 97.03; 8; 100/100 |
| Payroll/delta/delta.groups | 24.45/31.5/34.79; 31.91; 8; 100/100 | 31.33/46.16/63.37; 85.4; 8; 100/100 | 34.16/59.22/92.8; 90.46; 8; 100/100 | 25.4/35.13/40.16; 30.05; 8; 100/100 | 31.13/39.93/56.91; 91.34; 8; 100/100 | 36.6/69.44/91.9; 89.27; 8; 100/100 |
| POS/atlas/atlas.registers | 32.45/41.16/43.45; 25.1; 11; 100/100 | 37.98/65.09/80.27; 75.64; 11; 100/100 | 54.27/91.65/110.03; 71.88; 11; 100/100 | 34.07/41.04/45.41; 24.01; 11; 100/100 | 40.12/54.02/70.45; 74.21; 11; 100/100 | 49.91/71.74/81.01; 79.47; 11; 100/100 |
| POS/delta/delta.registers | 30.2/38.13/44.72; 27.03; 11; 100/100 | 38.74/52.8/64.95; 79.64; 11; 100/100 | 47.55/69.58/89.18; 84.29; 11; 100/100 | 30.06/37.48/41.99; 27.34; 11; 100/100 | 37.93/51.58/70.93; 78.73; 11; 100/100 | 43.55/68.02/111.6; 83.19; 11; 100/100 |
| Projects/atlas/atlas.projects | 26.12/32.58/34.68; 30.31; 9; 100/100 | 32.88/41.38/51.63; 87.85; 9; 100/100 | 40.95/76.16/102.9; 88.09; 9; 100/100 | 26.19/35.58/41.51; 30.05; 9; 100/100 | 31.4/46.76/58.36; 89.11; 9; 100/100 | 38.4/63.45/81.99; 87.5; 9; 100/100 |
| Projects/atlas/create-draft-sustained | 41.8/50.51/53.53; 20.81; 15; 100/100 | 61.74/87.06/98.86; 54.25; 15; 100/100 | 99.3/283.39/402.85; 52.61; 15; 100/100 | 40.77/50.81/55.33; 20.75; 15; 100/100 | 63.38/87.69/93.91; 52.75; 15; 100/100 | 106.7/263.96/364.91; 53.19; 15; 100/100 |
| Projects/delta/delta.projects | 25.78/32.92/37.12; 30.25; 9; 100/100 | 31.92/38.58/46.77; 91.68; 9; 100/100 | 38.6/65.08/95.58; 93; 9; 100/100 | 25.83/32.28/34.74; 30.96; 9; 100/100 | 30.29/46.89/63.02; 91.47; 9; 100/100 | 38.63/56.32/93.87; 91.17; 9; 100/100 |
| Sales/atlas/atlas.quotations | 30.76/37.38/42.72; 26.7; 11; 100/100 | 37.45/51.43/69.57; 78.24; 11; 100/100 | 49.59/67.83/77.98; 78.36; 11; 100/100 | 31.28/36.5/38.31; 26.89; 11; 100/100 | 42.24/63.93/86.35; 69.39; 11; 100/100 | 47.56/67.44/89.73; 81.19; 11; 100/100 |
| Sales/delta/delta.quotations | 30.98/35.96/43.78; 26.73; 11; 100/100 | 39.37/50.56/61.57; 76.35; 11; 100/100 | 51.37/82.23/113.1; 78.16; 11; 100/100 | 30.2/38.21/39.68; 27.22; 11; 100/100 | 37.1/55.11/75.65; 76.62; 11; 100/100 | 46.08/74.75/106.9; 83.94; 11; 100/100 |
| Subledger/atlas/atlas.party-exposure | 58.26/70.65/75.47; 15.52; 26; 100/100 | 71.1/82.36/87.24; 48.76; 26; 100/100 | 84.53/119.13/157.23; 67.57; 26; 100/100 | 58.33/67.41/72.45; 15.74; 26; 100/100 | 68.17/87.8/93.32; 49.5; 26; 100/100 | 87.49/123.58/136.74; 64.08; 26.01; 100/100 |

## Interpretation and limits

The controlled repeat is directionally consistent with the first run. The highest observed controlled-load p95/p99 belongs to the sustained Projects draft write at concurrency 8 (`283.39/402.85 ms` first run; `263.96/364.91 ms` repeat), while Accounting health is the heaviest read (`52` SQL statements/request). The four retained plans show index-backed access for Inventory stock balance, Projects and Subledger exposure; Sales quotations use a sequential scan on the small seeded table (161 scanned rows in the plan). These are observations for the seeded dataset, not production query certification.

Raw JSON, CSV and API logs remain in the ignored local `artifacts/performance` tree and are enumerated by [P0-B09](P0-B09_ARTIFACT_VERIFICATION_AND_REPRODUCTION.md). No raw artifact is promoted as a production SLA or capacity certification.
