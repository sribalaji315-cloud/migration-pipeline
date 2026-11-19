import { Alert, Anchor, Badge, Button, Group, Paper, Select, SimpleGrid, Stack, Table, Text, Title } from '@mantine/core'
import { Download, RefreshCw, Upload } from 'lucide-react'
import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link, useParams } from 'react-router-dom'
import { downloadWaveFile } from '../api/waves'
import { IssueGroupTable } from '../components/domain/IssueGroupTable'
import { WaveGatePanel } from '../components/domain/WaveGatePanel'
import { HelpDrawer } from '../components/ui/HelpDrawer'
import { PageHeader } from '../components/ui/PageHeader'
import { StatCard } from '../components/ui/StatCard'
import { StatusBadge } from '../components/ui/StatusBadge'
import { useAuth } from '../hooks/useAuth'
import { useHelpSections } from '../hooks/useHelpSections'
import { useIssueGroups } from '../hooks/useIssueGroups'
import { isActive, useRun } from '../hooks/useRuns'
import { useExportWave, useInvalidateWaveData, useRefreshWave, useWave, useWaveFiles, useWaveGate } from '../hooks/useWaves'
import { formatDateTime, formatNumber } from '../lib/format'

export function WaveDetailPage() {
  const { waveId = '' } = useParams()
  const { t } = useTranslation(['pipeline', 'common', 'help'])
  const auth = useAuth()
  const help = useHelpSections('waves')
  const wave = useWave(waveId)
  const gate = useWaveGate(waveId)
  const files = useWaveFiles(waveId)
  const refresh = useRefreshWave()
  const exportWave = useExportWave()
  const invalidate = useInvalidateWaveData()
  const [exportRunId, setExportRunId] = useState<string | null>(null)
  const exportRun = useRun(exportRunId)
  const [bom, setBom] = useState<string | null>(null)
  const [downloadError, setDownloadError] = useState<string | null>(null)
  const tops = wave.data?.topBoms ?? []
  const issueBom = bom ?? tops[0] ?? ''
  const issues = useIssueGroups({ severity: null, fixSource: null, status: 'Open', q: '', bom: issueBom }, 200, 0)
  const exporting = exportWave.isPending || isActive(exportRun.data)

  const exportStatus = exportRun.data?.status
  useEffect(() => {
    if (exportStatus === 'Completed' || exportStatus === 'Failed') {
      void invalidate()
    }
  }, [exportStatus]) // eslint-disable-line react-hooks/exhaustive-deps

  // Evaluating the gate updates the wave status (Draft → Ready/Blocked) on the server.
  const gateUpdatedAt = gate.dataUpdatedAt
  useEffect(() => {
    if (gateUpdatedAt) {
      void wave.refetch()
    }
  }, [gateUpdatedAt]) // eslint-disable-line react-hooks/exhaustive-deps

  async function download(file: string) {
    setDownloadError(null)
    try {
      await downloadWaveFile(waveId, file)
    } catch (error) {
      setDownloadError((error as Error).message)
    }
  }

  if (wave.error) {
    return <Alert color="nordRed">{wave.error.message}</Alert>
  }

  const w = wave.data
  return (
    <Stack>
      <PageHeader
        title={w ? `${w.waveId} · ${w.name}` : waveId}
        actions={(
          <>
            {w ? <StatusBadge status={w.status} label={t(`pipeline:waves.statuses.${w.status}`)} /> : null}
            <HelpDrawer buttonLabel={t('common:actions.help')} title={t('help:waves.title')} sections={help} />
          </>
        )}
      />
      <Anchor component={Link} to="/waves" size="sm">{t('pipeline:waveDetail.back')}</Anchor>
      {w ? (
        <SimpleGrid cols={{ base: 1, sm: 2, lg: 4 }}>
          <StatCard label={t('pipeline:waves.items')} value={formatNumber(w.items)} hint={t('pipeline:waveDetail.tops', { tops: w.topBoms.join(', ') })} />
          <StatCard label={t('pipeline:waves.pins')} value={`v${w.bronzeVersion} · bs-${w.referenceVersion}`} hint={t('pipeline:waveDetail.effective', { date: w.effectiveDate })} />
          <StatCard label={t('pipeline:waves.createdAt')} value={formatDateTime(w.createdAt)} hint={w.createdByUserId} />
          <StatCard label={t('pipeline:waveDetail.lastExport')} value={w.lastExportRunId ?? t('pipeline:waveDetail.never')} />
        </SimpleGrid>
      ) : null}
      {gate.error ? <Alert color="nordAmber">{gate.error.message}</Alert> : null}
      {gate.data && w ? <WaveGatePanel gate={gate.data} wave={w} /> : null}
      <Group>
        {auth.can('Analyst') ? (
          <Button variant="light" leftSection={<RefreshCw size={16} />} loading={refresh.isPending} onClick={() => refresh.mutate(waveId)}>
            {t('pipeline:waveDetail.refresh')}
          </Button>
        ) : null}
        {auth.can('Approver') ? (
          <Button
            leftSection={<Upload size={16} />}
            disabled={!gate.data?.canExport || exporting}
            loading={exporting}
            onClick={() => exportWave.mutate(waveId, { onSuccess: (run) => setExportRunId(run.runId) })}
          >
            {w?.status === 'Exported' ? t('pipeline:waveDetail.reexport') : t('pipeline:waveDetail.export')}
          </Button>
        ) : null}
      </Group>
      {refresh.error ? <Alert color="nordRed">{refresh.error.message}</Alert> : null}
      {exportWave.error ? <Alert color="nordRed">{exportWave.error.message}</Alert> : null}
      {exportRun.data ? (
        <Alert color={exportRun.data.status === 'Failed' ? 'nordRed' : exportRun.data.status === 'Completed' ? 'nordGreen' : 'nordBlue'}>
          {t('pipeline:waveDetail.exportRun', { runId: exportRun.data.runId, status: t(`pipeline:status.${exportRun.data.status}`) })}
          {exportRun.data.errorMessage ? ` ${exportRun.data.errorMessage}` : ''}
        </Alert>
      ) : null}
      <Paper withBorder p="md">
        <Stack gap="sm">
          <Title order={5}>{t('pipeline:waveDetail.files')}</Title>
          {downloadError ? <Alert color="nordRed">{downloadError}</Alert> : null}
          {(files.data ?? []).length === 0 ? <Text size="sm" c="dimmed">{t('pipeline:waveDetail.noFiles')}</Text> : (
            <Table striped>
              <Table.Tbody>
                {files.data!.map((f) => (
                  <Table.Tr key={f.name}>
                    <Table.Td><Text size="sm" ff="monospace">{f.name}</Text></Table.Td>
                    <Table.Td><Badge variant="light" color="gray">{formatNumber(Math.ceil(f.size / 1024))} KB</Badge></Table.Td>
                    <Table.Td>
                      <Button size="xs" variant="subtle" leftSection={<Download size={14} />} onClick={() => void download(f.name)}>
                        {t('pipeline:waveDetail.download')}
                      </Button>
                    </Table.Td>
                  </Table.Tr>
                ))}
              </Table.Tbody>
            </Table>
          )}
        </Stack>
      </Paper>
      <Paper withBorder p="md">
        <Stack gap="sm">
          <Group justify="space-between" align="flex-end">
            <Title order={5}>{t('pipeline:waveDetail.issues')}</Title>
            {tops.length > 1 ? (
              <Select data={tops} value={issueBom} onChange={setBom} allowDeselect={false} w={220} aria-label={t('pipeline:waves.topBom')} />
            ) : null}
          </Group>
          <IssueGroupTable groups={issues.data?.items ?? []} loading={issues.isFetching} errorMessage={issues.error?.message} height={300} />
        </Stack>
      </Paper>
    </Stack>
  )
}
