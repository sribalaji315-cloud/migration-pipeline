import { Download } from 'lucide-react'
import type { ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
import { formatDateTime, formatNumber } from '../../lib/format'
import type { HealthCheck, MigrationToolSource, RunRequest } from '../../types/pipeline'
import { SourceImportPanel } from './SourceImportPanel'

type MigrationToolBomImportPanelProps = {
  source: MigrationToolSource | undefined
  loading: boolean
  errorMessage?: string
  importing: boolean
  importError?: string
  testing: boolean
  testResult: HealthCheck | undefined
  selectedCount: number
  scopeValid: boolean
  onImport: (request: RunRequest) => void
  onTest: () => void
  selector: ReactNode
}

/** Migration Tool import limited to the selected BOMs (top + all descendants); other items keep their mappings. */
export function MigrationToolBomImportPanel({ source, selectedCount, scopeValid, onImport, selector, ...rest }: MigrationToolBomImportPanelProps) {
  const { t } = useTranslation('pipeline')
  const snapshot = source?.latestSnapshot
  const lastScope = snapshot?.mtScopeTopBoms
    ? t('bomImport.lastScope', { boms: snapshot.mtScopeTopBoms.length, items: formatNumber(snapshot.mtScopeItems ?? 0) })
    : t('sourceImport.notApplicable')

  return (
    <SourceImportPanel
      {...rest}
      title={t('bomImport.title')}
      description={t('bomImport.description')}
      testDataBadge={source?.mode === 'Fixture' ? t('mtImport.fixtureBadge') : null}
      status={source}
      facts={[
        [t('mtImport.baseUrl'), source?.baseUrl ?? ''],
        [t('sourceImport.snapshot'), snapshot ? `v${snapshot.version}` : t('sourceImport.never')],
        [t('mtImport.mappingRows'), snapshot ? formatNumber(snapshot.mappingRows) : ''],
        [t('mtImport.itemHeaders'), snapshot ? formatNumber(snapshot.mtItems) : ''],
        [t('bomImport.lastScopeLabel'), lastScope],
        [t('mtImport.lastChange'), formatDateTime(snapshot?.maxUpdatedAt)],
      ]}
      actions={[{
        key: 'import-boms',
        label: t('bomImport.import', { count: selectedCount }),
        icon: <Download size={16} />,
        primary: true,
        disabled: selectedCount === 0 || !scopeValid,
        onClick: onImport,
      }]}
    >
      {selector}
    </SourceImportPanel>
  )
}
