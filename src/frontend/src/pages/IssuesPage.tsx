import { Alert, Button, Drawer, Group, Pagination, Paper, Select, Stack, Tabs, Text, TextInput, Textarea, Title } from '@mantine/core'
import { useDebouncedValue } from '@mantine/hooks'
import { Search } from 'lucide-react'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { IssueGroupTable, IssueItemTable } from '../components/domain/IssueGroupTable'
import { HelpDrawer } from '../components/ui/HelpDrawer'
import { PageHeader } from '../components/ui/PageHeader'
import { StatusBadge } from '../components/ui/StatusBadge'
import { useAuth } from '../hooks/useAuth'
import { useHelpSections } from '../hooks/useHelpSections'
import { useAcknowledgeIssue, useIssueGroups, useIssueItems, useIssueSummary } from '../hooks/useIssueGroups'
import { formatNumber } from '../lib/format'
import { fixSources, type FixSource, type IssueGroup, type IssueStatus, type Severity } from '../types/pipeline'

const PAGE_SIZE = 50
const ITEM_PAGE_SIZE = 100
const severities: Severity[] = ['Block', 'Review', 'Approve', 'Warn', 'Info']
const statuses: IssueStatus[] = ['Open', 'Acknowledged', 'Resolved']

export function IssuesPage() {
  const { t } = useTranslation(['issues', 'common', 'help'])
  const help = useHelpSections('issues')
  const [tab, setTab] = useState<string>('All')
  const [severity, setSeverity] = useState<Severity | null>(null)
  const [status, setStatus] = useState<IssueStatus | null>(null)
  const [search, setSearch] = useState('')
  const [bom, setBom] = useState('')
  const [debouncedSearch] = useDebouncedValue(search.trim(), 300)
  const [debouncedBom] = useDebouncedValue(bom.trim(), 400)
  const [page, setPage] = useState(1)
  const [openGroup, setOpenGroup] = useState<IssueGroup | null>(null)
  const filters = { severity, fixSource: tab === 'All' ? null : (tab as FixSource), status, q: debouncedSearch, bom: debouncedBom }
  const groups = useIssueGroups(filters, PAGE_SIZE, (page - 1) * PAGE_SIZE)
  const summary = useIssueSummary()
  const totalPages = Math.max(1, Math.ceil((groups.data?.total ?? 0) / PAGE_SIZE))
  const countFor = (source: FixSource) => (summary.data ?? []).filter((s) => s.fixSource === source).reduce((n, s) => n + s.groups, 0)
  const tabsWithIssues = fixSources.filter((s) => countFor(s) > 0)
  const resetPage = () => setPage(1)

  return (
    <Stack>
      <PageHeader title={t('issues:page.title')} actions={<HelpDrawer buttonLabel={t('common:actions.help')} title={t('help:issues.title')} sections={help} />} />
      <Paper withBorder p="md">
        <Stack gap="sm">
          <Tabs value={tab} onChange={(v) => { setTab(v ?? 'All'); resetPage() }}>
            <Tabs.List>
              <Tabs.Tab value="All">{t('issues:page.allSources')}</Tabs.Tab>
              {tabsWithIssues.map((s) => (
                <Tabs.Tab key={s} value={s}>{`${t(`issues:fixSource.${s}`)} (${formatNumber(countFor(s))})`}</Tabs.Tab>
              ))}
            </Tabs.List>
          </Tabs>
          <Group align="flex-end">
            <TextInput
              label={t('issues:page.search')}
              leftSection={<Search size={16} />}
              value={search}
              onChange={(e) => { setSearch(e.currentTarget.value); resetPage() }}
              w={260}
            />
            <TextInput label={t('issues:page.bom')} placeholder={t('issues:page.bomPlaceholder')} value={bom} onChange={(e) => { setBom(e.currentTarget.value); resetPage() }} w={200} />
            <Select
              label={t('issues:columns.severity')}
              placeholder={t('issues:page.any')}
              data={severities.map((s) => ({ value: s, label: t(`issues:severity.${s}`) }))}
              value={severity}
              onChange={(v) => { setSeverity(v as Severity | null); resetPage() }}
              clearable
              w={160}
            />
            <Select
              label={t('issues:columns.status')}
              placeholder={t('issues:page.notResolved')}
              data={statuses.map((s) => ({ value: s, label: t(`issues:status.${s}`) }))}
              value={status}
              onChange={(v) => { setStatus(v as IssueStatus | null); resetPage() }}
              clearable
              w={180}
            />
          </Group>
          <IssueGroupTable groups={groups.data?.items ?? []} loading={groups.isFetching} errorMessage={groups.error?.message} onOpen={setOpenGroup} />
          <Group justify="space-between">
            <Text size="sm" c="dimmed">{t('issues:page.total', { count: groups.data?.total ?? 0 })}</Text>
            <Pagination total={totalPages} value={page} onChange={setPage} size="sm" />
          </Group>
        </Stack>
      </Paper>
      <Drawer opened={Boolean(openGroup)} onClose={() => setOpenGroup(null)} position="right" size="xl" title={openGroup?.issueType}>
        {openGroup ? <IssueGroupDetail group={openGroup} onChanged={setOpenGroup} /> : null}
      </Drawer>
    </Stack>
  )
}

