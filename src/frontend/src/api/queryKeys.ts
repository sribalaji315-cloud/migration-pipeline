export const queryKeys = {
  overview: {
    all: ['overview'] as const,
  },
  runs: {
    all: ['runs'] as const,
    list: (limit: number, offset: number) => ['runs', 'list', limit, offset] as const,
    detail: (runId: string) => ['runs', 'detail', runId] as const,
  },
  bronzeVersions: {
    all: ['bronzeVersions'] as const,
  },
  reference: {
    all: ['reference'] as const,
    versions: () => ['reference', 'versions'] as const,
  },
  items: {
    all: ['items'] as const,
    type: (itemId: string) => ['items', 'type', itemId] as const,
    class: (itemId: string) => ['items', 'class', itemId] as const,
    missingValues: (itemId: string, attributeId: string) => ['items', 'missingValues', itemId, attributeId] as const,
  },
  boms: {
    all: ['boms'] as const,
    lines: (itemId: string) => ['boms', 'lines', itemId] as const,
    list: (q: string, topOnly: boolean, limit: number, offset: number) => ['boms', 'list', q, topOnly, limit, offset] as const,
    scopePreview: (topBoms: string[]) => ['boms', 'scopePreview', ...topBoms] as const,
    readiness: (q: string, status: string, limit: number, offset: number) => ['boms', 'readiness', q, status, limit, offset] as const,
    tree: (topItemId: string) => ['boms', 'tree', topItemId] as const,
  },
  issueGroups: {
    all: ['issueGroups'] as const,
    list: (filters: object, limit: number, offset: number) => ['issueGroups', 'list', filters, limit, offset] as const,
    summary: () => ['issueGroups', 'summary'] as const,
    detail: (id: string) => ['issueGroups', 'detail', id] as const,
    items: (id: string, limit: number, offset: number) => ['issueGroups', 'items', id, limit, offset] as const,
  },
  typeChanges: {
    all: ['typeChanges'] as const,
    list: (q: string, decision: string | null, limit: number, offset: number) => ['typeChanges', 'list', q, decision, limit, offset] as const,
  },
  waves: {
    all: ['waves'] as const,
    list: (limit: number, offset: number) => ['waves', 'list', limit, offset] as const,
    detail: (waveId: string) => ['waves', 'detail', waveId] as const,
    gate: (waveId: string) => ['waves', 'gate', waveId] as const,
    files: (waveId: string) => ['waves', 'files', waveId] as const,
  },
  swing: {
    all: ['swing'] as const,
    source: () => ['swing', 'source'] as const,
  },
  migrationTool: {
    all: ['migrationTool'] as const,
    source: () => ['migrationTool', 'source'] as const,
  },
}
