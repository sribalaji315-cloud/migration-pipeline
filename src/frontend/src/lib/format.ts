export function formatDateTime(value: string | null | undefined) {
  if (!value) {
    return ''
  }

  const date = new Date(value.endsWith('Z') || value.includes('+') ? value : `${value}Z`)
  return Number.isNaN(date.getTime()) ? value : date.toLocaleString()
}

export function formatNumber(value: number | null | undefined) {
  return value === null || value === undefined ? '' : value.toLocaleString()
}

export function todayIso() {
  const now = new Date()
  const offset = now.getTimezoneOffset() * 60000
  return new Date(now.getTime() - offset).toISOString().slice(0, 10)
}

/** i18n key + params describing where a snapshot's Migration Tool data came from. */
export function mtDataLabel(snapshot: { mtCarriedFromVersion: number | null; mappingRows: number; mtItems: number }) {
  if (snapshot.mtCarriedFromVersion) {
    return { key: 'pipeline:swingImport.mtCarried', params: { version: snapshot.mtCarriedFromVersion } }
  }

  return snapshot.mappingRows === 0 && snapshot.mtItems === 0
    ? { key: 'pipeline:swingImport.mtNone', params: {} }
    : { key: 'pipeline:swingImport.mtFresh', params: {} }
}