function IssueGroupDetail({ group, onChanged }: { group: IssueGroup; onChanged: (group: IssueGroup) => void }) {
  const { t } = useTranslation('issues')
  const auth = useAuth()
  const [page, setPage] = useState(1)
  const [note, setNote] = useState('')
  const items = useIssueItems(group.id, ITEM_PAGE_SIZE, (page - 1) * ITEM_PAGE_SIZE)
  const acknowledge = useAcknowledgeIssue()
  const canAcknowledge = auth.can('Analyst') && group.status === 'Open' && (group.severity === 'Warn' || group.severity === 'Info')
  const totalPages = Math.max(1, Math.ceil((items.data?.total ?? 0) / ITEM_PAGE_SIZE))

  return (
    <Stack>
      <Group gap="xs">
        <StatusBadge status={group.severity} label={t(`severity.${group.severity}`)} />
        <StatusBadge status={group.status} label={t(`status.${group.status}`)} />
        <Text size="sm" c="dimmed">{group.groupKey}</Text>
      </Group>
      <Text size="sm">{t(`types.${group.issueType}`, { defaultValue: group.issueType })}</Text>
      <Paper withBorder p="sm">
        <Stack gap={4}>
          <Title order={6}>{t('detail.fixIn', { source: t(`fixSource.${group.fixSource}`) })}</Title>
          <Text size="sm">{group.fixAction}</Text>
          <Text size="xs" c="dimmed">{t('detail.fixTarget', { target: group.fixTarget })}</Text>
        </Stack>
      </Paper>
      <Text size="xs" c="dimmed">
        {t('detail.seen', { first: group.firstSeenRunId, last: group.lastSeenRunId, items: formatNumber(group.affectedItems), boms: formatNumber(group.affectedBoms) })}
      </Text>
      {group.note ? <Alert color="nordBlue">{t('detail.acknowledged', { user: group.acknowledgedByUserId, note: group.note })}</Alert> : null}
      <IssueItemTable items={items.data?.items ?? []} loading={items.isFetching} errorMessage={items.error?.message} />
      {totalPages > 1 ? <Pagination total={totalPages} value={page} onChange={setPage} size="sm" /> : null}
      {canAcknowledge ? (
        <Stack gap="xs">
          <Textarea label={t('detail.note')} value={note} onChange={(e) => setNote(e.currentTarget.value)} autosize minRows={2} />
          <Button
            w="fit-content"
            loading={acknowledge.isPending}
            onClick={() => acknowledge.mutate({ id: group.id, note: note.trim() || null }, { onSuccess: onChanged })}
          >
            {t('detail.acknowledge')}
          </Button>
          {acknowledge.error ? <Alert color="nordRed">{acknowledge.error.message}</Alert> : null}
        </Stack>
      ) : null}
    </Stack>
  )
}
