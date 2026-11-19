import { Alert } from '@mantine/core'
import { Download, RotateCcw } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { formatDateTime, formatNumber } from '../../lib/format'
import type { HealthCheck, MigrationToolSource, RunRequest } from '../../types/pipeline'
import { SourceImportPanel } from './SourceImportPanel'

type MigrationToolImportPanelProps = {
  source: MigrationToolSource | undefined
  loading: boolean
  errorMessage?: string
  importing: boolean
  importError?: string
  testing: boolean
  testResult: HealthCheck | undefined
  onImport: (full: boolean, request: RunRequest) => void
  onTest: () => void
}

export function MigrationToolImportPanel({ source, onImport, ...rest }: MigrationToolImportPanelProps) {
  const { t } = useTranslation('pipeline')
  const snapshot = source?.latestSnapshot
  const auth = !source
    ? ''
    : source.authMode === 'BearerToken'
      ? (!source.tokenExpiresAt
        ? t('mtImport.auth.token')
        : new Date(source.tokenExpiresAt) <= new Date()
          ? t('mtImport.auth.tokenExpired', { date: formatDateTime(source.tokenExpiresAt) })
          : t('mtImport.auth.tokenValid', { date: formatDateTime(source.tokenExpiresAt) }))
      : source.authMode === 'Login'
        ? t('mtImport.auth.login', { user: source.user })
        : t('sourceImport.notApplicable')
  const mtData = !snapshot
    ? ''
    : snapshot.mtCarriedFromVersion
      ? t('swingImport.mtCarried', { version: snapshot.mtCarriedFromVersion })
      : snapshot.mappingRows === 0 && snapshot.mtItems === 0
        ? t('swingImport.mtNone')
        : t(`mtImport.modes.${snapshot.mode}`)

  return (
    <SourceImportPanel
      {...rest}
      title={t('mtImport.title')}
      description={t('mtImport.description')}
      testDataBadge={source?.mode === 'Fixture' ? t('mtImport.fixtureBadge') : null}
      status={source}
      facts={[
        [t('mtImport.mode'), source ? t(`mtImport.sources.${source.mode}`) : ''],
        [t('mtImport.baseUrl'), source?.baseUrl ?? ''],
        [t('mtImport.authentication'), auth],
        [t('mtImport.bulk'), source ? (source.bulkEndpoint ? t('mtImport.bulkOn') : t('mtImport.bulkOff')) : ''],
        [t('sourceImport.snapshot'), snapshot ? `v${snapshot.version}` : t('sourceImport.never')],
        [t('mtImport.mappingRows'), snapshot ? formatNumber(snapshot.mappingRows) : ''],
        [t('mtImport.itemHeaders'), snapshot ? formatNumber(snapshot.mtItems) : ''],
        [t('mtImport.lastChange'), formatDateTime(snapshot?.maxUpdatedAt)],
        [t('mtImport.data'), mtData],
        [t('mtImport.changedItems'), snapshot && !snapshot.mtCarriedFromVersion ? formatNumber(snapshot.changedItems) : ''],
        [t('mtImport.swingData'), snapshot
          ? (snapshot.swingCarriedFromVersion
            ? t('mtImport.swingCarried', { version: snapshot.swingCarriedFromVersion })
            : t('mtImport.swingFresh'))
          : ''],
      ]}
      actions={source && !source.bulkEndpoint ? [
        { key: 'full', label: t('mtImport.importAll'), icon: <Download size={16} />, primary: true, onClick: (r) => onImport(true, r) },
      ] : [
        { key: 'changes', label: t('mtImport.importChanges'), icon: <Download size={16} />, primary: true, onClick: (r) => onImport(false, r) },
        { key: 'full', label: t('mtImport.importFull'), icon: <RotateCcw size={16} />, onClick: (r) => onImport(true, r) },
      ]}
    >
      {source && !source.bulkEndpoint ? <Alert color="nordBlue">{t('mtImport.perItem')}</Alert> : null}
    </SourceImportPanel>
  )
}
