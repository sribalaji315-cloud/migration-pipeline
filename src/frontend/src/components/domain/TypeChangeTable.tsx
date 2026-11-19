import { Checkbox } from '@mantine/core'
import type { ColDef } from 'ag-grid-community'
import { useTranslation } from 'react-i18next'
import { formatDateTime, formatNumber } from '../../lib/format'
import type { TypeChange, Wave } from '../../types/pipeline'
import { DataGrid } from '../ui/DataGrid'
import { StatusBadge } from '../ui/StatusBadge'

type TypeChangeTableProps = {
  rows: TypeChange[]
  loading: boolean
  errorMessage?: string
  selected: string[]
  selectable: boolean
  onToggle: (itemId: string) => void
}

export function TypeChangeTable({ rows, loading, errorMessage, selected, selectable, onToggle }: TypeChangeTableProps) {
  const { t } = useTranslation('pipeline')
  const selectedSet = new Set(selected)
  const columns: ColDef<TypeChange>[] = [
    ...(selectable ? [{
      colId: 'select', headerName: '', maxWidth: 56, sortable: false, filter: false,
      cellRenderer: (p: { data?: TypeChange }) => (p.data
        ? <Checkbox aria-label={p.data.itemId} checked={selectedSet.has(p.data.itemId)} readOnly tabIndex={-1} mt={8} />
        : null),
    } as ColDef<TypeChange>] : []),
    { colId: 'itemId', field: 'itemId', headerName: t('typeChanges.item'), minWidth: 140 },
    { colId: 'description', field: 'description', headerName: t('bomSelect.description'), minWidth: 170 },
    { colId: 'swingTypeCode', headerName: t('typeChanges.swingType'), maxWidth: 160, valueGetter: (p) => `${p.data?.sourceType} (${p.data?.swingTypeCode ?? '–'})` },
    { colId: 'resolvedType', field: 'resolvedType', headerName: t('typeChanges.resolvedType'), maxWidth: 160 },
    { colId: 'inheritedAttrs', field: 'inheritedAttrs', headerName: t('typeChanges.inheritedAttrs'), minWidth: 200 },
    { colId: 'affectedBoms', headerName: t('typeChanges.boms'), maxWidth: 100, valueGetter: (p) => formatNumber(p.data?.affectedBoms) },
    {
      colId: 'decision', headerName: t('typeChanges.decision'), maxWidth: 140,
      cellRenderer: (p: { data?: TypeChange }) => (p.data ? <StatusBadge status={p.data.decision} label={t(`typeChanges.decisions.${p.data.decision}`)} /> : null),
    },
    { colId: 'decidedBy', headerName: t('typeChanges.decidedBy'), minWidth: 160, valueGetter: (p) => (p.data?.decidedByUserId ? `${p.data.decidedByUserId} · ${formatDateTime(p.data.decidedAt)}` : '') },
    { colId: 'note', field: 'note', headerName: t('typeChanges.note'), minWidth: 160 },
  ]

  return (
    <DataGrid
      rows={rows}
      columns={columns}
      loading={loading}
      errorMessage={errorMessage}
      emptyMessage={t('typeChanges.empty')}
      height={420}
      getRowId={(r) => r.itemId}
      onRowClick={selectable ? (r) => onToggle(r.itemId) : undefined}
    />
  )
}

export function WaveTable({ waves, loading, onOpen }: { waves: Wave[]; loading: boolean; onOpen: (wave: Wave) => void }) {
  const { t } = useTranslation('pipeline')
  const columns: ColDef<Wave>[] = [
    { colId: 'waveId', field: 'waveId', headerName: t('waves.waveId'), maxWidth: 110 },
    { colId: 'name', field: 'name', headerName: t('waves.name'), minWidth: 180 },
    {
      colId: 'status', headerName: t('waves.status'), maxWidth: 140,
      cellRenderer: (p: { data?: Wave }) => (p.data ? <StatusBadge status={p.data.status} label={t(`waves.statuses.${p.data.status}`)} /> : null),
    },
    { colId: 'topBoms', headerName: t('waves.topBoms'), minWidth: 200, valueGetter: (p) => p.data?.topBoms.join(', ') },
    { colId: 'items', headerName: t('waves.items'), maxWidth: 110, valueGetter: (p) => formatNumber(p.data?.items) },
    { colId: 'pins', headerName: t('waves.pins'), maxWidth: 150, valueGetter: (p) => (p.data ? `v${p.data.bronzeVersion} · bs-${p.data.referenceVersion}` : '') },
    { colId: 'createdAt', headerName: t('waves.createdAt'), minWidth: 170, valueGetter: (p) => formatDateTime(p.data?.createdAt) },
  ]

  return (
    <DataGrid rows={waves} columns={columns} loading={loading} emptyMessage={t('waves.empty')} height={280} getRowId={(w) => w.waveId} onRowClick={onOpen} />
  )
}
