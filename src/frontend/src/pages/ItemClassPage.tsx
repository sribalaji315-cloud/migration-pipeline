import { Alert, Button, Group, Paper, SegmentedControl, SimpleGrid, Stack, Text, TextInput, Title } from '@mantine/core'
import { Search } from 'lucide-react'
import { useState, type FormEvent } from 'react'
import { useTranslation } from 'react-i18next'
import { useNavigate, useParams } from 'react-router-dom'
import { AttributeCard, MissingValuesPanel } from '../components/domain/AttributeCard'
import { ItemTypeCard } from '../components/domain/SwingItemPanels'
import { HelpDrawer } from '../components/ui/HelpDrawer'
import { PageHeader } from '../components/ui/PageHeader'
import { StatusBadge } from '../components/ui/StatusBadge'
import { useHelpSections } from '../hooks/useHelpSections'
import { useItemClass, useMissingValues } from '../hooks/useItemClass'
import { useItemType } from '../hooks/useItems'
import type { AttributeStatus } from '../types/pipeline'

type Filter = 'All' | AttributeStatus

export function ItemClassPage() {
  const { itemId = '' } = useParams()
  const navigate = useNavigate()
  const { t } = useTranslation(['pipeline', 'common', 'help'])
  const help = useHelpSections('itemClass')
  const [input, setInput] = useState(itemId)
  const [filter, setFilter] = useState<Filter>('All')
  const [missingFor, setMissingFor] = useState<string | null>(null)
  const itemClass = useItemClass(itemId)
  const itemType = useItemType(itemId)
  const missing = useMissingValues(itemId, missingFor)
  const data = itemClass.data
  const attributes = (data?.attributes ?? []).filter((a) => filter === 'All' || a.status === filter)

  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setMissingFor(null)
    navigate(`/items/${encodeURIComponent(input.trim())}`)
  }

  return (
    <Stack>
      <PageHeader title={t('pipeline:itemClass.title')} actions={<HelpDrawer buttonLabel={t('common:actions.help')} title={t('help:itemClass.title')} sections={help} />} />
      <Paper withBorder p="md">
        <form onSubmit={submit}>
          <Group align="flex-end">
            <TextInput label={t('pipeline:swing.itemId')} placeholder={t('pipeline:swing.itemIdPlaceholder')} value={input} onChange={(e) => setInput(e.currentTarget.value)} />
            <Button type="submit" leftSection={<Search size={16} />} disabled={!input.trim()}>{t('common:actions.search')}</Button>
          </Group>
        </form>
      </Paper>
      {itemClass.error ? <Alert color="nordAmber">{itemClass.error.message}</Alert> : null}
      {data ? (
        <Paper withBorder p="md">
          <Stack gap="xs">
            <Group justify="space-between">
              <Group gap="xs">
                <Title order={4}>{data.classId}</Title>
                <StatusBadge status={data.itemStatus} label={t(`pipeline:itemClass.status.${data.itemStatus}`)} />
                {data.readiness ? <StatusBadge status={data.readiness} label={t(`pipeline:readiness.${data.readiness}`)} /> : null}
                {data.typeChanged ? <StatusBadge status="Approve" label={t('pipeline:waves.typeChanged')} /> : null}
              </Group>
              <Text size="sm" c="dimmed">{t('pipeline:itemClass.header', { parent: data.parentClassId, run: data.runId })}</Text>
            </Group>
            {data.description ? <Text size="sm">{data.description}</Text> : null}
            <SegmentedControl
              value={filter}
              onChange={(v) => setFilter(v as Filter)}
              data={[
                { value: 'All', label: t('pipeline:itemClass.filterAll', { count: data.attributes.length }) },
                { value: 'Go', label: t('pipeline:itemClass.filterGo', { count: data.counts.go }) },
                { value: 'Highlight', label: t('pipeline:itemClass.filterHighlight', { count: data.counts.highlight }) },
                { value: 'Review', label: t('pipeline:itemClass.filterReview', { count: data.counts.review }) },
              ]}
              w="fit-content"
            />
          </Stack>
        </Paper>
      ) : null}
      {missingFor ? <MissingValuesPanel data={missing.data} loading={missing.isFetching} errorMessage={missing.error?.message} /> : null}
      {data ? (
        attributes.length === 0
          ? <Alert color="nordBlue">{t('pipeline:itemClass.noAttributes')}</Alert>
          : (
            <SimpleGrid cols={{ base: 1, sm: 2, lg: 3 }}>
              {attributes.map((a) => (
                <AttributeCard
                  key={`${a.attributeId}-${a.valueSource}`}
                  attribute={a}
                  selected={missingFor === a.attributeId}
                  onOpenMissing={setMissingFor}
                />
              ))}
            </SimpleGrid>
          )
      ) : null}
      {itemType.data ? <ItemTypeCard item={itemType.data} /> : null}
    </Stack>
  )
}
