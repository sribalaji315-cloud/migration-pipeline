import { apiRequest } from './http'
import type { BomLine, BomScopePreview, BomSummary, ItemType, PagedResult } from '../types/pipeline'

export function getItemType(itemId: string) {
  return apiRequest<ItemType>(`/api/items/${encodeURIComponent(itemId)}/type`)
}

export function getBomLines(itemId: string) {
  return apiRequest<BomLine[]>(`/api/boms/${encodeURIComponent(itemId)}/lines`)
}

export function getBoms(q: string, topOnly: boolean, limit: number, offset: number) {
  const params = new URLSearchParams({ q, topOnly: String(topOnly), limit: String(limit), offset: String(offset) })
  return apiRequest<PagedResult<BomSummary>>(`/api/boms?${params}`)
}

export function previewBomScope(topBoms: string[]) {
  return apiRequest<BomScopePreview>('/api/boms/scope-preview', { method: 'POST', body: JSON.stringify({ topBoms }) })
}
