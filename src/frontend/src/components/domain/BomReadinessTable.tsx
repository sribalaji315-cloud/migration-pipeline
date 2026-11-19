import { Checkbox } from '@mantine/core'
import type { ColDef } from 'ag-grid-community'
import { useTranslation } from 'react-i18next'
import { formatNumber } from '../../lib/format'
import type { BomReadiness } from '../../types/pipeline'
import { DataGrid } from '../ui/DataGrid'
import { StatusBadge } from '../ui/StatusBadge'

type BomReadinessTableProps = {
  rows: BomReadiness[]
  loading: boolean
  errorMessage?: string
  selected: string[]
  selectable: boolean
  onToggle: (topItemId: string) => void
  onOpen: (topItemId: string) => void
}

/** Top BOM readiness; the row click selects (when allowed) and the item cell opens the BOM tree. */
export function BomReadinessTable({ rows, loading, errorMessage, selected, selectable, onToggle, onOpen }: BomReadinessTableProps) {
  const { t } = useTranslation('pipeline')
  const selectedSet = new Set(selected)
  const columns: ColDef<BomReadiness>[] = [
    ...(selectable ? [{
      colId: 'select', headerName: '', maxWidth: 56, sortable: false, filter: false,
      cellRenderer: (p: { data?: BomReadiness }) => (p.data
        ? <Checkbox aria-label={p.data.topItemId} checked={selectedSet.has(p.data.topItemId)} readOnly tabIndex={-1} mt={8} />
        : null),
    } as ColDef<BomReadiness>] : []),
    {
      colId: 'topItemId', headerName: t('waves.topBom'), minWidth: 150,
      cellRenderer: (p: { data?: BomReadiness }) => (p.data
        ? (
          <button
            type="button"
            className="link-button"
            onClick={() => onOpen(p.data!.topItemId)}
          >
            {p.data.topItemId}
          </button>
        )
        : null),
    },
    { colId: 'description', field: 'description', headerName: t('bomSelect.description'), minWidth: 180 },
    {
      colId: 'status', headerName: t('waves.readiness'), maxWidth: 150,
      cellRenderer: (p: { data?: BomReadiness }) => (p.data ? <StatusBadge status={p.data.status} label={t(`readiness.${p.data.status}`)} /> : null),
    },
    { colId: 'items', headerName: t('waves.items'), maxWidth: 110, valueGetter: (p) => formatNumber(p.data?.items) },
    { colId: 'blockedItems', headerName: t('waves.blocked'), maxWidth: 110, valueGetter: (p) => formatNumber(p.data?.blockedItems) },
    { colId: 'reviewItems', headerName: t('waves.review'), maxWidth: 110, valueGetter: (p) => formatNumber(p.data?.reviewItems) },
    { colId: 'decisionItems', headerName: t('waves.decision'), maxWidth: 110, valueGetter: (p) => formatNumber(p.data?.decisionItems) },
    { colId: 'notImportedItems', headerName: t('waves.notImported'), maxWidth: 130, valueGetter: (p) => formatNumber(p.data?.notImportedItems) },
    { colId: 'alreadyExportedItems', headerName: t('waves.exported'), maxWidth: 120, valueGetter: (p) => formatNumber(p.data?.alreadyExportedItems) },
  ]

  return (
    <DataGrid
      rows={rows}
      columns={columns}
      loading={loading}
      errorMessage={errorMessage}
      emptyMessage={t('waves.readinessEmpty')}
      height={360}
      getRowId={(b) => b.topItemId}
      onRowClick={selectable ? (b) => onToggle(b.topItemId) : undefined}
    />
  )
}
