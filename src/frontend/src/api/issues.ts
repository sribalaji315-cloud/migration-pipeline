import { apiRequest } from './http'
import type { IssueFilters, IssueGroup, IssueItem, IssueSummary, PagedResult } from '../types/pipeline'

export function getIssueGroups(filters: IssueFilters, limit: number, offset: number) {
  const params = new URLSearchParams({ limit: String(limit), offset: String(offset) })
  if (filters.severity) params.set('severity', filters.severity)
  if (filters.fixSource) params.set('fixSource', filters.fixSource)
  if (filters.status) params.set('status', filters.status)
  if (filters.q) params.set('q', filters.q)
  if (filters.bom) params.set('bom', filters.bom)
  return apiRequest<PagedResult<IssueGroup>>(`/api/issues/groups?${params}`)
}

export function getIssueSummary() {
  return apiRequest<IssueSummary[]>('/api/issues/summary')
}

export function getIssueGroup(id: string) {
  return apiRequest<IssueGroup>(`/api/issues/groups/${encodeURIComponent(id)}`)
}

export function getIssueItems(id: string, limit: number, offset: number) {
  return apiRequest<PagedResult<IssueItem>>(`/api/issues/groups/${encodeURIComponent(id)}/items?limit=${limit}&offset=${offset}`)
}

export function acknowledgeIssueGroup(id: string, note: string | null) {
  return apiRequest<IssueGroup>(`/api/issues/groups/${encodeURIComponent(id)}/acknowledge`, { method: 'POST', body: JSON.stringify({ note }) })
}
