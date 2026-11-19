import { Stack } from '@mantine/core'
import { useTranslation } from 'react-i18next'
import { MigrationToolImportPanel } from '../components/domain/MigrationToolImportPanel'
import { HelpDrawer } from '../components/ui/HelpDrawer'
import { PageHeader } from '../components/ui/PageHeader'
import { useHelpSections } from '../hooks/useHelpSections'
import { useImportMigrationTool, useMigrationToolSource, useTestMigrationToolConnection } from '../hooks/useSources'

/** Admin-only (route guarded in App.tsx). */
export function MigrationToolPage() {
  const { t } = useTranslation(['pipeline', 'common', 'help'])
  const source = useMigrationToolSource(true)
  const importMt = useImportMigrationTool()
  const testConnection = useTestMigrationToolConnection()
  const help = useHelpSections('migrationTool')

  return (
    <Stack>
      <PageHeader title={t('pipeline:mtImport.pageTitle')} actions={<HelpDrawer buttonLabel={t('common:actions.help')} title={t('help:migrationTool.title')} sections={help} />} />
      <MigrationToolImportPanel
        source={source.data}
        loading={source.isLoading}
        errorMessage={source.error?.message}
        importing={importMt.isPending}
        importError={importMt.error?.message}
        testing={testConnection.isPending}
        testResult={testConnection.data}
        onImport={(full, request) => importMt.mutate({ full, request })}
        onTest={() => testConnection.mutate()}
      />
    </Stack>
  )
}
