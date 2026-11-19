import type { ColDef } from 'ag-grid-community'
import { useTranslation } from 'react-i18next'
import { formatDateTime, mtDataLabel } from '../../lib/format'
import type { BronzeVersion } from '../../types/pipeline'
import { DataGrid } from '../ui/DataGrid'
import { StatusBadge } from '../ui/StatusBadge'

type BronzeVersionTableProps = {
  versions: BronzeVersion[]
  loading: boolean
  errorMessage?: string
}

export function BronzeVersionTable({ versions, loading, errorMessage }: BronzeVersionTableProps) {
  const { t } = useTranslation('pipeline')
  const columns: ColDef<BronzeVersion>[] = [
    { field: 'version', headerName: t('bronze.version'), valueFormatter: (p) => `v${p.value}`, maxWidth: 110 },
    { field: 'status', headerName: t('bronze.status'), cellRenderer: (p: { value: string }) => <StatusBadge status={p.value} label={t(`status.${p.value}`)} /> },
    { field: 'mode', headerName: t('bronze.mode'), valueFormatter: (p) => t(`syncMode.${p.value}`) },
    { field: 'mappingRows', headerName: t('bronze.mappingRows') },
    { field: 'changedItems', headerName: t('bronze.changedItems') },
    { field: 'mtItems', headerName: t('bronze.mtItems') },
    {
      headerName: t('bronze.mtSource'),
      valueGetter: (p) => {
        const label = p.data ? mtDataLabel(p.data) : null
        return label ? t(label.key, label.params) : ''
      },
    },
    { field: 'swingItems', headerName: t('bronze.swingItems') },
    { field: 'swingBomRows', headerName: t('bronze.swingBomRows') },
    {
      headerName: t('bronze.swingSource'),
      valueGetter: (p) => (p.data?.swingCarriedFromVersion ? t('mtImport.swingCarried', { version: p.data.swingCarriedFromVersion }) : t('mtImport.swingFresh')),
    },
    { field: 'swingBomExtractedAt', headerName: t('bronze.swingBomExtractedAt'), valueFormatter: (p) => formatDateTime(p.value) },
    { field: 'swingBomHash', headerName: t('bronze.swingBomHash'), minWidth: 200 },
    { field: 'maxUpdatedAt', headerName: t('bronze.maxUpdatedAt'), valueFormatter: (p) => formatDateTime(p.value) },
  ]

  return (
    <DataGrid
      rows={versions}
      columns={columns}
      loading={loading}
      errorMessage={errorMessage}
      emptyMessage={t('bronze.empty')}
      height={260}
      getRowId={(v) => String(v.version)}
    />
  )
}
