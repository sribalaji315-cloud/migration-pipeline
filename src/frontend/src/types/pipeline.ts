export type Role = 'Viewer' | 'Analyst' | 'Approver' | 'Admin'

export type RunType = 'Analysis' | 'WaveExport'
export type RunStatus = 'Queued' | 'Syncing' | 'Running' | 'Completed' | 'Failed'
export type StepStatus = 'Running' | 'Completed' | 'Failed' | 'Skipped'
export type SyncMode = 'Full' | 'Incremental'
export type SyncScope = 'All' | 'SwingOnly' | 'MigrationToolOnly'
export type BronzeVersionStatus = 'Writing' | 'Published' | 'Failed'
export type BomTypePolicyMode = 'AllTypesUnfiltered' | 'IncludeOnly'

export type AuthLoginResponse = {
  token: string
  email: string
  displayName: string
  roles: string[]
}

export type PagedResult<T> = {
  items: T[]
  total: number
  limit: number
  offset: number
}

export type BomTypePolicy = {
  mode: BomTypePolicyMode
  types: string[] | null
}

export type RunRequest = {
  effectiveDate: string | null
  bomTypePolicy: BomTypePolicy | null
}

export type RunStep = {
  sequence: number
  step: string
  outputTables: string
  status: StepStatus
  rowsOut: number
  durationSeconds: number
  errorMessage: string | null
}

export type Run = {
  runId: string
  runType: RunType
  status: RunStatus
  syncRequested: boolean
  syncMode: SyncMode | null
  syncScope: SyncScope
  scopeTopBoms: string[] | null
  bronzeVersion: number | null
  referenceVersion: number | null
  effectiveDate: string
  bomTypePolicy: BomTypePolicy
  startedByUserId: string
  queuedAt: string
  startedAt: string | null
  finishedAt: string | null
  errorMessage: string | null
  steps: RunStep[]
  waveId: string | null
  sourceAnalysisRunId: string | null
}

export type BronzeVersion = {
  version: number
  status: BronzeVersionStatus
  mode: SyncMode
  syncedAt: string
  maxUpdatedAt: string | null
  changedItems: number
  mappingRows: number
  mtItems: number
  mtCarriedFromVersion: number | null
  swingCarriedFromVersion: number | null
  mtScopeTopBoms: string[] | null
  mtScopeItems: number | null
  swingItems: number
  swingExtractedAt: string | null
  swingItemHash: string | null
  swingBomRows: number
  swingBomExtractedAt: string | null
  swingBomHash: string | null
  createdByRunId: string | null
}

export type ReferenceFile = {
  kind: string
  fileName: string
  bronzeTable: string
  rows: number
  sha256: string
}

export type ReferenceVersion = {
  version: number
  uploadedAt: string
  uploadedByUserId: string
  files: ReferenceFile[]
}

export type SourceIssueSummary = {
  issueType: string
  severity: string
  fixSource: string
  count: number
}

export type Overview = {
  latestBronze: BronzeVersion | null
  latestReference: ReferenceVersion | null
  latestRun: Run | null
  latestCompletedAnalysis: Run | null
  activeRuns: number
  swingIssues: SourceIssueSummary[]
  analysis: AnalysisSummary | null
}

export type ItemType = {
  itemId: string
  sourceTable: string
  sourceColumn: string
  rawCode: string | null
  typeCode: number | null
  sourceType: string
  isAmbiguous: boolean
  modelCount: number
  runId: string
  resolvedType: string | null
  typeChanged: boolean
  inheritedAttrs: string | null
  decision: TypeDecisionStatus | null
  decisionMatches: boolean
}

export type BomLine = {
  bomLineId: string
  parentItemId: string
  childItemId: string | null
  bomType: string | null
  bomPosition: string | null
  childModelNo: string | null
  quantity: string | null
  quantityRaw: string | null
  unit: string | null
  isPseudoLine: boolean | null
  pseudoRaw: string | null
  dateIn: string | null
  dateOut: string | null
  isEffective: boolean | null
  includedByPolicy: boolean
  bomConditionId: string | null
  sourceTable: string
}

export type BomSummary = {
  itemId: string
  description: string | null
  lineCount: number
  childCount: number
  isTop: boolean
}

export type BomScopePreview = {
  selected: number
  items: number
  unknown: string[]
  runId: string
}

export type HealthCheck = {
  name: string
  healthy: boolean
  detail: string
}

export type SourceStatus = {
  latestSnapshot: BronzeVersion | null
  activeImport: Run | null
  lastImport: Run | null
}

export type SwingSource = SourceStatus & {
  mode: 'Odbc' | 'Fixture'
  dataSource: string
  library: string
}

export type MigrationToolSource = SourceStatus & {
  mode: 'Rest' | 'Fixture'
  baseUrl: string
  authMode: 'BearerToken' | 'Login' | 'None'
  user: string
  tokenExpiresAt: string | null
  bulkEndpoint: boolean
}

