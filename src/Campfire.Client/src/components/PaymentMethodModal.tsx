import { useEffect, useState, type FormEvent } from 'react'
import { loadStripe, type Stripe } from '@stripe/stripe-js'
import { CardElement, Elements, useElements, useStripe } from '@stripe/react-stripe-js'
import { useAuth } from '../auth/AuthContext'
import { createPaymentMethodSetupIntent, setDefaultPaymentMethod } from '../api/paymentMethods'
import { ApiRequestError } from '../api/client'

let stripePromise: Promise<Stripe | null> | null = null
function getStripe(publishableKey: string) {
  stripePromise ??= loadStripe(publishableKey)
  return stripePromise
}

interface PaymentMethodModalProps {
  onClose: () => void
  onSaved: () => void
}

function CardForm({ onClose, onSaved, setupClientSecret }: PaymentMethodModalProps & { setupClientSecret: string }) {
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

      await setDefaultPaymentMethod(paymentMethodId, token)
      onSaved()
    } catch (err) {
      setError(err instanceof ApiRequestError ? err.message : err instanceof Error ? err.message : 'Something went wrong. Try again.')
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <form className="auth-form" onSubmit={handleSubmit}>
      <h1>Add a card</h1>
      {error && <p className="auth-error">{error}</p>}
      <label>
        Card details
        <div className="card-element-wrapper">
          <CardElement options={{ style: { base: { fontSize: '16px' } } }} />
        </div>
      </label>
      <div className="tier-form-actions">
        <button type="submit" disabled={!stripe || submitting}>
          {submitting ? 'Saving…' : 'Save card'}
        </button>
        <button type="button" className="secondary" onClick={onClose} disabled={submitting}>
          Cancel
        </button>
      </div>
    </form>
  )
}

export function PaymentMethodModal({ onClose, onSaved }: PaymentMethodModalProps) {
  const { token } = useAuth()
  const [setupInfo, setSetupInfo] = useState<{ clientSecret: string; publishableKey: string } | null>(null)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    let cancelled = false
    createPaymentMethodSetupIntent(token)
      .then((info) => { if (!cancelled) setSetupInfo({ clientSecret: info.clientSecret, publishableKey: info.publishableKey }) })
      .catch((err) => { if (!cancelled) setError(err instanceof ApiRequestError ? err.message : 'Failed to start card setup.') })
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
        {!error && !setupInfo && <p className="placeholder">Loading…</p>}
        {setupInfo && (
          <Elements stripe={getStripe(setupInfo.publishableKey)}>
            <CardForm onClose={onClose} onSaved={onSaved} setupClientSecret={setupInfo.clientSecret} />
          </Elements>
        )}
      </div>
    </div>
  )
}
