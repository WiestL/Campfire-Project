export type UserRole = 'Creator' | 'Member' | 'Admin'

export interface AuthResponse {
  token: string
  expiresAt: string
  userId: string
  email: string
  displayName: string
  role: UserRole
}

export interface CurrentUser {
  id: string
  email: string
  displayName: string
  role: UserRole
  createdAt: string
}

export interface ApiError {
  message: string
}