export const bluestarFileKinds = ['ClassList', 'ClassVariable', 'GlobalVariable', 'ValueList', 'ValueListValue'] as const
export type BluestarFileKind = (typeof bluestarFileKinds)[number]

export type Severity = 'Block' | 'Review' | 'Approve' | 'Warn' | 'Info'
export const fixSources = [
  'MtWorkspaceMapping', 'MtMigrationApproval', 'BluestarClassVariable', 'BluestarValueList',
  'SwingItemMaster', 'SwingBom', 'MdPipelineDecision', 'Rerun',
] as const
export type FixSource = (typeof fixSources)[number]
export type IssueStatus = 'Open' | 'Resolved' | 'Acknowledged'
export type TypeDecisionStatus = 'Pending' | 'Approved' | 'Rejected'
export type WaveStatus = 'Draft' | 'Blocked' | 'Ready' | 'Exported'
export type AttributeStatus = 'Go' | 'Highlight' | 'Review'
export const bomReadinessStatuses = ['READY', 'DECISION', 'REVIEW', 'BLOCKED', 'EXPORTED'] as const
export type BomReadinessStatus = (typeof bomReadinessStatuses)[number]

export type ItemClassAttribute = {
  attributeId: string
  valueSource: 'Local' | 'Inherited'
  attributeSource: 'Bluestar' | 'MappingApi'
  globalVariable: string | null
  valueListId: string | null
  valueListSize: number | null
  valueType: string | null
  nUsed: number
  covered: number | null
  status: AttributeStatus
  inheritedFrom: string | null
  note: string | null
}

export type ItemClass = {
  itemId: string
  classId: string
  parentClassId: string
  description: string | null
  itemStatus: AttributeStatus
  readiness: string | null
  resolvedType: string | null
  typeChanged: boolean
  counts: { go: number; highlight: number; review: number }
  attributes: ItemClassAttribute[]
  runId: string
}

export type MissingValues = {
  itemId: string
  attributeId: string
  globalVariable: string | null
  valueListId: string | null
  valueType: string | null
  isList: boolean
  values: string[]
  runId: string
}

export type IssueGroup = {
  id: string
  groupKey: string
  issueType: string
  severity: Severity
  fixSource: FixSource
  fixTarget: string
  fixAction: string
  affectedItems: number
  affectedBoms: number
  firstSeenRunId: string
  lastSeenRunId: string
  resolvedInRunId: string | null
  status: IssueStatus
  acknowledgedByUserId: string | null
  acknowledgedAt: string | null
  note: string | null
}

export type IssueItem = {
  itemId: string | null
  parentItemId: string | null
  legacyFeatureId: string | null
  legacyValue: string | null
  attributeId: string | null
  valueCode: string | null
  globalVariable: string | null
  valueListId: string | null
  detail: string | null
}

export type IssueSummary = { fixSource: FixSource; severity: Severity; groups: number; affectedItems: number }

export type IssueFilters = {
  severity: Severity | null
  fixSource: FixSource | null
  status: IssueStatus | null
  q: string
  bom: string
}

export type TypeChange = {
  itemId: string
  description: string | null
  swingTypeCode: string | null
  sourceType: string
  resolvedType: string
  inheritedAttrs: string
  affectedBoms: number
  decision: TypeDecisionStatus
  decidedByUserId: string | null
  decidedAt: string | null
  note: string | null
}

export type TypeDecisionResult = { updated: number; notFound: string[] }

export type BomReadiness = {
  topItemId: string
  description: string | null
  items: number
  blockedItems: number
  reviewItems: number
  decisionItems: number
  alreadyExportedItems: number
  notImportedItems: number
  status: BomReadinessStatus
}

export type BomTreeNode = { itemId: string; description: string | null; status: string; resolvedType: string | null; typeChanged: boolean }

export type BomTree = {
  topItemId: string
  totalItems: number
  truncated: boolean
  nodes: BomTreeNode[]
  edges: Array<{ parentItemId: string; childItemId: string }>
  runId: string
}

export type Wave = {
  waveId: string
  name: string
  topBoms: string[]
  effectiveDate: string
  items: number
  status: WaveStatus
  bronzeVersion: number
  referenceVersion: number
  createdByUserId: string
  createdAt: string
  lastExportRunId: string | null
}

export type WaveGateCheck = { name: string; passed: boolean; count: number }

export type WaveGate = {
  waveId: string
  items: number
  alreadyExportedReferenced: number
  checks: WaveGateCheck[]
  canExport: boolean
  analysisRunId: string
  latestBronzeVersion: number | null
  latestReferenceVersion: number | null
}

export type WaveFile = { name: string; size: number; runId: string }

export type AnalysisSummary = {
  runId: string
  items: number
  boms: { ready: number; decision: number; review: number; blocked: number; exported: number }
  openIssueGroups: number
  pendingTypeDecisions: number
  topBlockingGroups: IssueGroup[]
}
