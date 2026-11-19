# Increment 1 – Foundation, Bronze capture, Swing normalization

Status: implemented (see "Delivered"). Architecture reference: [MD_PIPELINE_ARCHITECTURE_GSST00_GSAR00.md](../../../MD_PIPELINE_ARCHITECTURE_GSST00_GSAR00.md).

## Scope

1. Foundation – solution scaffold, Core enums/rules, EF Core SQLite metadata + Identity/JWT, run queue, health, frontend shell.
2. Bronze capture – Swing GSST00/GSAR00 (ODBC or CSV fixtures), Migration Tool mappings + item headers (REST or JSON fixtures), Bluestar reference upload → versioned Parquet.
3. Swing normalization – pipeline step 2c (`NormalizeSwingStep`, `Sql/02_swing.sql`).

Out of scope for this increment: steps 3–12, waves, gate, Gold/Excel export, issue groups, type decisions, scheduled analysis (`Pipeline:AnalysisCron`), optional Migration Tool BOM reconciliation.

## Design decisions taken

| Topic | Decision |
|---|---|
| DuckDB | `DuckDB.NET.Data.Full` 1.5.6.1 (bundles native DuckDB). Approved. In-process; one DuckDB file per analysis run (`data/runs/<run_id>/silver.duckdb`). |
| Excel | `ClosedXML` 0.105.1 for Bluestar reading (and later Gold writing). Approved. |
| HTTP resilience | `Microsoft.Extensions.Http.Resilience` standard handler (Polly) + `ReadOnlyGuardHandler` (verb allowlist, `MaxConcurrency`). |
| Run model | `POST /api/sync` queues an **Analysis** run with `SyncRequested=true` (states Queued → Syncing → Running → Completed). `POST /api/runs/analysis` reuses the latest published Bronze. `RunType` stays `{Analysis, WaveExport}`. |
| Bronze publishing | Steps write to `data/bronze/v{n}.tmp`; the folder is renamed to `v{n}` and `BronzeVersion.Status=Published` only after **all** extracts succeed. Failed syncs leave a `Failed` version row and no folder. |
| Sync order | GSST00 → GSAR00 → Migration Tool, because the per-item mapping fallback and item-header lookups are scoped to GSST00 items. |
| Content hashes | Order-independent, duplicate-sensitive: row count + two 64-bit lane sums of per-row SHA-256 (`_row_hash`). Stored as `SwingBomHash` / `SwingItemHash`. |
| Mapping partitions | Hive-style `mappings/item_bucket=XX/*.parquet` (FNV-1a bucket, `Pipeline:MappingBuckets`, default 64). |
| Config into SQL | Interpretation rules (type codes, pseudo map, open-ended date patterns, two-digit-year pivot, date-out inclusivity, BOM-type policy, effective date) are appended to `cfg_*` tables; SQL never interpolates data values. Only Parquet paths are substituted as escaped literals (`{{token}}`). |
| Infrastructure SQL in C# | Bronze DDL/COPY is generated from source contracts in `BronzeParquetWriter`; step logic stays in `Pipeline/Sql/*.sql`. |
| Item ids | Trim only; leading zeros **and case** preserved (§18.5 supersedes the "upper-case" note in §6). |
| Bluestar columns | Bronze keeps the actual export headers (e.g. `CLASSDESCRIPTION`, `NUMSEQUENCE`; global variables use `VARIABLETYPE` as name and `VARIABLEDATATYPE` as value type). Required per file: class list `CLASSNAME, EXTENSCLASSNAME, ENABLEDFORCLASSIFICATION`; class variables `CLASSNAME, VARIABLENAME, VALUELISTID, VARIABLEVALUES, VALUETYPE, MANDATORY, OBSOLETE`; global variables `VARIABLETYPE, VALUELISTID, VARIABLEDATATYPE, VARIABLEVALUES`; value lists `VALUELISTID, VALUETYPE`; values `VALUELISTID, VALUE`. |
| User FKs | User ids are stored without FKs to `AspNetUsers` so system-initiated runs can use the `system` actor. |
| Restart | Non-terminal runs are failed at startup (the queue is in-memory); `Writing` Bronze versions become `Failed`. |
| Swing-only import | Admin "Import Swing data now" on the Swing BOM page (`POST /api/swing/import`) queues an Analysis run with `SyncScope=SwingOnly`: GSST00 + GSAR00 are extracted in full, `CarryForwardMigrationToolStep` copies the previous snapshot's `mappings/` and `items.parquet` unchanged (rows keep their original `_bronze_version`), and `BronzeVersion.MtCarriedFromVersion` records the origin. Only one source import may be queued/running at a time (409). |
| Migration Tool-only import | Admin "Import changes" / "Full re-import" on the Migration Tool page (`POST /api/migration-tool/import[?full=true]`) queues an Analysis run with `SyncScope=MigrationToolOnly`: `CarryForwardSwingStep` copies the previous snapshot's `swing_bom_raw`/`swing_item_raw` (Swing extract metadata and hashes carried over, `BronzeVersion.SwingCarriedFromVersion` set), then `SyncMigrationToolStep` runs incrementally (falls back to full when no earlier mappings exist). Requires a previous snapshot with Swing data (400 otherwise). |
| BOM-scoped Migration Tool import | Swing BOM page (Admin): pick BOMs from `bom_parent` (new Silver table: every GSST00 parent with description, counts, `is_top`), preview the scope, import. Scope = selected items + all descendants over **all** GSST00 lines (recursive `UNION`, cycle-safe; superset of the effective structure so nothing needed is missed). `SyncMigrationToolStep` fetches mappings per scoped item and headers in batches, then rebuilds Bronze as previous rows for items outside the scope + fresh rows for scoped items (scoped items without mappings now lose stale rows). Progress (items fetched) is saved to the step record every 3 s. Snapshots from older analyses without `bom_parent` need a new analysis before BOMs can be listed. |

