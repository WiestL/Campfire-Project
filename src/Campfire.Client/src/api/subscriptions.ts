import { apiFetch } from './client'
import type { SetupIntentInfo, SubscriptionInfo } from './types'

export function getMySubscription(token: string | null) {
  return apiFetch<SubscriptionInfo | null>('/subscriptions/me', {}, token)
}

export function createSetupIntent(token: string | null) {
  return apiFetch<SetupIntentInfo>('/subscriptions/setup-intent', { method: 'POST' }, token)
}

export function createSubscription(tierId: string, paymentMethodId: string, token: string | null) {
  return apiFetch<SubscriptionInfo>('/subscriptions', {
    method: 'POST',
    body: JSON.stringify({ tierId, paymentMethodId }),
  }, token)
}
