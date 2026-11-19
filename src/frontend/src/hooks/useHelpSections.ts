import { useTranslation } from 'react-i18next'

type HelpPage = 'overview' | 'runs' | 'swing' | 'migrationTool' | 'reference' | 'waves' | 'itemClass' | 'issues' | 'typeChanges'

/** Builds the role-agnostic help sections for a page from the `help` namespace. */
export function useHelpSections(page: HelpPage) {
  const { t } = useTranslation('help')
  const sections = t(`${page}.sections`, { returnObjects: true }) as Array<{ heading: string; body: string }>
  return Array.isArray(sections) ? sections : []
}
