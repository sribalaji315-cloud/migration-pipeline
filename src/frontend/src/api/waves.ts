import { apiDownload, apiRequest } from './http'
import type { PagedResult, Run, Wave, WaveFile, WaveGate } from '../types/pipeline'

export function getWaves(limit: number, offset: number) {
  return apiRequest<PagedResult<Wave>>(`/api/waves?limit=${limit}&offset=${offset}`)
}

export function getWave(waveId: string) {
  return apiRequest<Wave>(`/api/waves/${encodeURIComponent(waveId)}`)
}

export function createWave(name: string, topBoms: string[], effectiveDate: string | null) {
  return apiRequest<Wave>('/api/waves', { method: 'POST', body: JSON.stringify({ name, topBoms, effectiveDate }) })
}

export function refreshWave(waveId: string) {
  return apiRequest<Wave>(`/api/waves/${encodeURIComponent(waveId)}/refresh`, { method: 'POST' })
}

export function getWaveGate(waveId: string) {
  return apiRequest<WaveGate>(`/api/waves/${encodeURIComponent(waveId)}/gate`)
}

export function exportWave(waveId: string) {
  return apiRequest<Run>(`/api/waves/${encodeURIComponent(waveId)}/export`, { method: 'POST' })
}

export function getWaveFiles(waveId: string) {
  return apiRequest<WaveFile[]>(`/api/waves/${encodeURIComponent(waveId)}/files`)
}

export function downloadWaveFile(waveId: string, file: string) {
  return apiDownload(`/api/waves/${encodeURIComponent(waveId)}/files/${encodeURIComponent(file)}`, file)
}
