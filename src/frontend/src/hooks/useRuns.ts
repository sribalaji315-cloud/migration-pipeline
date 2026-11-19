import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { queryKeys } from '../api/queryKeys'
import { getBronzeVersions, getRun, getRuns, queueAnalysis, queueSync } from '../api/runs'
import type { Run, RunRequest } from '../types/pipeline'

export const POLL_MS = 2000

export function isActive(run: Pick<Run, 'status'> | null | undefined) {
  return run?.status === 'Queued' || run?.status === 'Syncing' || run?.status === 'Running'
}

export function useRuns(limit: number, offset: number) {
  return useQuery({
    queryKey: queryKeys.runs.list(limit, offset),
    queryFn: () => getRuns(limit, offset),
    refetchInterval: (query) => (query.state.data?.items.some(isActive) ? POLL_MS : false),
  })
}

export function useRun(runId: string | null) {
  return useQuery({
    queryKey: queryKeys.runs.detail(runId ?? ''),
    queryFn: () => getRun(runId!),
    enabled: Boolean(runId),
    refetchInterval: (query) => (isActive(query.state.data) ? POLL_MS : false),
  })
}

export function useBronzeVersions() {
  return useQuery({ queryKey: queryKeys.bronzeVersions.all, queryFn: getBronzeVersions })
}

function useInvalidateRunData() {
  const client = useQueryClient()
  return () => Promise.all([
    client.invalidateQueries({ queryKey: queryKeys.runs.all }),
    client.invalidateQueries({ queryKey: queryKeys.overview.all }),
    client.invalidateQueries({ queryKey: queryKeys.bronzeVersions.all }),
  ])
}

export function useQueueSync() {
  const invalidate = useInvalidateRunData()
  return useMutation({
    mutationFn: ({ full, request }: { full: boolean; request: RunRequest }) => queueSync(full, request),
    onSuccess: invalidate,
  })
}

export function useQueueAnalysis() {
  const invalidate = useInvalidateRunData()
  return useMutation({ mutationFn: queueAnalysis, onSuccess: invalidate })
}
