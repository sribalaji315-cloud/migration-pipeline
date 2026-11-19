import { Alert, Button, Group, Paper, SimpleGrid, Stack, Text, Title } from '@mantine/core'
import { PlugZap } from 'lucide-react'
import { useState, type ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
import { defaultRunOptions, isRunOptionsValid, toRunRequest } from '../../lib/runOptions'
import type { HealthCheck, RunRequest, SourceStatus } from '../../types/pipeline'
import { StatusBadge } from '../ui/StatusBadge'
import { RunOptionsFields } from './RunOptionsFields'
import { RunStepTimeline } from './RunStepTimeline'

export type ImportAction = {
  key: string
  label: string
  icon: ReactNode
  primary?: boolean
  disabled?: boolean
  onClick: (request: RunRequest) => void
}

type SourceImportPanelProps = {
  title: string
  description: string
  testDataBadge: string | null
  facts: Array<[string, string]>
  status: SourceStatus | undefined
  loading: boolean
  errorMessage?: string
  actions: ImportAction[]
  importing: boolean
  importError?: string
  testing: boolean
  testResult: HealthCheck | undefined
  onTest: () => void
  /** Rendered between the facts and the run options (e.g. a BOM selector or a notice). */
  children?: ReactNode
}

/** Admin import panel shared by the Swing and Migration Tool sources. */
export function SourceImportPanel({
  title, description, testDataBadge, facts, status, loading, errorMessage, actions,
  importing, importError, testing, testResult, onTest, children,
}: SourceImportPanelProps) {
  const { t } = useTranslation('pipeline')
  const [options, setOptions] = useState(defaultRunOptions)
  const busy = importing || Boolean(status?.activeImport)
  const shownRun = status?.activeImport ?? status?.lastImport ?? null

  return (
    <Paper withBorder p="md">
      <Stack>
        <Group justify="space-between">
          <Title order={5}>{title}</Title>
          {testDataBadge ? <StatusBadge status="Warn" label={testDataBadge} /> : null}
        </Group>
        <Text size="sm" c="dimmed">{description}</Text>
        {errorMessage ? <Alert color="nordRed">{errorMessage}</Alert> : null}
        <SimpleGrid cols={{ base: 2, sm: 4 }}>
          {facts.map(([label, value]) => (
            <Stack gap={0} key={label}>
              <Text size="xs" c="dimmed">{label}</Text>
              <Text size="sm">{loading ? '…' : value}</Text>
            </Stack>
          ))}
        </SimpleGrid>
        {children}
        <RunOptionsFields value={options} onChange={setOptions} />
        <Group gap="xs">
          {actions.map((action) => (
            <Button
              key={action.key}
              variant={action.primary ? 'filled' : 'light'}
              leftSection={action.icon}
              loading={busy}
              disabled={action.disabled || !isRunOptionsValid(options) || loading}
              onClick={() => action.onClick(toRunRequest(options))}
            >
              {action.label}
            </Button>
          ))}
          <Button variant="light" leftSection={<PlugZap size={16} />} loading={testing} onClick={onTest}>
            {t('sourceImport.test')}
          </Button>
        </Group>
        {testResult ? (
          <Alert color={testResult.healthy ? 'nordGreen' : 'nordRed'} title={testResult.healthy ? t('sourceImport.testOk') : t('sourceImport.testFailed')}>
            {testResult.detail}
          </Alert>
        ) : null}
        {importError ? <Alert color="nordRed">{importError}</Alert> : null}
        {shownRun ? (
          <Paper withBorder p="sm">
            <Stack gap="xs">
              <Text size="sm" fw={600}>{status?.activeImport ? t('sourceImport.running') : t('sourceImport.lastRun')}</Text>
              <RunStepTimeline run={shownRun} />
            </Stack>
          </Paper>
        ) : null}
      </Stack>
    </Paper>
  )
}
