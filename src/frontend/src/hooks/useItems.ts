import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { queryKeys } from '../api/queryKeys'
import { getBomLines, getBoms, getItemType, previewBomScope } from '../api/items'

export function useItemType(itemId: string) {
  return useQuery({ queryKey: queryKeys.items.type(itemId), queryFn: () => getItemType(itemId), enabled: Boolean(itemId), retry: false })
}

export function useBomLines(itemId: string) {
  return useQuery({ queryKey: queryKeys.boms.lines(itemId), queryFn: () => getBomLines(itemId), enabled: Boolean(itemId), retry: false })
}

export function useBomList(q: string, topOnly: boolean, limit: number, offset: number, enabled: boolean) {
  return useQuery({
    queryKey: queryKeys.boms.list(q, topOnly, limit, offset),
    queryFn: () => getBoms(q, topOnly, limit, offset),
    enabled,
    retry: false,
    placeholderData: keepPreviousData,
  })
}

export function useBomScopePreview(topBoms: string[]) {
  return useQuery({
    queryKey: queryKeys.boms.scopePreview(topBoms),
    queryFn: () => previewBomScope(topBoms),
    enabled: topBoms.length > 0,
    retry: false,
    placeholderData: keepPreviousData,
  })
}
