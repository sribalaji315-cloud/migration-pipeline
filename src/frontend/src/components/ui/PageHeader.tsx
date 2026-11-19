import { Group, Title } from '@mantine/core'
import type { ReactNode } from 'react'

type PageHeaderProps = {
  title: string
  actions?: ReactNode
}

export function PageHeader({ title, actions }: PageHeaderProps) {
  return (
    <Group justify="space-between" align="center">
      <Title order={3}>{title}</Title>
      {actions ? <Group gap="xs">{actions}</Group> : null}
    </Group>
  )
}
