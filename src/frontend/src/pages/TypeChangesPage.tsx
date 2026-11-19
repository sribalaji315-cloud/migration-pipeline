import { Alert, Button, Group, Modal, Pagination, Paper, Select, Stack, Text, TextInput, Textarea } from '@mantine/core'
import { useDebouncedValue } from '@mantine/hooks'
import { Check, Search, X } from 'lucide-react'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { TypeChangeTable } from '../components/domain/TypeChangeTable'
import { HelpDrawer } from '../components/ui/HelpDrawer'
import { PageHeader } from '../components/ui/PageHeader'
import { useAuth } from '../hooks/useAuth'
import { useHelpSections } from '../hooks/useHelpSections'
import { useDecideTypeChanges, useTypeChanges } from '../hooks/useTypeChanges'
import type { TypeDecisionStatus } from '../types/pipeline'

const PAGE_SIZE = 100
const decisions: TypeDecisionStatus[] = ['Pending', 'Approved', 'Rejected']

export function TypeChangesPage() {
  const { t } = useTranslation(['pipeline', 'common', 'help'])
  const auth = useAuth()
  const canDecide = auth.can('Approver')
  const help = useHelpSections('typeChanges')
  const [search, setSearch] = useState('')
  const [debounced] = useDebouncedValue(search.trim(), 300)
  const [decision, setDecision] = useState<TypeDecisionStatus | null>('Pending')
  const [page, setPage] = useState(1)
  const [selected, setSelected] = useState<string[]>([])
  const [pending, setPending] = useState<TypeDecisionStatus | null>(null)
  const [note, setNote] = useState('')
  const changes = useTypeChanges(debounced, decision, PAGE_SIZE, (page - 1) * PAGE_SIZE)
  const decide = useDecideTypeChanges()
  const totalPages = Math.max(1, Math.ceil((changes.data?.total ?? 0) / PAGE_SIZE))

  function toggle(id: string) {
    setSelected((current) => (current.includes(id) ? current.filter((x) => x !== id) : [...current, id].sort()))
  }

  function confirm() {
    if (!pending) {
      return
    }

    decide.mutate({ itemIds: selected, decision: pending, note: note.trim() || null }, {
      onSuccess: () => {
        setSelected([])
        setPending(null)
        setNote('')
      },
    })
  }

  return (
    <Stack>
      <PageHeader title={t('pipeline:typeChanges.title')} actions={<HelpDrawer buttonLabel={t('common:actions.help')} title={t('help:typeChanges.title')} sections={help} />} />
      <Paper withBorder p="md">
        <Stack gap="sm">
          <Text size="sm" c="dimmed">{t('pipeline:typeChanges.description')}</Text>
          <Group align="flex-end" justify="space-between">
            <Group align="flex-end">
              <TextInput label={t('common:actions.search')} leftSection={<Search size={16} />} value={search} onChange={(e) => { setSearch(e.currentTarget.value); setPage(1) }} w={260} />
              <Select
                label={t('pipeline:typeChanges.decision')}
                placeholder={t('pipeline:typeChanges.all')}
                data={decisions.map((d) => ({ value: d, label: t(`pipeline:typeChanges.decisions.${d}`) }))}
                value={decision}
                onChange={(v) => { setDecision(v as TypeDecisionStatus | null); setPage(1); setSelected([]) }}
                clearable
                w={180}
              />
            </Group>
            {canDecide ? (
              <Group>
                <Button color="nordGreen" leftSection={<Check size={16} />} disabled={selected.length === 0} onClick={() => setPending('Approved')}>
                  {t('pipeline:typeChanges.approve', { count: selected.length })}
                </Button>
                <Button color="nordRed" variant="light" leftSection={<X size={16} />} disabled={selected.length === 0} onClick={() => setPending('Rejected')}>
                  {t('pipeline:typeChanges.reject', { count: selected.length })}
                </Button>
              </Group>
            ) : null}
          </Group>
          {decide.data && decide.data.notFound.length > 0 ? (
            <Alert color="nordAmber">{t('pipeline:typeChanges.notFound', { ids: decide.data.notFound.join(', ') })}</Alert>
          ) : null}
          <TypeChangeTable
            rows={changes.data?.items ?? []}
            loading={changes.isFetching}
            errorMessage={changes.error?.message}
            selected={selected}
            selectable={canDecide}
            onToggle={toggle}
          />
          <Group justify="space-between">
            <Text size="sm" c="dimmed">{t('pipeline:typeChanges.total', { count: changes.data?.total ?? 0 })}</Text>
            <Pagination total={totalPages} value={page} onChange={setPage} size="sm" />
          </Group>
        </Stack>
      </Paper>
      <Modal opened={Boolean(pending)} onClose={() => setPending(null)} title={pending ? t(`pipeline:typeChanges.confirm${pending}`, { count: selected.length }) : ''}>
        <Stack>
          <Text size="sm">{t('pipeline:typeChanges.confirmText')}</Text>
          <Textarea
            label={t('pipeline:typeChanges.note')}
            description={pending === 'Rejected' ? t('pipeline:typeChanges.noteRequired') : undefined}
            value={note}
            onChange={(e) => setNote(e.currentTarget.value)}
            autosize
            minRows={2}
          />
          {decide.error ? <Alert color="nordRed">{decide.error.message}</Alert> : null}
          <Group justify="flex-end">
            <Button variant="default" onClick={() => setPending(null)}>{t('pipeline:typeChanges.cancel')}</Button>
            <Button
              color={pending === 'Rejected' ? 'nordRed' : 'nordGreen'}
              loading={decide.isPending}
              disabled={pending === 'Rejected' && !note.trim()}
              onClick={confirm}
            >
              {t('pipeline:typeChanges.confirm')}
            </Button>
          </Group>
        </Stack>
      </Modal>
    </Stack>
  )
}