## Swing normalization rules implemented (R17, §18)

- `swing_item`: distinct trimmed GSAR00 rows; `type_code` = `TRY_CAST(trim(ARVAKZ))`, `source_type` via `Swing:TypeCodes` (default `0=FIXED, 2=CONFIGURABLE`), otherwise `UNKNOWN`.
- `swing_item_type`: one row per item; conflicting ARVAKZ values or differing rows for the same model → `is_ambiguous`, type `UNKNOWN`, no row chosen.
- `bom_line`: every GSST00 occurrence kept (exact duplicates get distinct deterministic ids `BL-<sha256(row_hash:dup_seq)>`). Dates built from raw components; only configured open-ended patterns become `NULL`; invalid/partial/unknown → unresolved. Two-digit years unresolved unless `Swing:TwoDigitYearPivot` is set.
- Effectivity: `(date_in IS NULL OR date_in <= eff) AND (date_out IS NULL OR date_out >= eff)` (`Swing:DateOutInclusive`, default `true`). Unresolved dates → `is_effective = NULL`, line **kept** in `effective_bom_line` with `effectivity_resolved = false`.
- BOM-type policy (`AllTypesUnfiltered` default, or `IncludeOnly`) filters traversal scope only; `bom_line.included_by_policy` keeps provenance.
- `bom_edge` (distinct parent/child) + `bom_edge_line` bridge to source lines; `bom_line_key_profile` profiles `(STANR, STBKAR, STBKPO)` without enforcing it.
- Cycle detection: iterative root/leaf peel loop (C# loop, SQL per iteration); non-convergence fails the run.
- `swing_source_issue` (input for the future IssuesStep):

| Issue | Severity | Fix source |
|---|---|---|
| BOM_LINE_KEY_MISSING | Block | SwingBom |
| BOM_DATE_UNRESOLVED | Block | SwingBom |
| PSEUDO_FLAG_UNKNOWN | Block | SwingBom |
| ITEM_TYPE_UNKNOWN (missing GSAR00 or unsupported ARVAKZ, effective BOM items only) | Block | SwingItemMaster |
| ITEM_MODEL_AMBIGUOUS | Block | SwingItemMaster |
| BOM_CYCLE | Block | SwingBom |
| BOM_CHILD_MODEL_NOT_FOUND | Review | SwingBom |
| BOM_QUANTITY_UNPARSEABLE | Warn | SwingBom |
| BOM_LINE_KEY_NOT_UNIQUE | Info | SwingBom |

## Assumptions to confirm (Migration Tool team / Swing owners)

- **Verified against the live Migration Tool (hwds-dev, 2026-11-19):** bearer-token auth works; `GET /workspace-mappings/{itemId}` returns a bare array (ASYCB3S0 = 2,470 rows) with `itemId`, `inherited` as `"Yes"/"No"`, `updatedAt`/`signedOnAt` as Unix seconds, nested `prediction {status, confidence: "High"|"Medium", matchType, evidence, prev*, predictedAt}`, plus `formula`, `signedOnBy*`, `allGlobalTargets`, `candidateAttributeIds`. `POST /bom/items/by-ids` takes a bare JSON array of ids and returns `itemId, description, category, priority, approvedForMigration, features[]` (no `productType`). Parsing is lenient (`MtPayload`) and every row keeps `raw_json`.
- **Not present in the live API (affects R4/R5 later):** `legacyReachable`, `simplifiedLegacyCondition`, `targetCondition` and a numeric prediction confidence. Confirm with the Migration Tool team where these come from before implementing step 4.
- Bulk endpoint `GET /export/workspace-mappings` is not confirmed; with `UseBulkEndpoint=false` mappings are fetched per GSST00 item (no incremental sync in that mode).
- Login `POST /auth/login {username,password}` → `{accessToken, refreshToken}` is still an assumption (bearer token is the verified path).
- `STFIST` encoding (`Y`/`N` configured; blank or other → issue), date sentinels (none configured), date-out inclusivity, BOM-type meanings, ODBC library (`Swing:AllowedLibraries` must list it).

## Delivered

- Backend: `src/backend` (Api, Core, Data, Pipeline + 3 test projects), initial migration `InitialMetadataSchema`.
- Endpoints: `POST /api/auth/login`, `GET /api/health`, `GET /api/overview`, `POST /api/sync[?full=true]`, `GET /api/sync/versions`, `POST /api/runs/analysis`, `GET /api/runs`, `GET /api/runs/{runId}`, `GET /api/items/{itemId}/type`, `GET /api/boms/{itemId}/lines`, `POST /api/reference/bluestar`, `GET /api/reference/bluestar/versions`.
- Frontend: login, Overview (run launcher, Swing issue summary), Swing BOM lookup, Runs (step timeline, Bronze snapshots), Bluestar reference (upload, versions), help drawers, i18n.
- Fixtures: `src/backend/tests/fixtures/swing/GSST00.csv`, `GSAR00.csv`, `mt/mappings.json`, `mt/items.json`.

## Next increments

Implemented in increment 2 – see `analysis-waves-export.md`.
