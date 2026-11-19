import { useState, type FormEvent } from 'react'
import { Alert, Button, Group, Paper, PasswordInput, Stack, TextInput, Title } from '@mantine/core'
import { LogIn } from 'lucide-react'
import { useLocation, useNavigate } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { useAuth } from '../hooks/useAuth'

type LocationState = { from?: { pathname?: string } }

export function LoginPage() {
  const { t } = useTranslation(['common', 'errors'])
  const auth = useAuth()
  const navigate = useNavigate()
  const location = useLocation()
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [submitting, setSubmitting] = useState(false)
  const from = (location.state as LocationState | null)?.from?.pathname ?? '/'

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setError(null)
    setSubmitting(true)
    try {
      await auth.login(email, password)
      navigate(from, { replace: true })
    } catch (err) {
      setError(err instanceof Error ? err.message : t('errors:generic'))
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <Stack className="login-shell" justify="center">
      <Paper withBorder p="lg" radius="sm">
        <form onSubmit={submit}>
          <Stack>
            <Title order={2}>{t('auth.signInTitle')}</Title>
            <TextInput label={t('fields.email')} value={email} onChange={(e) => setEmail(e.currentTarget.value)} autoComplete="email" required />
            <PasswordInput label={t('fields.password')} value={password} onChange={(e) => setPassword(e.currentTarget.value)} autoComplete="current-password" required />
            {error ? <Alert color="nordRed">{error}</Alert> : null}
            <Group justify="flex-end">
              <Button type="submit" leftSection={<LogIn size={16} />} loading={submitting}>{t('actions.login')}</Button>
            </Group>
          </Stack>
        </form>
      </Paper>
    </Stack>
  )
}
