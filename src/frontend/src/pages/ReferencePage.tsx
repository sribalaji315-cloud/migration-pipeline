import { Alert, Paper, Stack, Title } from '@mantine/core'
import { useTranslation } from 'react-i18next'
import { ReferenceUploadForm, ReferenceVersionTable } from '../components/domain/ReferencePanels'
import { HelpDrawer } from '../components/ui/HelpDrawer'
import { PageHeader } from '../components/ui/PageHeader'
import { useAuth } from '../hooks/useAuth'
import { useReferenceVersions, useUploadReference } from '../hooks/useReference'
import { useHelpSections } from '../hooks/useHelpSections'

export function ReferencePage() {
  const { t } = useTranslation(['pipeline', 'common', 'help'])
  const auth = useAuth()
  const versions = useReferenceVersions()
  const upload = useUploadReference()
  const help = useHelpSections('reference')

  return (
    <Stack>
      <PageHeader title={t('pipeline:reference.title')} actions={<HelpDrawer buttonLabel={t('common:actions.help')} title={t('help:reference.title')} sections={help} />} />
      {auth.can('Admin') ? (
        <Paper withBorder p="md">
          <Stack>
            <Title order={5}>{t('pipeline:reference.uploadTitle')}</Title>
            <ReferenceUploadForm submitting={upload.isPending} errorMessage={upload.error?.message} onSubmit={(files) => upload.mutate(files)} />
            {upload.isSuccess ? <Alert color="nordGreen">{t('pipeline:reference.uploaded', { version: upload.data.version })}</Alert> : null}
          </Stack>
        </Paper>
      ) : null}
      <Paper withBorder p="md">
        <Stack>
          <Title order={5}>{t('pipeline:reference.versions')}</Title>
          <ReferenceVersionTable versions={versions.data ?? []} loading={versions.isLoading} />
        </Stack>
      </Paper>
    </Stack>
  )
}
