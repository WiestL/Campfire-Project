import { apiFetch } from './client'
import type { PaymentMethodInfo, SetupIntentInfo } from './types'

export function getMyPaymentMethod(token: string | null) {
  return apiFetch<PaymentMethodInfo | null>('/payment-methods/me', {}, token)
}

export function createPaymentMethodSetupIntent(token: string | null) {
  return apiFetch<SetupIntentInfo>('/payment-methods/me/setup-intent', { method: 'POST' }, token)
}

export function setDefaultPaymentMethod(paymentMethodId: string, token: string | null) {
  return apiFetch<void>('/payment-methods/me', {
    method: 'POST',
    body: JSON.stringify({ paymentMethodId }),
  }, token)
}

export function removePaymentMethod(token: string | null) {
  return apiFetch<void>('/payment-methods/me', { method: 'DELETE' }, token)
}
