import { Paper, Stack, Text } from '@mantine/core'
import type { ReactNode } from 'react'

type StatCardProps = {
  label: string
  value: ReactNode
  hint?: ReactNode
}

export function StatCard({ label, value, hint }: StatCardProps) {
  return (
    <Paper withBorder p="md" radius="sm">
      <Stack gap={4}>
        <Text size="sm" c="dimmed">{label}</Text>
        <Text component="div" className="stat-card-value">{value}</Text>
        {hint ? <Text component="div" size="xs" c="dimmed">{hint}</Text> : null}
      </Stack>
    </Paper>
  )
}
