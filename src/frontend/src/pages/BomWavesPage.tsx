import { Alert, Button, Drawer, Group, Pagination, Paper, Pill, Select, Stack, Text, TextInput, Title } from '@mantine/core'
import { useDebouncedValue } from '@mantine/hooks'
import { Plus, Search } from 'lucide-react'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useNavigate } from 'react-router-dom'
import { BomReadinessTable } from '../components/domain/BomReadinessTable'
import { BomTree } from '../components/domain/BomTree'
import { WaveTable } from '../components/domain/TypeChangeTable'
import { HelpDrawer } from '../components/ui/HelpDrawer'
import { PageHeader } from '../components/ui/PageHeader'
import { useAuth } from '../hooks/useAuth'
import { useHelpSections } from '../hooks/useHelpSections'
import { useBomReadiness, useBomTree, useCreateWave, useWaves } from '../hooks/useWaves'
import { bomReadinessStatuses } from '../types/pipeline'

const PAGE_SIZE = 50

export function BomWavesPage() {
  const { t } = useTranslation(['pipeline', 'common', 'help'])
  const auth = useAuth()
  const navigate = useNavigate()
  const canCreate = auth.can('Analyst')
  const help = useHelpSections('waves')
  const [search, setSearch] = useState('')
  const [debounced] = useDebouncedValue(search.trim(), 300)
  const [status, setStatus] = useState<string | null>(null)
  const [page, setPage] = useState(1)
  const [selected, setSelected] = useState<string[]>([])
  const [name, setName] = useState('')
  const [treeTop, setTreeTop] = useState<string | null>(null)
  const readiness = useBomReadiness(debounced, status ?? '', PAGE_SIZE, (page - 1) * PAGE_SIZE)
  const tree = useBomTree(treeTop)
  const waves = useWaves(100, 0)
  const create = useCreateWave()
  const totalPages = Math.max(1, Math.ceil((readiness.data?.total ?? 0) / PAGE_SIZE))

  function toggle(id: string) {
    setSelected((current) => (current.includes(id) ? current.filter((x) => x !== id) : [...current, id].sort()))
  }

  function submit() {
    create.mutate({ name: name.trim(), topBoms: selected, effectiveDate: null }, {
      onSuccess: (wave) => {
        setSelected([])
        setName('')
        navigate(`/waves/${encodeURIComponent(wave.waveId)}`)
      },
    })
  }

  return (
    <Stack>
      <PageHeader title={t('pipeline:waves.title')} actions={<HelpDrawer buttonLabel={t('common:actions.help')} title={t('help:waves.title')} sections={help} />} />
      <Paper withBorder p="md">
        <Stack gap="sm">
          <Title order={5}>{t('pipeline:waves.readinessTitle')}</Title>
          <Group align="flex-end">
            <TextInput
              label={t('pipeline:bomSelect.search')}
              placeholder={t('pipeline:bomSelect.searchPlaceholder')}
              leftSection={<Search size={16} />}
              value={search}
              onChange={(e) => { setSearch(e.currentTarget.value); setPage(1) }}
              w={300}
            />
            <Select
              label={t('pipeline:waves.readiness')}
              placeholder={t('pipeline:waves.allStatuses')}
              data={bomReadinessStatuses.map((s) => ({ value: s, label: t(`pipeline:readiness.${s}`) }))}
              value={status}
              onChange={(v) => { setStatus(v); setPage(1) }}
              clearable
              w={200}
            />
          </Group>
          {readiness.error ? <Alert color="nordAmber">{readiness.error.message}</Alert> : null}
          <BomReadinessTable
            rows={readiness.data?.items ?? []}
            loading={readiness.isFetching}
            selected={selected}
            selectable={canCreate}
            onToggle={toggle}
            onOpen={setTreeTop}
          />
          <Group justify="space-between">
            <Text size="sm" c="dimmed">{t('pipeline:waves.total', { count: readiness.data?.total ?? 0 })}</Text>
            <Pagination total={totalPages} value={page} onChange={setPage} size="sm" />
          </Group>
        </Stack>
      </Paper>
      {canCreate ? (
        <Paper withBorder p="md">
          <Stack gap="sm">
            <Title order={5}>{t('pipeline:waves.createTitle')}</Title>
            <Text size="sm" c="dimmed">{t('pipeline:waves.createHint')}</Text>
            {selected.length > 0 ? (
              <Pill.Group>
                {selected.map((id) => <Pill key={id} withRemoveButton onRemove={() => toggle(id)}>{id}</Pill>)}
              </Pill.Group>
            ) : <Text size="sm" c="dimmed">{t('pipeline:waves.noneSelected')}</Text>}
            <Group align="flex-end">
              <TextInput label={t('pipeline:waves.name')} placeholder={t('pipeline:waves.namePlaceholder')} value={name} onChange={(e) => setName(e.currentTarget.value)} w={320} />
              <Button leftSection={<Plus size={16} />} disabled={!name.trim() || selected.length === 0} loading={create.isPending} onClick={submit}>
                {t('pipeline:waves.create', { count: selected.length })}
              </Button>
            </Group>
            {create.error ? <Alert color="nordRed">{create.error.message}</Alert> : null}
          </Stack>
        </Paper>
      ) : null}
      <Paper withBorder p="md">
        <Stack gap="sm">
          <Title order={5}>{t('pipeline:waves.listTitle')}</Title>
          <WaveTable waves={waves.data?.items ?? []} loading={waves.isLoading} onOpen={(w) => navigate(`/waves/${encodeURIComponent(w.waveId)}`)} />
        </Stack>
      </Paper>
      <Drawer opened={Boolean(treeTop)} onClose={() => setTreeTop(null)} position="right" size="xl" title={t('pipeline:waves.treeTitle', { top: treeTop ?? '' })}>
        {tree.error ? <Alert color="nordRed">{tree.error.message}</Alert> : null}
        {tree.isLoading ? <Text size="sm" c="dimmed">{t('pipeline:itemClass.loading')}</Text> : null}
        {tree.data ? <BomTree tree={tree.data} /> : null}
      </Drawer>
    </Stack>
  )
}
