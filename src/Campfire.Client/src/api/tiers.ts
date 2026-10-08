import { apiFetch } from './client'
import type { Tier, TierRequest } from './types'

export function getTiers(token: string | null) {
  return apiFetch<Tier[]>('/tiers', {}, token)
}

export function createTier(request: TierRequest, token: string | null) {
  return apiFetch<Tier>('/tiers', { method: 'POST', body: JSON.stringify(request) }, token)
}

export function updateTier(id: string, request: TierRequest, token: string | null) {
  return apiFetch<Tier>(`/tiers/${id}`, { method: 'PUT', body: JSON.stringify(request) }, token)
}

export function deleteTier(id: string, token: string | null) {
  return apiFetch<void>(`/tiers/${id}`, { method: 'DELETE' }, token)
}
