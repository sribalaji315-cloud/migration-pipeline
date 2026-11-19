import { Alert, Group, Stack, Text, Tree, useTree, type TreeNodeData } from '@mantine/core'
import { ChevronRight } from 'lucide-react'
import { useMemo } from 'react'
import { useTranslation } from 'react-i18next'
import type { BomTree as BomTreeData, BomTreeNode } from '../../types/pipeline'
import { StatusBadge } from '../ui/StatusBadge'

const MAX_RENDERED = 2000
const SEPARATOR = '\u001f'

/** Builds the explosion as a tree; a shared sub-assembly appears under every parent (node values are paths). */
function buildTree(tree: BomTreeData) {
  const children = new Map<string, string[]>()
  for (const edge of tree.edges) {
    children.set(edge.parentItemId, [...(children.get(edge.parentItemId) ?? []), edge.childItemId])
  }

  let rendered = 0
  const visit = (itemId: string, path: string): TreeNodeData => {
    rendered += 1
    const value = path ? `${path}${SEPARATOR}${itemId}` : itemId
    const kids = rendered < MAX_RENDERED ? (children.get(itemId) ?? []).sort() : []
    return { value, label: itemId, children: kids.filter(() => rendered < MAX_RENDERED).map((child) => visit(child, value)) }
  }

  return { data: [visit(tree.topItemId, '')], capped: rendered >= MAX_RENDERED }
}

export function BomTree({ tree }: { tree: BomTreeData }) {
  const { t } = useTranslation('pipeline')
  const nodes = useMemo(() => new Map<string, BomTreeNode>(tree.nodes.map((n) => [n.itemId, n])), [tree])
  const { data, capped } = useMemo(() => buildTree(tree), [tree])
  const controller = useTree({ initialExpandedState: { [tree.topItemId]: true } })

  return (
    <Stack gap="xs">
      {tree.truncated || capped ? <Alert color="nordAmber">{t('waves.treeTruncated', { total: tree.totalItems })}</Alert> : null}
      <Tree
        data={data}
        tree={controller}
        levelOffset={22}
        renderNode={({ node, expanded, hasChildren, elementProps }) => {
          const info = nodes.get(String(node.label))
          return (
            <Group gap={6} wrap="nowrap" {...elementProps} py={2}>
              {hasChildren
                ? <ChevronRight size={14} style={{ transform: expanded ? 'rotate(90deg)' : 'none' }} />
                : <span style={{ width: 14 }} />}
              <Text size="sm" fw={600}>{node.label}</Text>
              {info?.description ? <Text size="sm" c="dimmed" truncate>{info.description}</Text> : null}
              {info ? <StatusBadge status={info.status} label={t(`readiness.${info.status}`, { defaultValue: info.status })} /> : null}
              {info?.typeChanged ? <StatusBadge status="Approve" label={t('waves.typeChanged')} /> : null}
            </Group>
          )
        }}
      />
    </Stack>
  )
}
