import { createContext } from 'react'
import type { Role } from '../types/pipeline'

export type AuthUser = {
  email: string
  displayName: string
  roles: string[]
}

export type AuthContextValue = {
  token: string | null
  user: AuthUser | null
  activeRole: Role | null
  availableRoles: Role[]
  isAuthenticated: boolean
  login: (email: string, password: string) => Promise<void>
  logout: () => void
  switchRole: (role: Role) => void
  /** Roles are cumulative: Viewer < Analyst < Approver < Admin. */
  can: (minimumRole: Role) => boolean
}

export const roleRank: Record<Role, number> = { Viewer: 1, Analyst: 2, Approver: 3, Admin: 4 }

export const AuthContext = createContext<AuthContextValue | null>(null)
