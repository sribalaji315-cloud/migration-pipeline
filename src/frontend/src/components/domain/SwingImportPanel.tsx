import { Download } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { formatDateTime, formatNumber, mtDataLabel } from '../../lib/format'
import type { HealthCheck, RunRequest, SwingSource } from '../../types/pipeline'
import { SourceImportPanel } from './SourceImportPanel'

type SwingImportPanelProps = {
  source: SwingSource | undefined
  loading: boolean
  errorMessage?: string
  importing: boolean
  importError?: string
  testing: boolean
  testResult: HealthCheck | undefined
  onImport: (request: RunRequest) => void
  onTest: () => void
}

export function SwingImportPanel({ source, onImport, ...rest }: SwingImportPanelProps) {
  const { t } = useTranslation('pipeline')
  const snapshot = source?.latestSnapshot
  const mtData = snapshot ? mtDataLabel(snapshot) : null

  return (
    <SourceImportPanel
      {...rest}
      title={t('swingImport.title')}
      description={t('swingImport.description')}
      testDataBadge={source?.mode === 'Fixture' ? t('swingImport.fixtureBadge') : null}
      status={source}
      facts={[
        [t('swingImport.mode'), source ? t(`swingImport.modes.${source.mode}`) : ''],
        [t('swingImport.dataSource'), source?.dataSource ?? ''],
        [t('swingImport.library'), source?.library || t('sourceImport.notApplicable')],
        [t('sourceImport.snapshot'), snapshot ? `v${snapshot.version}` : t('sourceImport.never')],
        [t('swingImport.bomRows'), snapshot ? formatNumber(snapshot.swingBomRows) : ''],
        [t('swingImport.itemRows'), snapshot ? formatNumber(snapshot.swingItems) : ''],
        [t('swingImport.extractedAt'), formatDateTime(snapshot?.swingBomExtractedAt)],
        [t('swingImport.mtData'), mtData ? t(mtData.key, mtData.params) : ''],
      ]}
      actions={[{ key: 'import', label: t('swingImport.import'), icon: <Download size={16} />, primary: true, onClick: onImport }]}
    />
  )
}
