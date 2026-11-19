import { Alert, Checkbox, Group, Pagination, Pill, Stack, Switch, Text, TextInput } from '@mantine/core'
import type { ColDef } from 'ag-grid-community'
import { Search } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { formatNumber } from '../../lib/format'
import type { BomScopePreview, BomSummary, PagedResult } from '../../types/pipeline'
import { DataGrid } from '../ui/DataGrid'

type BomSelectorProps = {
  search: string
  onSearch: (value: string) => void
  topOnly: boolean
  onTopOnly: (value: boolean) => void
  page: number
  pageSize: number
  onPage: (page: number) => void
  boms: PagedResult<BomSummary> | undefined
  loading: boolean
  errorMessage?: string
  selected: string[]
  onToggle: (itemId: string) => void
  onClear: () => void
  preview: BomScopePreview | undefined
  previewLoading: boolean
  previewError?: string
}

/** Server-paged BOM picker with checkbox selection and a scope preview (selected BOMs + all their children). */
export function BomSelector({
  search, onSearch, topOnly, onTopOnly, page, pageSize, onPage, boms, loading, errorMessage,
  selected, onToggle, onClear, preview, previewLoading, previewError,
}: BomSelectorProps) {
  const { t } = useTranslation('pipeline')
  const selectedSet = new Set(selected)
  const totalPages = Math.max(1, Math.ceil((boms?.total ?? 0) / pageSize))
  const columns: ColDef<BomSummary>[] = [
    {
      colId: 'select',
      headerName: '',
      maxWidth: 56,
      sortable: false,
      filter: false,
      // Display only: the row click toggles selection (a checkbox handler as well would toggle twice).
      cellRenderer: (p: { data?: BomSummary }) => (p.data
        ? <Checkbox aria-label={p.data.itemId} checked={selectedSet.has(p.data.itemId)} readOnly tabIndex={-1} mt={8} />
        : null),
    },
    { colId: 'itemId', field: 'itemId', headerName: t('bomSelect.item'), minWidth: 150 },
    { colId: 'description', field: 'description', headerName: t('bomSelect.description'), minWidth: 200 },
    { colId: 'childCount', field: 'childCount', headerName: t('bomSelect.children'), maxWidth: 140 },
    { colId: 'lineCount', field: 'lineCount', headerName: t('bomSelect.lines'), maxWidth: 140 },
    { colId: 'isTop', headerName: t('bomSelect.top'), maxWidth: 120, valueGetter: (p) => (p.data?.isTop ? t('swing.yes') : t('swing.no')) },
  ]

  return (
    <Stack gap="xs">
      <Group align="flex-end">
        <TextInput
          label={t('bomSelect.search')}
          placeholder={t('bomSelect.searchPlaceholder')}
          leftSection={<Search size={16} />}
          value={search}
          onChange={(e) => onSearch(e.currentTarget.value)}
          w={320}
        />
        <Switch label={t('bomSelect.topOnly')} checked={topOnly} onChange={(e) => onTopOnly(e.currentTarget.checked)} mb={8} />
      </Group>
      <DataGrid
        rows={boms?.items ?? []}
        columns={columns}
        loading={loading}
        errorMessage={errorMessage}
        emptyMessage={t('bomSelect.empty')}
        height={300}
        getRowId={(b) => b.itemId}
        onRowClick={(b) => onToggle(b.itemId)}
      />
      <Group justify="space-between">
        <Text size="sm" c="dimmed">{t('bomSelect.total', { count: boms?.total ?? 0 })}</Text>
        <Pagination total={totalPages} value={page} onChange={onPage} size="sm" />
      </Group>
      {selected.length > 0 ? (
        <Stack gap={6}>
          <Group gap="xs">
            <Text size="sm" fw={600}>{t('bomSelect.selected', { count: selected.length })}</Text>
            <Text size="sm" c="nordBlue" style={{ cursor: 'pointer' }} onClick={onClear}>{t('bomSelect.clear')}</Text>
          </Group>
          <Pill.Group>
            {selected.map((id) => <Pill key={id} withRemoveButton onRemove={() => onToggle(id)}>{id}</Pill>)}
          </Pill.Group>
          {previewError ? <Alert color="nordRed">{previewError}</Alert> : null}
          {preview ? (
            <Alert color={preview.unknown.length > 0 ? 'nordRed' : 'nordBlue'}>
              {previewLoading
                ? t('bomSelect.previewLoading')
                : t('bomSelect.preview', { boms: preview.selected, items: formatNumber(preview.items) })}
              {preview.unknown.length > 0 ? ` ${t('bomSelect.unknown', { ids: preview.unknown.join(', ') })}` : ''}
            </Alert>
          ) : null}
        </Stack>
      ) : (
        <Text size="sm" c="dimmed">{t('bomSelect.none')}</Text>
      )}
    </Stack>
  )
}
