import { apiRequest } from './http'
import type { HealthCheck, Run, RunRequest, SwingSource } from '../types/pipeline'

export function getSwingSource() {
  return apiRequest<SwingSource>('/api/swing/source')
}

export function testSwingConnection() {
  return apiRequest<HealthCheck>('/api/swing/test-connection', { method: 'POST' })
}

export function importSwing(request: RunRequest) {
  return apiRequest<Run>('/api/swing/import', { method: 'POST', body: JSON.stringify(request) })
}
