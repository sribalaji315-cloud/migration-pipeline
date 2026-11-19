import { Button, Group, Stack } from '@mantine/core'
import { Play, RefreshCw, RotateCcw } from 'lucide-react'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { defaultRunOptions, isRunOptionsValid, toRunRequest } from '../../lib/runOptions'
import type { RunRequest } from '../../types/pipeline'
import { RunOptionsFields } from './RunOptionsFields'

type RunLauncherProps = {
  canAnalyse: boolean
  canFullSync: boolean
  busy: boolean
  onSync: (full: boolean, request: RunRequest) => void
  onAnalysis: (request: RunRequest) => void
}

export function RunLauncher({ canAnalyse, canFullSync, busy, onSync, onAnalysis }: RunLauncherProps) {
  const { t } = useTranslation('pipeline')
  const [options, setOptions] = useState(defaultRunOptions)
  const invalid = !isRunOptionsValid(options)
  const request = toRunRequest(options)

  if (!canAnalyse) {
    return null
  }

  return (
    <Stack gap="sm">
      <RunOptionsFields value={options} onChange={setOptions} />
      <Group gap="xs">
        <Button leftSection={<RefreshCw size={16} />} loading={busy} disabled={invalid} onClick={() => onSync(false, request)}>
          {t('launcher.sync')}
        </Button>
        {canFullSync ? (
          <Button variant="light" leftSection={<RotateCcw size={16} />} loading={busy} disabled={invalid} onClick={() => onSync(true, request)}>
            {t('launcher.fullSync')}
          </Button>
        ) : null}
        <Button variant="light" leftSection={<Play size={16} />} loading={busy} disabled={invalid} onClick={() => onAnalysis(request)}>
          {t('launcher.analysis')}
        </Button>
      </Group>
    </Stack>
  )
}
