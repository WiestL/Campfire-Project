import { apiFetch } from './client'
import type { ChangePasswordRequest, CurrentUser, UpdateProfileRequest } from './types'

export function getMe(token: string | null) {
  return apiFetch<CurrentUser>('/users/me', {}, token)
}

export function updateProfile(request: UpdateProfileRequest, token: string | null) {
  return apiFetch<CurrentUser>('/users/me', {
    method: 'PUT',
    body: JSON.stringify(request),
  }, token)
}

export function changePassword(request: ChangePasswordRequest, token: string | null) {
  return apiFetch<void>('/users/me/password', {
    method: 'PUT',
    body: JSON.stringify(request),
  }, token)
}
