import { Alert, Badge, Group, Paper, SimpleGrid, Stack, Text, UnstyledButton } from '@mantine/core'
import { useTranslation } from 'react-i18next'
import { formatNumber } from '../../lib/format'
import type { ItemClassAttribute, MissingValues } from '../../types/pipeline'
import { StatusBadge } from '../ui/StatusBadge'

type AttributeCardProps = {
  attribute: ItemClassAttribute
  selected: boolean
  onOpenMissing: (attributeId: string) => void
}

/** One class attribute: where it comes from, the reused global variable / value list and its coverage. No suggestions. */
export function AttributeCard({ attribute, selected, onOpenMissing }: AttributeCardProps) {
  const { t } = useTranslation('pipeline')
  const a = attribute
  const fields: Array<[string, string]> = [
    [t('itemClass.globalVariable'), a.globalVariable ?? t('itemClass.noneShort')],
    [t('itemClass.valueList'), a.valueListId ? `${a.valueListId} (${formatNumber(a.valueListSize)})` : t('itemClass.freeValue')],
    [t('itemClass.valueType'), a.valueType ?? t('itemClass.noneShort')],
    [t('itemClass.coverage'), a.valueSource === 'Inherited' ? t('itemClass.inheritedCoverage') : `${formatNumber(a.covered ?? 0)} / ${formatNumber(a.nUsed)}`],
  ]
  const clickable = a.status === 'Highlight'
  const body = (
    <Paper withBorder p="sm" className={selected ? 'attribute-card attribute-card-selected' : 'attribute-card'}>
      <Stack gap={6}>
        <Group justify="space-between" wrap="nowrap">
          <Text fw={700} truncate>{a.attributeId}</Text>
          <StatusBadge status={a.status} label={t(`itemClass.status.${a.status}`)} />
        </Group>
        <Group gap={6}>
          <Badge variant="outline" size="sm">{t(`itemClass.valueSource.${a.valueSource}`)}</Badge>
          <Badge variant="outline" size="sm" color={a.attributeSource === 'Bluestar' ? 'nordBlue' : 'nordAmber'}>
            {t(`itemClass.attributeSource.${a.attributeSource}`)}
          </Badge>
          {a.inheritedFrom ? <Text size="xs" c="dimmed">{t('itemClass.inheritedFrom', { items: a.inheritedFrom })}</Text> : null}
        </Group>
        <SimpleGrid cols={2} spacing="xs" verticalSpacing={4}>
          {fields.map(([label, value]) => (
            <Stack gap={0} key={label}>
              <Text size="xs" c="dimmed">{label}</Text>
              <Text size="sm" truncate>{value}</Text>
            </Stack>
          ))}
        </SimpleGrid>
        {a.note ? <Text size="xs" c="nordRed.7">{t(`itemClass.notes.${a.note}`, { defaultValue: a.note })}</Text> : null}
        {clickable ? <Text size="xs" c="nordBlue">{t('itemClass.showMissing')}</Text> : null}
      </Stack>
    </Paper>
  )

  return clickable
    ? <UnstyledButton onClick={() => onOpenMissing(a.attributeId)} aria-label={t('itemClass.showMissingFor', { attribute: a.attributeId })}>{body}</UnstyledButton>
    : body
}

type MissingValuesPanelProps = {
  data: MissingValues | undefined
  loading: boolean
  errorMessage?: string
}

/** Values used by the mapping that the assigned list / value type does not cover; fix in Bluestar or in the mapping. */
export function MissingValuesPanel({ data, loading, errorMessage }: MissingValuesPanelProps) {
  const { t } = useTranslation('pipeline')
  if (errorMessage) {
    return <Alert color="nordRed">{errorMessage}</Alert>
  }

  if (loading || !data) {
    return <Text size="sm" c="dimmed">{t('itemClass.loading')}</Text>
  }

  return (
    <Paper withBorder p="md">
      <Stack gap="xs">
        <Text fw={700}>{t('itemClass.missingTitle', { attribute: data.attributeId, count: data.values.length })}</Text>
        <Text size="sm" c="dimmed">
          {data.isList
            ? t('itemClass.missingList', { list: data.valueListId })
            : t('itemClass.missingType', { type: data.valueType ?? '' })}
        </Text>
        <Group gap={6}>
          {data.values.map((v) => <Badge key={v} color="nordRed" variant="light" className="code-badge">{v}</Badge>)}
        </Group>
      </Stack>
    </Paper>
  )
}
