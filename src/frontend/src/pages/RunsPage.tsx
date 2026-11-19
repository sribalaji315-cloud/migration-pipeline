import { Grid, Group, Pagination, Paper, Stack, Text, Title } from '@mantine/core'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { BronzeVersionTable } from '../components/domain/BronzeVersionTable'
import { RunStepTimeline } from '../components/domain/RunStepTimeline'
import { RunTable } from '../components/domain/RunTable'
import { HelpDrawer } from '../components/ui/HelpDrawer'
import { PageHeader } from '../components/ui/PageHeader'
import { useBronzeVersions, useRun, useRuns } from '../hooks/useRuns'
import { useHelpSections } from '../hooks/useHelpSections'

const PAGE_SIZE = 25

export function RunsPage() {
  const { t } = useTranslation(['pipeline', 'common', 'help'])
  const [page, setPage] = useState(1)
  const [selectedRunId, setSelectedRunId] = useState<string | null>(null)
  const runs = useRuns(PAGE_SIZE, (page - 1) * PAGE_SIZE)
  const selected = useRun(selectedRunId ?? runs.data?.items[0]?.runId ?? null)
  const versions = useBronzeVersions()
  const help = useHelpSections('runs')
  const totalPages = Math.max(1, Math.ceil((runs.data?.total ?? 0) / PAGE_SIZE))

  return (
    <Stack>
      <PageHeader title={t('pipeline:runs.title')} actions={<HelpDrawer buttonLabel={t('common:actions.help')} title={t('help:runs.title')} sections={help} />} />
      <Grid>
        <Grid.Col span={{ base: 12, lg: 8 }}>
          <Paper withBorder p="md">
            <Stack>
              <RunTable runs={runs.data?.items ?? []} loading={runs.isLoading} errorMessage={runs.error?.message} onSelect={(run) => setSelectedRunId(run.runId)} />
              <Group justify="space-between">
                <Text size="sm" c="dimmed">{t('pipeline:runs.total', { count: runs.data?.total ?? 0 })}</Text>
                <Pagination total={totalPages} value={page} onChange={setPage} size="sm" />
              </Group>
            </Stack>
          </Paper>
        </Grid.Col>
        <Grid.Col span={{ base: 12, lg: 4 }}>
          <Paper withBorder p="md">
            {selected.data ? <RunStepTimeline run={selected.data} /> : <Text size="sm" c="dimmed">{t('pipeline:runs.selectRun')}</Text>}
          </Paper>
        </Grid.Col>
      </Grid>
      <Paper withBorder p="md">
        <Stack>
          <Title order={5}>{t('pipeline:bronze.title')}</Title>
          <BronzeVersionTable versions={versions.data ?? []} loading={versions.isLoading} errorMessage={versions.error?.message} />
        </Stack>
      </Paper>
    </Stack>
  )
}
