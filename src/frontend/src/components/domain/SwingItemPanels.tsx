import { Badge, Group, Paper, SimpleGrid, Stack, Text } from '@mantine/core'
import type { ColDef } from 'ag-grid-community'
import { useTranslation } from 'react-i18next'
import type { BomLine, ItemType } from '../../types/pipeline'
import { DataGrid } from '../ui/DataGrid'
import { StatusBadge } from '../ui/StatusBadge'

export function ItemTypeCard({ item }: { item: ItemType }) {
  const { t } = useTranslation('pipeline')
  const rows: Array<[string, string]> = [
    [t('swing.source'), `${item.sourceTable}.${item.sourceColumn}`],
    [t('swing.rawCode'), item.rawCode ?? t('swing.blank')],
    [t('swing.normalizedType'), item.sourceType],
    [t('swing.models'), String(item.modelCount)],
    [t('swing.run'), item.runId],
  ]

  return (
    <Paper withBorder p="md">
      <Stack gap="xs">
        <Group gap="xs">
          <Text fw={700}>{item.itemId}</Text>
          <Badge color={item.sourceType === 'UNKNOWN' ? 'nordRed' : 'nordBlue'} variant="light">{item.sourceType}</Badge>
          {item.isAmbiguous ? <StatusBadge status="Block" label={t('swing.ambiguous')} /> : null}
          {item.typeChanged ? (
            <>
              <Text size="sm">→</Text>
              <Badge color="nordAmber" variant="light">{item.resolvedType}</Badge>
              <StatusBadge
                status={item.decision && item.decisionMatches ? item.decision : 'Pending'}
                label={t(`typeChanges.decisions.${item.decision && item.decisionMatches ? item.decision : 'Pending'}`)}
              />
            </>
          ) : null}
        </Group>
        {item.typeChanged ? <Text size="sm" c="dimmed">{t('typeChanges.becauseInherited', { attrs: item.inheritedAttrs })}</Text> : null}
        <SimpleGrid cols={{ base: 1, sm: 5 }}>
          {rows.map(([label, value]) => (
            <Stack gap={0} key={label}>
              <Text size="xs" c="dimmed">{label}</Text>
              <Text size="sm">{value}</Text>
            </Stack>
          ))}
        </SimpleGrid>
      </Stack>
    </Paper>
  )
}

type BomLineTableProps = {
  lines: BomLine[]
  loading: boolean
  errorMessage?: string
  onOpenChild: (itemId: string) => void
}

export function BomLineTable({ lines, loading, errorMessage, onOpenChild }: BomLineTableProps) {
  const { t } = useTranslation('pipeline')
  const yesNoUnknown = (value: boolean | null | undefined) =>
    value === null || value === undefined ? t('swing.unresolved') : value ? t('swing.yes') : t('swing.no')
  const columns: ColDef<BomLine>[] = [
    { field: 'bomPosition', headerName: t('swing.position'), maxWidth: 110 },
    { field: 'bomType', headerName: t('swing.bomType'), maxWidth: 110 },
    { field: 'childItemId', headerName: t('swing.child'), minWidth: 150 },
    { field: 'childModelNo', headerName: t('swing.childModel') },
    { field: 'quantity', headerName: t('swing.quantity'), valueGetter: (p) => p.data?.quantity ?? p.data?.quantityRaw },
    { field: 'unit', headerName: t('swing.unit'), maxWidth: 90 },
    { headerName: t('swing.pseudo'), valueGetter: (p) => (p.data ? `${yesNoUnknown(p.data.isPseudoLine)} (${p.data.pseudoRaw ?? ''})` : '') },
    { field: 'dateIn', headerName: t('swing.dateIn') },
    { field: 'dateOut', headerName: t('swing.dateOut') },
    {
      headerName: t('swing.effective'),
      cellRenderer: (p: { data?: BomLine }) => {
        if (!p.data) {
          return null
        }

        if (!p.data.includedByPolicy) {
          return <StatusBadge status="Info" label={t('swing.excludedByPolicy')} />
        }

        return p.data.isEffective === null
          ? <StatusBadge status="Block" label={t('swing.unresolved')} />
          : <StatusBadge status={p.data.isEffective ? 'Completed' : 'Info'} label={yesNoUnknown(p.data.isEffective)} />
      },
    },
    { field: 'bomLineId', headerName: t('swing.lineId'), minWidth: 200 },
  ]

  return (
    <DataGrid
      rows={lines}
      columns={columns}
      loading={loading}
      errorMessage={errorMessage}
      emptyMessage={t('swing.noLines')}
      height={420}
      getRowId={(l) => l.bomLineId}
      onRowClick={(l) => l.childItemId && onOpenChild(l.childItemId)}
    />
  )
}
