import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { queryKeys } from '../api/queryKeys'
import { decideTypeChanges, getTypeChanges } from '../api/analysis'
import type { TypeDecisionStatus } from '../types/pipeline'

export function useTypeChanges(q: string, decision: TypeDecisionStatus | null, limit: number, offset: number) {
  return useQuery({
    queryKey: queryKeys.typeChanges.list(q, decision, limit, offset),
    queryFn: () => getTypeChanges(q, decision, limit, offset),
    retry: false,
    placeholderData: keepPreviousData,
  })
}

export function useDecideTypeChanges() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: ({ itemIds, decision, note }: { itemIds: string[]; decision: TypeDecisionStatus; note: string | null }) =>
      decideTypeChanges(itemIds, decision, note),
    onSuccess: () => Promise.all([
      client.invalidateQueries({ queryKey: queryKeys.typeChanges.all }),
      client.invalidateQueries({ queryKey: queryKeys.waves.all }),
      client.invalidateQueries({ queryKey: queryKeys.items.all }),
      client.invalidateQueries({ queryKey: queryKeys.overview.all }),
    ]),
  })
}
