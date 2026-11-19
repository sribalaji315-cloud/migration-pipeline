import { Badge } from '@mantine/core'
import { statusColor } from '../../theme/theme'

type StatusBadgeProps = {
  status: string
  label?: string
}

export function StatusBadge({ status, label }: StatusBadgeProps) {
  return (
    <Badge color={statusColor[status] ?? 'gray'} variant="light">
      {label ?? status}
    </Badge>
  )
}
