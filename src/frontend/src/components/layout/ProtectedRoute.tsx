import { Navigate, useLocation } from 'react-router-dom'
import { Alert } from '@mantine/core'
import { useTranslation } from 'react-i18next'
import type { ReactNode } from 'react'
import { useAuth } from '../../hooks/useAuth'
import type { Role } from '../../types/pipeline'

export function ProtectedRoute({ children, minimumRole }: { children: ReactNode; minimumRole?: Role }) {
  const { t } = useTranslation('errors')
  const auth = useAuth()
  const location = useLocation()

  if (!auth.isAuthenticated) {
    return <Navigate to="/login" state={{ from: location }} replace />
  }

  if (minimumRole && !auth.can(minimumRole)) {
    return <Alert color="nordRed">{t('unauthorized')}</Alert>
  }

  return children
}
