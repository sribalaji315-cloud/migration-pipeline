import { Button, Group, Menu, NavLink, Paper, Stack, Text, Title } from '@mantine/core'
import { ArrowLeftRight, Boxes, Check, ChevronDown, Database, FileSpreadsheet, GitCompareArrows, LayoutDashboard, ListTree, LogOut, Shapes, TriangleAlert, UserCog } from 'lucide-react'
import { NavLink as RouterNavLink, Outlet, useNavigate } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { useAuth } from '../../hooks/useAuth'

export function AppLayout() {
  const { t } = useTranslation('common')
  const auth = useAuth()
  const navigate = useNavigate()

  function handleLogout() {
    auth.logout()
    navigate('/login', { replace: true })
  }

  return (
    <Stack gap="lg" className="workspace-shell">
      <Paper withBorder p="md" radius="sm">
        <Group justify="space-between" align="center">
          <Title order={2}>{t('appName')}</Title>
          <Group>
            <Text size="sm">{t('status.signedInAs', { name: auth.user?.displayName || auth.user?.email })}</Text>
            <Menu shadow="md" width={180} position="bottom-end" disabled={auth.availableRoles.length < 2}>
              <Menu.Target>
                <Button variant="light" leftSection={<UserCog size={16} />} rightSection={<ChevronDown size={14} />} aria-label={t('actions.switchRole')}>
                  {auth.activeRole ? t(`roles.${auth.activeRole}`) : t('status.unknownRole')}
                </Button>
              </Menu.Target>
              <Menu.Dropdown>
                {auth.availableRoles.map((role) => (
                  <Menu.Item key={role} leftSection={role === auth.activeRole ? <Check size={16} /> : null} onClick={() => auth.switchRole(role)}>
                    {t(`roles.${role}`)}
                  </Menu.Item>
                ))}
              </Menu.Dropdown>
            </Menu>
            <Button variant="light" leftSection={<LogOut size={16} />} onClick={handleLogout}>{t('actions.logout')}</Button>
          </Group>
        </Group>
        <Group mt="md" gap="xs">
          <NavLink component={RouterNavLink} to="/" end label={t('navigation.overview')} leftSection={<LayoutDashboard size={16} />} className="app-nav-link" />
          <NavLink component={RouterNavLink} to="/waves" label={t('navigation.waves')} leftSection={<Boxes size={16} />} className="app-nav-link" />
          <NavLink component={RouterNavLink} to="/items" label={t('navigation.items')} leftSection={<Shapes size={16} />} className="app-nav-link" />
          <NavLink component={RouterNavLink} to="/issues" label={t('navigation.issues')} leftSection={<TriangleAlert size={16} />} className="app-nav-link" />
          <NavLink component={RouterNavLink} to="/type-changes" label={t('navigation.typeChanges')} leftSection={<GitCompareArrows size={16} />} className="app-nav-link" />
          <NavLink component={RouterNavLink} to="/swing" label={t('navigation.swing')} leftSection={<ListTree size={16} />} className="app-nav-link" />
          {auth.can('Admin') ? (
            <NavLink component={RouterNavLink} to="/migration-tool" label={t('navigation.migrationTool')} leftSection={<ArrowLeftRight size={16} />} className="app-nav-link" />
          ) : null}
          <NavLink component={RouterNavLink} to="/runs" label={t('navigation.runs')} leftSection={<Database size={16} />} className="app-nav-link" />
          <NavLink component={RouterNavLink} to="/reference" label={t('navigation.reference')} leftSection={<FileSpreadsheet size={16} />} className="app-nav-link" />
        </Group>
      </Paper>
      <Outlet />
    </Stack>
  )
}
