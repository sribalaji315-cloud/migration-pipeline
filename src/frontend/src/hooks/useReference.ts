import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { queryKeys } from '../api/queryKeys'
import { getReferenceVersions, uploadReference } from '../api/reference'

export function useReferenceVersions() {
  return useQuery({ queryKey: queryKeys.reference.versions(), queryFn: getReferenceVersions })
}

export function useUploadReference() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: uploadReference,
    onSuccess: () => Promise.all([
      client.invalidateQueries({ queryKey: queryKeys.reference.all }),
      client.invalidateQueries({ queryKey: queryKeys.overview.all }),
    ]),
  })
}
