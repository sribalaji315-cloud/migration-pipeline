import { useState, type ReactNode } from 'react'
import { login as loginRequest } from '../api/auth'
import { clearStoredToken, getStoredToken, setStoredToken } from '../api/http'
import type { Role } from '../types/pipeline'
import { AuthContext, roleRank, type AuthContextValue, type AuthUser } from './auth'

const PROFILE_KEY = 'mdPipelineProfile'
const ACTIVE_ROLE_KEY = 'mdPipelineActiveRole'

export function AuthProvider({ children }: { children: ReactNode }) {
  const [token, setToken] = useState(() => getStoredToken())
  const [user, setUser] = useState<AuthUser | null>(() => readStoredUser())
  const [activeRole, setActiveRole] = useState<Role | null>(() => localStorage.getItem(ACTIVE_ROLE_KEY) as Role | null)
  const availableRoles = knownRoles(user?.roles ?? [])
  const effectiveRole = activeRole && availableRoles.includes(activeRole) ? activeRole : highestRole(availableRoles)

  async function login(email: string, password: string) {
    const response = await loginRequest(email, password)
    const nextUser: AuthUser = { email: response.email, displayName: response.displayName, roles: response.roles }
    const nextRole = highestRole(knownRoles(nextUser.roles))
    setStoredToken(response.token)
    localStorage.setItem(PROFILE_KEY, JSON.stringify(nextUser))
    storeActiveRole(nextRole)
    setToken(response.token)
    setUser(nextUser)
    setActiveRole(nextRole)
  }

  function logout() {
    clearStoredToken()
    localStorage.removeItem(PROFILE_KEY)
    localStorage.removeItem(ACTIVE_ROLE_KEY)
    setToken(null)
    setUser(null)
    setActiveRole(null)
  }

  function switchRole(role: Role) {
    if (availableRoles.includes(role)) {
      setActiveRole(role)
      storeActiveRole(role)
    }
  }

  const value: AuthContextValue = {
    token,
    user,
    activeRole: effectiveRole,
    availableRoles,
    isAuthenticated: Boolean(token),
    login,
    logout,
    switchRole,
    can: (minimumRole: Role) => effectiveRole !== null && roleRank[effectiveRole] >= roleRank[minimumRole],
  }

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}

function knownRoles(roles: string[]): Role[] {
  return (Object.keys(roleRank) as Role[]).filter((role) => roles.includes(role))
}

function highestRole(roles: Role[]): Role | null {
  return roles.reduce<Role | null>((best, role) => (best === null || roleRank[role] > roleRank[best] ? role : best), null)
}

function storeActiveRole(role: Role | null) {
  if (role) {
    localStorage.setItem(ACTIVE_ROLE_KEY, role)
  } else {
    localStorage.removeItem(ACTIVE_ROLE_KEY)
  }
}

function readStoredUser(): AuthUser | null {
  const stored = localStorage.getItem(PROFILE_KEY)
  if (!stored) {
    return null
  }

  try {
    const parsed = JSON.parse(stored) as AuthUser
    return Array.isArray(parsed.roles) ? parsed : null
  } catch {
    return null
  }
}
