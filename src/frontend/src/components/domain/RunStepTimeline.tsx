import { Alert, Group, Stack, Text, Timeline } from '@mantine/core'
import { useTranslation } from 'react-i18next'
import { formatNumber } from '../../lib/format'
import type { Run } from '../../types/pipeline'
import { StatusBadge } from '../ui/StatusBadge'

export function RunStepTimeline({ run }: { run: Run }) {
  const { t } = useTranslation('pipeline')
  const done = run.steps.filter((s) => s.status === 'Completed').length

  return (
    <Stack>
      <Group gap="xs">
        <Text fw={700}>{run.runId}</Text>
        <StatusBadge status={run.status} label={t(`status.${run.status}`)} />
      </Group>
      {run.errorMessage ? <Alert color="nordRed" title={t('runs.error')}>{run.errorMessage}</Alert> : null}
      {run.steps.length === 0 ? <Text size="sm" c="dimmed">{t('runs.noSteps')}</Text> : null}
      <Timeline active={done - 1} bulletSize={18} lineWidth={2}>
        {run.steps.map((step) => (
          <Timeline.Item key={step.sequence} title={step.step}>
            <Group gap="xs">
              <StatusBadge status={step.status} label={t(`status.${step.status}`)} />
              <Text size="xs" c="dimmed">
                {t('runs.stepStats', { rows: formatNumber(step.rowsOut), seconds: step.durationSeconds.toFixed(1) })}
              </Text>
            </Group>
            <Text size="xs" c="dimmed">{step.outputTables}</Text>
            {step.errorMessage ? <Text size="xs" c="nordRed">{step.errorMessage}</Text> : null}
          </Timeline.Item>
        ))}
      </Timeline>
    </Stack>
  )
}
