import { Alert, Anchor, Group, Paper, SimpleGrid, Stack, Title } from '@mantine/core'
import { useTranslation } from 'react-i18next'
import { Link, useNavigate } from 'react-router-dom'
import { IssueGroupTable } from '../components/domain/IssueGroupTable'
import { RunLauncher } from '../components/domain/RunLauncher'
import { SourceIssueTable } from '../components/domain/SourceIssueTable'
import { HelpDrawer } from '../components/ui/HelpDrawer'
import { PageHeader } from '../components/ui/PageHeader'
import { StatCard } from '../components/ui/StatCard'
import { StatusBadge } from '../components/ui/StatusBadge'
import { useAuth } from '../hooks/useAuth'
import { useOverview } from '../hooks/useOverview'
import { useQueueAnalysis, useQueueSync } from '../hooks/useRuns'
import { useHelpSections } from '../hooks/useHelpSections'
import { formatDateTime, formatNumber } from '../lib/format'

export function OverviewPage() {
  const { t } = useTranslation(['pipeline', 'common', 'help'])
  const auth = useAuth()
  const navigate = useNavigate()
  const overview = useOverview()
  const sync = useQueueSync()
  const analysis = useQueueAnalysis()
  const help = useHelpSections('overview')
  const data = overview.data
  const mutationError = sync.error ?? analysis.error

  return (
    <Stack>
      <PageHeader title={t('pipeline:overview.title')} actions={<HelpDrawer buttonLabel={t('common:actions.help')} title={t('help:overview.title')} sections={help} />} />
      {overview.error ? <Alert color="nordRed">{overview.error.message}</Alert> : null}
      <SimpleGrid cols={{ base: 1, sm: 2, lg: 4 }}>
        <StatCard
          label={t('pipeline:overview.bronze')}
          value={data?.latestBronze ? `v${data.latestBronze.version}` : t('pipeline:overview.none')}
          hint={data?.latestBronze ? t('pipeline:overview.bronzeHint', {
            bom: formatNumber(data.latestBronze.swingBomRows),
            items: formatNumber(data.latestBronze.swingItems),
            mappings: formatNumber(data.latestBronze.mappingRows),
          }) : null}
        />
        <StatCard
          label={t('pipeline:overview.reference')}
          value={data?.latestReference ? `bs-${data.latestReference.version}` : t('pipeline:overview.none')}
          hint={data?.latestReference ? formatDateTime(data.latestReference.uploadedAt) : t('pipeline:overview.referenceMissing')}
        />
        <StatCard
          label={t('pipeline:overview.latestRun')}
          value={data?.latestRun ? <StatusBadge status={data.latestRun.status} label={t(`pipeline:status.${data.latestRun.status}`)} /> : t('pipeline:overview.none')}
          hint={data?.latestRun?.runId}
        />
        <StatCard
          label={t('pipeline:overview.activeRuns')}
          value={formatNumber(data?.activeRuns ?? 0)}
          hint={data?.latestCompletedAnalysis ? t('pipeline:overview.lastAnalysis', { date: formatDateTime(data.latestCompletedAnalysis.finishedAt) }) : null}
        />
      </SimpleGrid>
      {data?.analysis ? (
        <>
          <SimpleGrid cols={{ base: 2, sm: 3, lg: 6 }}>
            <StatCard label={t('pipeline:overview.items')} value={formatNumber(data.analysis.items)} hint={data.analysis.runId} />
            <StatCard label={t('pipeline:readiness.READY')} value={formatNumber(data.analysis.boms.ready)} hint={t('pipeline:overview.boms')} />
            <StatCard label={t('pipeline:readiness.DECISION')} value={formatNumber(data.analysis.boms.decision)} hint={t('pipeline:overview.boms')} />
            <StatCard label={t('pipeline:readiness.REVIEW')} value={formatNumber(data.analysis.boms.review)} hint={t('pipeline:overview.boms')} />
            <StatCard label={t('pipeline:readiness.BLOCKED')} value={formatNumber(data.analysis.boms.blocked)} hint={t('pipeline:overview.boms')} />
            <StatCard label={t('pipeline:readiness.EXPORTED')} value={formatNumber(data.analysis.boms.exported)} hint={t('pipeline:overview.boms')} />
          </SimpleGrid>
          <Paper withBorder p="md">
            <Stack>
              <Group justify="space-between">
                <Title order={5}>{t('pipeline:overview.topBlocking')}</Title>
                <Group gap="md">
                  <Anchor component={Link} to="/issues" size="sm">{t('pipeline:overview.openGroups', { count: data.analysis.openIssueGroups })}</Anchor>
                  <Anchor component={Link} to="/type-changes" size="sm">{t('pipeline:overview.pendingDecisions', { count: data.analysis.pendingTypeDecisions })}</Anchor>
                </Group>
              </Group>
              <IssueGroupTable groups={data.analysis.topBlockingGroups} loading={false} height={240} onOpen={() => navigate('/issues')} />
            </Stack>
          </Paper>
        </>
      ) : data?.latestCompletedAnalysis ? (
        <Alert color="nordBlue">{t('pipeline:overview.noClassModel')}</Alert>
      ) : null}
      {auth.can('Analyst') ? (
        <Paper withBorder p="md">
          <Stack>
            <Title order={5}>{t('pipeline:launcher.title')}</Title>
            <RunLauncher
              canAnalyse={auth.can('Analyst')}
              canFullSync={auth.can('Admin')}
              busy={sync.isPending || analysis.isPending}
              onSync={(full, request) => sync.mutate({ full, request })}
              onAnalysis={(request) => analysis.mutate(request)}
            />
            {mutationError ? <Alert color="nordRed">{mutationError.message}</Alert> : null}
          </Stack>
        </Paper>
      ) : null}
      <Paper withBorder p="md">
        <Stack>
          <Title order={5}>{t('pipeline:overview.swingIssues')}</Title>
          <SourceIssueTable issues={data?.swingIssues ?? []} loading={overview.isLoading} />
        </Stack>
      </Paper>
    </Stack>
  )
}
