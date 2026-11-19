import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { queryKeys } from '../api/queryKeys'
import { getItemClass, getMissingValues } from '../api/analysis'

export function useItemClass(itemId: string) {
  return useQuery({ queryKey: queryKeys.items.class(itemId), queryFn: () => getItemClass(itemId), enabled: Boolean(itemId), retry: false })
}

export function useMissingValues(itemId: string, attributeId: string | null) {
  return useQuery({
    queryKey: queryKeys.items.missingValues(itemId, attributeId ?? ''),
    queryFn: () => getMissingValues(itemId, attributeId!),
    enabled: Boolean(itemId && attributeId),
    retry: false,
    placeholderData: keepPreviousData,
  })
}
