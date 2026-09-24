import { createContext, useCallback, useContext, useMemo, useState, type ReactNode } from 'react'
import { apiFetch } from '../api/client'
import type { AuthResponse, UserRole } from '../api/types'

interface AuthUser {
  userId: string
  email: string
  displayName: string
  role: UserRole
}

interface AuthContextValue {
  user: AuthUser | null
  token: string | null
  isAuthenticated: boolean
  login: (email: string, password: string) => Promise<void>
  register: (email: string, password: string, displayName: string, role: UserRole) => Promise<void>
  logout: () => void
}

const STORAGE_KEY = 'campfire.auth'

const AuthContext = createContext<AuthContextValue | undefined>(undefined)

function loadStoredAuth(): { token: string; user: AuthUser } | null {
  try {
    const raw = localStorage.getItem(STORAGE_KEY)
    return raw ? JSON.parse(raw) : null
  } catch {
    return null
  }
}

export function AuthProvider({ children }: { children: ReactNode }) {
  const stored = loadStoredAuth()
  const [token, setToken] = useState<string | null>(stored?.token ?? null)
  const [user, setUser] = useState<AuthUser | null>(stored?.user ?? null)

  const persist = useCallback((auth: AuthResponse) => {
    const authUser: AuthUser = {
      userId: auth.userId,
      email: auth.email,
      displayName: auth.displayName,
      role: auth.role,
    }
    localStorage.setItem(STORAGE_KEY, JSON.stringify({ token: auth.token, user: authUser }))
    setToken(auth.token)
    setUser(authUser)
  }, [])

  const login = useCallback(async (email: string, password: string) => {
    const auth = await apiFetch<AuthResponse>('/auth/login', {
      method: 'POST',
      body: JSON.stringify({ email, password }),
    })
    persist(auth)
  }, [persist])

  const register = useCallback(async (email: string, password: string, displayName: string, role: UserRole) => {
    const auth = await apiFetch<AuthResponse>('/auth/register', {
      method: 'POST',
      body: JSON.stringify({ email, password, displayName, role }),
    })
    persist(auth)
  }, [persist])

  const logout = useCallback(() => {
    localStorage.removeItem(STORAGE_KEY)
    setToken(null)
    setUser(null)
  }, [])

  const value = useMemo<AuthContextValue>(() => ({
    user,
    token,
    isAuthenticated: !!token,
    login,
    register,
    logout,
  }), [user, token, login, register, logout])

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}

export function useAuth(): AuthContextValue {
  const ctx = useContext(AuthContext)
  if (!ctx) throw new Error('useAuth must be used within an AuthProvider')
  return ctx
}
