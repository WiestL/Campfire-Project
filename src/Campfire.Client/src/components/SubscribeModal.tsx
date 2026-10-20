import { useEffect, useState, type FormEvent } from 'react'
import { loadStripe, type Stripe } from '@stripe/stripe-js'
import { CardElement, Elements, useElements, useStripe } from '@stripe/react-stripe-js'
import { useAuth } from '../auth/AuthContext'
import { createSetupIntent, createSubscription } from '../api/subscriptions'
import { ApiRequestError } from '../api/client'
import type { Tier } from '../api/types'

let stripePromise: Promise<Stripe | null> | null = null
function getStripe(publishableKey: string) {
  stripePromise ??= loadStripe(publishableKey)
  return stripePromise
}

interface SubscribeModalProps {
  tier: Tier
  onClose: () => void
  onSubscribed: () => void
}

function CheckoutForm({ tier, onClose, onSubscribed, setupClientSecret }: SubscribeModalProps & { setupClientSecret: string }) {
  const { token } = useAuth()
  const stripe = useStripe()
  const elements = useElements()
  const [error, setError] = useState<string | null>(null)
  const [submitting, setSubmitting] = useState(false)

  async function handleSubmit(e: FormEvent) {
    e.preventDefault()
    if (!stripe || !elements) return
    setError(null)
    setSubmitting(true)

    try {
      const cardElement = elements.getElement(CardElement)
      if (!cardElement) throw new Error('Card field not ready.')

      const setupResult = await stripe.confirmCardSetup(setupClientSecret, {
        payment_method: { card: cardElement },
      })

      if (setupResult.error) {
        setError(setupResult.error.message ?? 'Card could not be saved.')
        return
      }

      const paymentMethodId = setupResult.setupIntent.payment_method
      if (typeof paymentMethodId !== 'string') {
        throw new Error('Stripe did not return a payment method.')
      }

      const subscription = await createSubscription(tier.id, paymentMethodId, token)

      if (subscription.clientSecretForAction) {
        const confirmResult = await stripe.confirmCardPayment(subscription.clientSecretForAction)
        if (confirmResult.error) {
          setError(confirmResult.error.message ?? 'Payment could not be confirmed.')
          return
        }
      }

      onSubscribed()
    } catch (err) {
      setError(err instanceof ApiRequestError ? err.message : err instanceof Error ? err.message : 'Something went wrong. Try again.')
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <form className="auth-form" onSubmit={handleSubmit}>
      <h1>Subscribe to {tier.name}</h1>
      <p className="tier-price">${tier.price.toFixed(2)}/mo</p>
      {error && <p className="auth-error">{error}</p>}
      <label>
        Card details
        <div className="card-element-wrapper">
          <CardElement options={{ style: { base: { fontSize: '16px' } } }} />
        </div>
      </label>
      <div className="tier-form-actions">
        <button type="submit" disabled={!stripe || submitting}>
          {submitting ? 'Processing…' : `Subscribe for $${tier.price.toFixed(2)}/mo`}
        </button>
        <button type="button" className="secondary" onClick={onClose} disabled={submitting}>
          Cancel
        </button>
      </div>
    </form>
  )
}

export function SubscribeModal({ tier, onClose, onSubscribed }: SubscribeModalProps) {
  const { token } = useAuth()
  const [setupInfo, setSetupInfo] = useState<{ clientSecret: string; publishableKey: string } | null>(null)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    let cancelled = false
    createSetupIntent(token)
      .then((info) => { if (!cancelled) setSetupInfo({ clientSecret: info.clientSecret, publishableKey: info.publishableKey }) })
      .catch((err) => { if (!cancelled) setError(err instanceof ApiRequestError ? err.message : 'Failed to start checkout.') })
    return () => { cancelled = true }
  }, [token])

  return (
    <div className="modal-overlay" onClick={onClose}>
      <div className="modal-content" onClick={(e) => e.stopPropagation()}>
        {error && (
          <div className="auth-form">
            <p className="auth-error">{error}</p>
            <button type="button" className="secondary" onClick={onClose}>Close</button>
          </div>
        )}
        {!error && !setupInfo && <p className="placeholder">Loading checkout…</p>}
        {setupInfo && (
          <Elements stripe={getStripe(setupInfo.publishableKey)}>
            <CheckoutForm tier={tier} onClose={onClose} onSubscribed={onSubscribed} setupClientSecret={setupInfo.clientSecret} />
          </Elements>
        )}
      </div>
    </div>
  )
}
