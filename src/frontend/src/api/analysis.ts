import { apiRequest } from './http'
import type {
  BomReadiness, BomTree, ItemClass, MissingValues, PagedResult, TypeChange, TypeDecisionResult, TypeDecisionStatus,
} from '../types/pipeline'

export function getItemClass(itemId: string) {
  return apiRequest<ItemClass>(`/api/items/${encodeURIComponent(itemId)}/class`)
}

export function getMissingValues(itemId: string, attributeId: string) {
  return apiRequest<MissingValues>(`/api/items/${encodeURIComponent(itemId)}/class/${encodeURIComponent(attributeId)}/missing-values`)
}

export function getBomReadiness(q: string, status: string, limit: number, offset: number) {
  const params = new URLSearchParams({ q, status, limit: String(limit), offset: String(offset) })
  return apiRequest<PagedResult<BomReadiness>>(`/api/boms/readiness?${params}`)
}

export function getBomTree(topItemId: string) {
  return apiRequest<BomTree>(`/api/boms/${encodeURIComponent(topItemId)}/tree`)
}

export function getTypeChanges(q: string, decision: TypeDecisionStatus | null, limit: number, offset: number) {
  const params = new URLSearchParams({ q, limit: String(limit), offset: String(offset) })
  if (decision) {
    params.set('decision', decision)
  }

  return apiRequest<PagedResult<TypeChange>>(`/api/type-changes?${params}`)
}

export function decideTypeChanges(itemIds: string[], decision: TypeDecisionStatus, note: string | null) {
  return apiRequest<TypeDecisionResult>('/api/type-changes/decide', { method: 'POST', body: JSON.stringify({ itemIds, decision, note }) })
}
