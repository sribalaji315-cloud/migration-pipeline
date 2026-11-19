import { useMutation, useQuery, useQueryClient, type QueryClient } from '@tanstack/react-query'
import { getMigrationToolSource, importMigrationTool, testMigrationToolConnection } from '../api/migrationTool'
import { queryKeys } from '../api/queryKeys'
import { getSwingSource, importSwing, testSwingConnection } from '../api/swing'
import type { RunRequest, SourceStatus } from '../types/pipeline'
import { POLL_MS } from './useRuns'

const dependentRoots = [
  queryKeys.swing.all,
  queryKeys.migrationTool.all,
  queryKeys.items.all,
  queryKeys.boms.all,
  queryKeys.overview.all,
  queryKeys.runs.all,
  queryKeys.bronzeVersions.all,
]

/** Everything that depends on the latest snapshot / analysis run, optionally except one root (the caller's own query). */
function invalidateAfterImport(client: QueryClient, exceptRoot?: unknown) {
  return Promise.all(dependentRoots
    .filter((root) => root[0] !== exceptRoot)
    .map((queryKey) => client.invalidateQueries({ queryKey })))
}

/** Polls while an import is active and refreshes dependent data once it finishes. */
function useSourceStatus<T extends SourceStatus>(queryKey: readonly unknown[], fetcher: () => Promise<T>, enabled: boolean) {
  const client = useQueryClient()
  return useQuery({
    queryKey,
    queryFn: async () => {
      const previous = client.getQueryData<T>(queryKey)
      const next = await fetcher()
      if (previous?.activeImport && !next.activeImport) {
        // Not awaited and excluding this query: awaiting a refetch of itself from inside its own queryFn deadlocks.
        void invalidateAfterImport(client, queryKey[0])
      }

      return next
    },
    enabled,
    refetchInterval: (query) => (query.state.data?.activeImport ? POLL_MS : false),
  })
}

export function useSwingSource(enabled: boolean) {
  return useSourceStatus(queryKeys.swing.source(), getSwingSource, enabled)
}

export function useMigrationToolSource(enabled: boolean) {
  return useSourceStatus(queryKeys.migrationTool.source(), getMigrationToolSource, enabled)
}

export function useTestSwingConnection() {
  return useMutation({ mutationFn: testSwingConnection })
}

export function useTestMigrationToolConnection() {
  return useMutation({ mutationFn: testMigrationToolConnection })
}

export function useImportSwing() {
  const client = useQueryClient()
  return useMutation({ mutationFn: importSwing, onSuccess: () => invalidateAfterImport(client) })
}

export function useImportMigrationTool() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: ({ full, request, topBoms }: { full: boolean; request: RunRequest; topBoms?: string[] }) => importMigrationTool(full, request, topBoms),
    onSuccess: () => invalidateAfterImport(client),
  })
}
