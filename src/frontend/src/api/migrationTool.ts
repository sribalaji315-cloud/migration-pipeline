import { apiRequest } from './http'
import type { HealthCheck, MigrationToolSource, Run, RunRequest } from '../types/pipeline'

export function getMigrationToolSource() {
  return apiRequest<MigrationToolSource>('/api/migration-tool/source')
}

export function testMigrationToolConnection() {
  return apiRequest<HealthCheck>('/api/migration-tool/test-connection', { method: 'POST' })
}

export function importMigrationTool(full: boolean, request: RunRequest, topBoms?: string[]) {
  return apiRequest<Run>(`/api/migration-tool/import${full ? '?full=true' : ''}`, {
    method: 'POST',
    body: JSON.stringify({ ...request, topBoms: topBoms ?? null }),
  })
}
