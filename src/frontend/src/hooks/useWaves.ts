import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { queryKeys } from '../api/queryKeys'
import { getBomReadiness, getBomTree } from '../api/analysis'
import { createWave, exportWave, getWave, getWaveFiles, getWaveGate, getWaves, refreshWave } from '../api/waves'

export function useBomReadiness(q: string, status: string, limit: number, offset: number) {
  return useQuery({
    queryKey: queryKeys.boms.readiness(q, status, limit, offset),
    queryFn: () => getBomReadiness(q, status, limit, offset),
    retry: false,
    placeholderData: keepPreviousData,
  })
}

export function useBomTree(topItemId: string | null) {
  return useQuery({ queryKey: queryKeys.boms.tree(topItemId ?? ''), queryFn: () => getBomTree(topItemId!), enabled: Boolean(topItemId), retry: false })
}

export function useWaves(limit: number, offset: number) {
  return useQuery({ queryKey: queryKeys.waves.list(limit, offset), queryFn: () => getWaves(limit, offset), placeholderData: keepPreviousData })
}

export function useWave(waveId: string) {
  return useQuery({ queryKey: queryKeys.waves.detail(waveId), queryFn: () => getWave(waveId), enabled: Boolean(waveId), retry: false })
}

export function useWaveGate(waveId: string) {
  return useQuery({ queryKey: queryKeys.waves.gate(waveId), queryFn: () => getWaveGate(waveId), enabled: Boolean(waveId), retry: false })
}

export function useWaveFiles(waveId: string) {
  return useQuery({ queryKey: queryKeys.waves.files(waveId), queryFn: () => getWaveFiles(waveId), enabled: Boolean(waveId), retry: false })
}

function useInvalidateWaves() {
  const client = useQueryClient()
  return () => Promise.all([
    client.invalidateQueries({ queryKey: queryKeys.waves.all }),
    client.invalidateQueries({ queryKey: queryKeys.runs.all }),
    client.invalidateQueries({ queryKey: queryKeys.overview.all }),
  ])
}

export function useCreateWave() {
  const invalidate = useInvalidateWaves()
  return useMutation({
    mutationFn: ({ name, topBoms, effectiveDate }: { name: string; topBoms: string[]; effectiveDate: string | null }) =>
      createWave(name, topBoms, effectiveDate),
    onSuccess: invalidate,
  })
}

export function useRefreshWave() {
  const invalidate = useInvalidateWaves()
  return useMutation({ mutationFn: refreshWave, onSuccess: invalidate })
}

export function useExportWave() {
  const invalidate = useInvalidateWaves()
  return useMutation({ mutationFn: exportWave, onSuccess: invalidate })
}

export function useInvalidateWaveData() {
  return useInvalidateWaves()
}
