import { Alert, Button, Group, Paper, Stack, TextInput, Title } from '@mantine/core'
import { useDebouncedValue } from '@mantine/hooks'
import { Search } from 'lucide-react'
import { useState, type FormEvent } from 'react'
import { useTranslation } from 'react-i18next'
import { BomSelector } from '../components/domain/BomSelector'
import { MigrationToolBomImportPanel } from '../components/domain/MigrationToolBomImportPanel'
import { SwingImportPanel } from '../components/domain/SwingImportPanel'
import { BomLineTable, ItemTypeCard } from '../components/domain/SwingItemPanels'
import { HelpDrawer } from '../components/ui/HelpDrawer'
import { PageHeader } from '../components/ui/PageHeader'
import { useAuth } from '../hooks/useAuth'
import { useBomLines, useBomList, useBomScopePreview, useItemType } from '../hooks/useItems'
import {
  useImportMigrationTool,
  useImportSwing,
  useMigrationToolSource,
  useSwingSource,
  useTestMigrationToolConnection,
  useTestSwingConnection,
} from '../hooks/useSources'
import { useHelpSections } from '../hooks/useHelpSections'

const BOM_PAGE_SIZE = 50

export function SwingPage() {
  const { t } = useTranslation(['pipeline', 'common', 'help'])
  const auth = useAuth()
  const isAdmin = auth.can('Admin')
  const [input, setInput] = useState('')
  const [itemId, setItemId] = useState('')
  const itemType = useItemType(itemId)
  const lines = useBomLines(itemId)
  const source = useSwingSource(isAdmin)
  const importSwing = useImportSwing()
  const testConnection = useTestSwingConnection()
  const help = useHelpSections('swing')

  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setItemId(input.trim())
  }

  function openChild(childId: string) {
    setInput(childId)
    setItemId(childId)
  }

  return (
    <Stack>
      <PageHeader title={t('pipeline:swing.title')} actions={<HelpDrawer buttonLabel={t('common:actions.help')} title={t('help:swing.title')} sections={help} />} />
      {isAdmin ? (
        <SwingImportPanel
          source={source.data}
          loading={source.isLoading}
          errorMessage={source.error?.message}
          importing={importSwing.isPending}
          importError={importSwing.error?.message}
          testing={testConnection.isPending}
          testResult={testConnection.data}
          onImport={(request) => importSwing.mutate(request)}
          onTest={() => testConnection.mutate()}
        />
      ) : null}
      {isAdmin ? <BomScopedImportSection /> : null}
      <Paper withBorder p="md">
        <form onSubmit={submit}>
          <Group align="flex-end">
            <TextInput label={t('pipeline:swing.itemId')} placeholder={t('pipeline:swing.itemIdPlaceholder')} value={input} onChange={(e) => setInput(e.currentTarget.value)} />
            <Button type="submit" leftSection={<Search size={16} />} disabled={!input.trim()}>{t('common:actions.search')}</Button>
          </Group>
        </form>
      </Paper>
      {itemId ? (
        <>
          {itemType.data ? <ItemTypeCard item={itemType.data} /> : null}
          {itemType.error ? <Alert color="nordAmber">{itemType.error.message}</Alert> : null}
          <Paper withBorder p="md">
            <Stack>
              <Title order={5}>{t('pipeline:swing.linesTitle', { itemId })}</Title>
              <BomLineTable lines={lines.data ?? []} loading={lines.isLoading} errorMessage={lines.error?.message} onOpenChild={openChild} />
            </Stack>
          </Paper>
        </>
      ) : null}
    </Stack>
  )
}

/** Admin: pick BOMs and import Migration Tool data only for their explosions. */
function BomScopedImportSection() {
  const [search, setSearch] = useState('')
  const [debouncedSearch] = useDebouncedValue(search.trim(), 300)
  const [topOnly, setTopOnly] = useState(true)
  const [page, setPage] = useState(1)
  const [selected, setSelected] = useState<string[]>([])
  const boms = useBomList(debouncedSearch, topOnly, BOM_PAGE_SIZE, (page - 1) * BOM_PAGE_SIZE, true)
  const preview = useBomScopePreview(selected)
  const source = useMigrationToolSource(true)
  const importMt = useImportMigrationTool()
  const testConnection = useTestMigrationToolConnection()

  function toggle(id: string) {
    setSelected((current) => (current.includes(id) ? current.filter((x) => x !== id) : [...current, id].sort()))
  }

  return (
    <MigrationToolBomImportPanel
      source={source.data}
      loading={source.isLoading}
      errorMessage={source.error?.message}
      importing={importMt.isPending}
      importError={importMt.error?.message}
      testing={testConnection.isPending}
      testResult={testConnection.data}
      selectedCount={selected.length}
      scopeValid={!preview.isFetching && (preview.data?.unknown.length ?? 0) === 0}
      onImport={(request) => importMt.mutate({ full: true, request, topBoms: selected })}
      onTest={() => testConnection.mutate()}
      selector={(
        <BomSelector
          search={search}
          onSearch={(value) => { setSearch(value); setPage(1) }}
          topOnly={topOnly}
          onTopOnly={(value) => { setTopOnly(value); setPage(1) }}
          page={page}
          pageSize={BOM_PAGE_SIZE}
          onPage={setPage}
          boms={boms.data}
          loading={boms.isFetching}
          errorMessage={boms.error?.message}
          selected={selected}
          onToggle={toggle}
          onClear={() => setSelected([])}
          preview={preview.data}
          previewLoading={preview.isFetching}
          previewError={preview.error?.message}
        />
      )}
    />
  )
}
