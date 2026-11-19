import type { ColDef } from 'ag-grid-community'
import { useTranslation } from 'react-i18next'
import { formatNumber } from '../../lib/format'
import type { IssueGroup, IssueItem } from '../../types/pipeline'
import { DataGrid } from '../ui/DataGrid'
import { StatusBadge } from '../ui/StatusBadge'

type IssueGroupTableProps = {
  groups: IssueGroup[]
  loading: boolean
  errorMessage?: string
  height?: number
  onOpen?: (group: IssueGroup) => void
}

export function IssueGroupTable({ groups, loading, errorMessage, height = 420, onOpen }: IssueGroupTableProps) {
  const { t } = useTranslation('issues')
  const columns: ColDef<IssueGroup>[] = [
    {
      colId: 'severity', headerName: t('columns.severity'), maxWidth: 120,
      cellRenderer: (p: { data?: IssueGroup }) => (p.data ? <StatusBadge status={p.data.severity} label={t(`severity.${p.data.severity}`)} /> : null),
    },
    { colId: 'issueType', field: 'issueType', headerName: t('columns.issueType'), minWidth: 230, tooltipValueGetter: (p) => t(`types.${p.data?.issueType}`, { defaultValue: '' }) },
    { colId: 'groupKey', field: 'groupKey', headerName: t('columns.groupKey'), minWidth: 220 },
    { colId: 'fixSource', headerName: t('columns.fixSource'), minWidth: 180, valueGetter: (p) => (p.data ? t(`fixSource.${p.data.fixSource}`) : '') },
    { colId: 'fixTarget', field: 'fixTarget', headerName: t('columns.fixTarget'), minWidth: 150 },
    { colId: 'affectedItems', headerName: t('columns.items'), maxWidth: 110, valueGetter: (p) => formatNumber(p.data?.affectedItems) },
    { colId: 'affectedBoms', headerName: t('columns.boms'), maxWidth: 110, valueGetter: (p) => formatNumber(p.data?.affectedBoms) },
    {
      colId: 'status', headerName: t('columns.status'), maxWidth: 150,
      cellRenderer: (p: { data?: IssueGroup }) => (p.data ? <StatusBadge status={p.data.status} label={t(`status.${p.data.status}`)} /> : null),
    },
  ]

  return (
    <DataGrid
      rows={groups}
      columns={columns}
      loading={loading}
      errorMessage={errorMessage}
      emptyMessage={t('groupsEmpty')}
      height={height}
      getRowId={(g) => g.id}
      onRowClick={onOpen}
    />
  )
}

export function IssueItemTable({ items, loading, errorMessage }: { items: IssueItem[]; loading: boolean; errorMessage?: string }) {
  const { t } = useTranslation('issues')
  const wrap: ColDef<IssueItem> = { wrapText: true, autoHeight: true, cellClass: 'grid-cell-wrap' }
  const columns: Array<{ col: ColDef<IssueItem>; keep?: boolean; has?: (r: IssueItem) => unknown }> = [
    { col: { colId: 'itemId', field: 'itemId', headerName: t('columns.item'), minWidth: 120, maxWidth: 160 }, keep: true },
    { col: { colId: 'parentItemId', field: 'parentItemId', headerName: t('columns.parent'), minWidth: 120, maxWidth: 160 }, has: (r) => r.parentItemId },
    { col: { colId: 'attributeId', field: 'attributeId', headerName: t('columns.attribute'), minWidth: 140, ...wrap }, has: (r) => r.attributeId },
    { col: { colId: 'valueCode', field: 'valueCode', headerName: t('columns.value'), minWidth: 110, ...wrap }, has: (r) => r.valueCode },
    {
      col: {
        colId: 'legacy', headerName: t('columns.legacy'), minWidth: 140, ...wrap,
        valueGetter: (p) => [p.data?.legacyFeatureId, p.data?.legacyValue].filter(Boolean).join(' = '),
      },
      has: (r) => r.legacyFeatureId || r.legacyValue,
    },
    { col: { colId: 'valueListId', field: 'valueListId', headerName: t('columns.valueList'), minWidth: 140, ...wrap }, has: (r) => r.valueListId },
    { col: { colId: 'detail', field: 'detail', headerName: t('columns.detail'), minWidth: 320, flex: 3, ...wrap }, keep: true },
  ]
  // Columns that are empty for every row (e.g. Swing item issues have no attribute/value) only take width from Detail.
  const visible = columns
    .filter((c) => c.keep || items.length === 0 || items.some((r) => c.has?.(r)))
    .map((c) => c.col)

  return <DataGrid rows={items} columns={visible} loading={loading} errorMessage={errorMessage} emptyMessage={t('itemsEmpty')} height={420} />
}
