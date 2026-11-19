import type { ColDef } from 'ag-grid-community'
import { useTranslation } from 'react-i18next'
import type { SourceIssueSummary } from '../../types/pipeline'
import { DataGrid } from '../ui/DataGrid'
import { StatusBadge } from '../ui/StatusBadge'

export function SourceIssueTable({ issues, loading }: { issues: SourceIssueSummary[]; loading: boolean }) {
  const { t } = useTranslation(['issues'])
  const columns: ColDef<SourceIssueSummary>[] = [
    { field: 'severity', headerName: t('issues:columns.severity'), maxWidth: 140, cellRenderer: (p: { value: string }) => <StatusBadge status={p.value} label={t(`issues:severity.${p.value}`)} /> },
    { field: 'issueType', headerName: t('issues:columns.issueType'), minWidth: 240 },
    { headerName: t('issues:columns.meaning'), minWidth: 320, valueGetter: (p) => (p.data ? t(`issues:types.${p.data.issueType}`, { defaultValue: '' }) : '') },
    { field: 'fixSource', headerName: t('issues:columns.fixSource'), valueFormatter: (p) => t(`issues:fixSource.${p.value}`) },
    { field: 'count', headerName: t('issues:columns.count'), maxWidth: 120 },
  ]

  return (
    <DataGrid
      rows={issues}
      columns={columns}
      loading={loading}
      emptyMessage={t('issues:empty')}
      height={300}
      getRowId={(i) => `${i.issueType}|${i.severity}`}
    />
  )
}
