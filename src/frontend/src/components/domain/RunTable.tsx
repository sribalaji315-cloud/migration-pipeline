import type { ColDef } from 'ag-grid-community'
import { useTranslation } from 'react-i18next'
import { formatDateTime } from '../../lib/format'
import type { Run } from '../../types/pipeline'
import { DataGrid } from '../ui/DataGrid'
import { StatusBadge } from '../ui/StatusBadge'

type RunTableProps = {
  runs: Run[]
  loading: boolean
  errorMessage?: string
  onSelect: (run: Run) => void
}

export function RunTable({ runs, loading, errorMessage, onSelect }: RunTableProps) {
  const { t } = useTranslation('pipeline')
  const columns: ColDef<Run>[] = [
    { field: 'runId', headerName: t('runs.runId'), minWidth: 210 },
    { field: 'status', headerName: t('runs.status'), cellRenderer: (p: { value: string }) => <StatusBadge status={p.value} label={t(`status.${p.value}`)} /> },
    { headerName: t('runs.sync'), valueGetter: (p) => (!p.data?.syncRequested ? t('runs.noSync') : p.data.syncScope === 'SwingOnly' ? t('runs.swingOnly') : p.data.syncScope === 'MigrationToolOnly' ? t('runs.mtOnly', { mode: t(`syncMode.${p.data.syncMode}`) }) : t(`syncMode.${p.data.syncMode}`)) },
    { field: 'bronzeVersion', headerName: t('runs.bronze'), valueFormatter: (p) => (p.value ? `v${p.value}` : '') },
    { field: 'referenceVersion', headerName: t('runs.reference'), valueFormatter: (p) => (p.value ? `bs-${p.value}` : t('runs.none')) },
    { field: 'effectiveDate', headerName: t('runs.effectiveDate') },
    { headerName: t('runs.bomTypePolicy'), valueGetter: (p) => (p.data ? policyLabel(p.data, t) : '') },
    { field: 'queuedAt', headerName: t('runs.queuedAt'), valueFormatter: (p) => formatDateTime(p.value) },
    { field: 'finishedAt', headerName: t('runs.finishedAt'), valueFormatter: (p) => formatDateTime(p.value) },
  ]

  return (
    <DataGrid
      rows={runs}
      columns={columns}
      loading={loading}
      errorMessage={errorMessage}
      emptyMessage={t('runs.empty')}
      getRowId={(run) => run.runId}
      onRowClick={onSelect}
    />
  )
}

function policyLabel(run: Pick<Run, 'bomTypePolicy'>, t: (key: string, options?: Record<string, unknown>) => string) {
  return run.bomTypePolicy.mode === 'AllTypesUnfiltered'
    ? t('policy.allTypes')
    : t('policy.includeOnly', { types: (run.bomTypePolicy.types ?? []).join(', ') })
}
