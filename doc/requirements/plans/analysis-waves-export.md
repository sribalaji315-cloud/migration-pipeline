# Increment 2 – Analysis (steps 3–12), waves, gate, Gold/Excel export

Status: implemented. Builds on [foundation-bronze-swing.md](./foundation-bronze-swing.md).

## Scope

- Silver steps 3–12 (`Sql/03_reference.sql` … `Sql/12_readiness.sql`, `Steps/AnalysisSteps.cs`), run after `NormalizeSwingStep`.
  Without an uploaded Bluestar reference they are recorded as `Skipped` and only Swing normalization runs.
- SQLite state carried across runs (`Services/AnalysisStateService.cs`): `TypeDecision` sync after step 8, `IssueGroup` upsert after step 12.
- Waves, gate (`Core/Rules/WaveGate.cs`), WaveExport runs (`WaveGoldStep` W1 + `WaveExportStep` W2, `Sql/w1_gold.sql`).
- APIs (§10) and pages: Overview stats, BOM waves, wave detail, item class, issues, type changes.

## Decisions taken (with the business, 2026-11-19)

| Topic | Decision | Setting |
|---|---|---|
| List vs. free variable | List when the class variable **or** the global variable says `VALUE_LIST` and the list has values | `Pipeline:ListRule = Either` (`ClassVariable` / `GlobalVariable` possible) |
| REVIEW attributes | Block the BOM and the gate | `Pipeline:ReviewBlocksExport = true` |
| Missing `legacyReachable` | Counts as reachable | `Pipeline:LegacyReachableNullIncluded = true` |
| Export layout | §8.3 spec sheets, one workbook each, + `ISSUES_<wave>.xlsx` + `manifest.json` | – |

Bluestar facts found in the real exports: class variable `VALUELISTID` holds the **global variable id**; the global variable file
gives the real `VALUELISTID`, `VARIABLEDATATYPE` and `VARIABLEVALUES`; value list values are left-padded (compared trimmed).

## Rules implemented

- **Scope** = items with a Migration Tool item header ∪ items with mappings (`mt_scope_item`).
- **R1–R6** mapping exclusions → `map_rules_excluded` (`NOT_ENGINEERING`, `NOT_REQUIRED`, `MAPPING_MISSING`, `IGNORED`); predicted or
  `feasibility = Review` rows are `PENDING_REVIEW` (not used). `feasibility = No` is **not** excluded (open question).
- **R11–R13** coverage + ranking: full-coverage list → full-coverage free → highest coverage; ties by class usage, then name.
  Free types: Integer = `TRY_CAST BIGINT`, Real = `DOUBLE`, String always valid.
- **R14 inheritance**: a child's condition needs come from `#FEATURE` tokens in its legacy conditions (mapped through clean
  feature → attribute mappings) and target-condition identifiers that are Bluestar variables. Only needs the item does not own
  are pushed to its direct parents, level by level in C# (fails visibly above `MaxBomDepth`). Owners resolved downward;
  unresolved needs at a top item → `top_missing` (`INHERITED_ATTRIBUTE_MISSING_ON_TOP_ITEM`).
- **R16**: Swing FIXED + any inherited attribute → CONFIGURABLE (`FIXED_TO_CONFIGURABLE`, Approver decision). A decision only
  counts while inherited attributes and Swing code are unchanged; otherwise the next analysis resets it to Pending.
- **Readiness**: item BLOCKED (block issue / rejected decision / review issue when review blocks) → REVIEW → DECISION → READY;
  BOM adds not-imported descendants (BLOCKED) and EXPORTED (all items already exported).
- **Issue groups**: new → Open; missing in a later run → Resolved; back again → Open; Acknowledged (Warn/Info only) stays.

## Gate (all-or-nothing, re-checked inside the export run)

`NoBlockingOrReviewIssues`, `ItemsImportedFromMigrationTool`, `TypeChangesApproved` (SQLite decisions vs. current Silver),
`ItemTypesKnown`, `ReferencesExistInBluestar`, `SourceUnchanged` (wave pins = latest analysis, and no newer snapshot/reference;
`Refresh wave` re-pins). On success `ExportedItem` rows (re-exports flagged), wave status and run are committed in one transaction.

## Tests

- `Pipeline.Tests/AnalysisScenarioTests.cs` on `tests/fixtures/scenario` (exclusions, GV ranking, inheritance, types, issues, readiness, decisions).
- `Api.Tests/AnalysisApiTests.cs`: reference upload → sync → class model / readiness / tree / issues / type changes → wave gate → approve → export → files → re-analysis (EXPORTED).
- `Core.Tests/WaveGateTests.cs`.

## Open

- Real Bluestar import template layout (sheets follow §8.3 for now).
- Migration Tool: source of `legacyReachable` / `simplifiedLegacyCondition` / `targetCondition`; whether `feasibility = No` excludes a row.
- Live `updatedAt` check against the Migration Tool in `SourceUnchanged`.
- References uploaded with the old column layout produce empty global variable data: re-upload the five files.
