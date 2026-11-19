import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { queryKeys } from '../api/queryKeys'
import { acknowledgeIssueGroup, getIssueGroup, getIssueGroups, getIssueItems, getIssueSummary } from '../api/issues'
import type { IssueFilters } from '../types/pipeline'

export function useIssueGroups(filters: IssueFilters, limit: number, offset: number) {
  return useQuery({
    queryKey: queryKeys.issueGroups.list(filters, limit, offset),
    queryFn: () => getIssueGroups(filters, limit, offset),
    retry: false,
    placeholderData: keepPreviousData,
  })
}

export function useIssueSummary() {
  return useQuery({ queryKey: queryKeys.issueGroups.summary(), queryFn: getIssueSummary, retry: false })
}

export function useIssueGroup(id: string | null) {
  return useQuery({ queryKey: queryKeys.issueGroups.detail(id ?? ''), queryFn: () => getIssueGroup(id!), enabled: Boolean(id), retry: false })
}

export function useIssueItems(id: string | null, limit: number, offset: number) {
  return useQuery({
    queryKey: queryKeys.issueGroups.items(id ?? '', limit, offset),
    queryFn: () => getIssueItems(id!, limit, offset),
    enabled: Boolean(id),
    retry: false,
    placeholderData: keepPreviousData,
  })
}

export function useAcknowledgeIssue() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: ({ id, note }: { id: string; note: string | null }) => acknowledgeIssueGroup(id, note),
    onSuccess: () => client.invalidateQueries({ queryKey: queryKeys.issueGroups.all }),
  })
}
