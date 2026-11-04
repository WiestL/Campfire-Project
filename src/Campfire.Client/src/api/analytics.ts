import { apiFetch } from './client'
import type { CreatorAnalytics } from './types'

export function getMyAnalytics(token: string | null) {
  return apiFetch<CreatorAnalytics>('/analytics/me', {}, token)
}
