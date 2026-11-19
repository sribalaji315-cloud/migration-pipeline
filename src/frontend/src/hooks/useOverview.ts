import { useQuery } from '@tanstack/react-query'
import { queryKeys } from '../api/queryKeys'
import { getOverview } from '../api/runs'
import { POLL_MS } from './useRuns'

export function useOverview() {
  return useQuery({
    queryKey: queryKeys.overview.all,
    queryFn: getOverview,
    refetchInterval: (query) => (query.state.data && query.state.data.activeRuns > 0 ? POLL_MS : false),
  })
}
