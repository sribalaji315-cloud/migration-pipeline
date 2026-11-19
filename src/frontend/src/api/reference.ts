import { apiFormRequest, apiRequest } from './http'
import type { BluestarFileKind, ReferenceVersion } from '../types/pipeline'

export function getReferenceVersions() {
  return apiRequest<ReferenceVersion[]>('/api/reference/bluestar/versions')
}

export function uploadReference(files: Record<BluestarFileKind, File>) {
  const form = new FormData()
  for (const [kind, file] of Object.entries(files)) {
    form.append(kind, file, file.name)
  }

  return apiFormRequest<ReferenceVersion>('/api/reference/bluestar', form)
}
