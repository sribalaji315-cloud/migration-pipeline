import { apiRequest } from './http'
import type { BronzeVersion, Overview, PagedResult, Run, RunRequest } from '../types/pipeline'

export function getOverview() {
  return apiRequest<Overview>('/api/overview')
}

export function getRuns(limit: number, offset: number) {
  return apiRequest<PagedResult<Run>>(`/api/runs?limit=${limit}&offset=${offset}`)
}

export function getRun(runId: string) {
  return apiRequest<Run>(`/api/runs/${encodeURIComponent(runId)}`)
}

export function getBronzeVersions() {
  return apiRequest<BronzeVersion[]>('/api/sync/versions')
}

export function queueSync(full: boolean, request: RunRequest) {
  return apiRequest<Run>(`/api/sync${full ? '?full=true' : ''}`, { method: 'POST', body: JSON.stringify(request) })
}

export function queueAnalysis(request: RunRequest) {
  return apiRequest<Run>('/api/runs/analysis', { method: 'POST', body: JSON.stringify(request) })
}
