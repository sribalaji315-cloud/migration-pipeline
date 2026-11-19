import { Alert, Group, List, Paper, Stack, Text, ThemeIcon, Title } from '@mantine/core'
import { Check, X } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { formatNumber } from '../../lib/format'
import type { Wave, WaveGate } from '../../types/pipeline'

/** Gate checks of a wave; export is only possible when every check passes. */
export function WaveGatePanel({ gate, wave }: { gate: WaveGate; wave: Wave }) {
  const { t } = useTranslation('pipeline')
  const stale = gate.checks.some((c) => c.name === 'SourceUnchanged' && !c.passed)

  return (
    <Paper withBorder p="md">
      <Stack gap="sm">
        <Group justify="space-between">
          <Title order={5}>{t('waveDetail.gateTitle')}</Title>
          <Text size="sm" c="dimmed">{t('waveDetail.gateRun', { runId: gate.analysisRunId })}</Text>
        </Group>
        <List spacing="xs" center>
          {gate.checks.map((check) => (
            <List.Item
              key={check.name}
              icon={(
                <ThemeIcon color={check.passed ? 'nordGreen' : 'nordRed'} size={22} radius="xl">
                  {check.passed ? <Check size={14} /> : <X size={14} />}
                </ThemeIcon>
              )}
            >
              <Text size="sm" fw={600} span>{t(`gate.${check.name}.label`)}</Text>
              <Text size="sm" c="dimmed" span>
                {' — '}
                {check.passed ? t(`gate.${check.name}.passed`) : t(`gate.${check.name}.failed`, { count: check.count })}
              </Text>
            </List.Item>
          ))}
        </List>
        {stale ? (
          <Alert color="nordAmber">
            {t('waveDetail.stale', {
              bronze: wave.bronzeVersion,
              reference: wave.referenceVersion,
              latestBronze: gate.latestBronzeVersion ?? '–',
              latestReference: gate.latestReferenceVersion ?? '–',
            })}
          </Alert>
        ) : null}
        {gate.alreadyExportedReferenced > 0 ? (
          <Text size="sm" c="dimmed">{t('waveDetail.alreadyExported', { count: gate.alreadyExportedReferenced, items: formatNumber(gate.items) })}</Text>
        ) : null}
      </Stack>
    </Paper>
  )
}
