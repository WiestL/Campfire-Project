import { useEffect, useState } from 'react'
import { useAuth } from '../auth/AuthContext'
import { getTiers } from '../api/tiers'
import { getMySubscription } from '../api/subscriptions'
import { ApiRequestError } from '../api/client'
import type { SubscriptionInfo, Tier } from '../api/types'
import { SubscribeModal } from './SubscribeModal'

export function TierSelection() {
  const { token } = useAuth()
  const [tiers, setTiers] = useState<Tier[] | null>(null)
  const [subscription, setSubscription] = useState<SubscriptionInfo | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [subscribingTier, setSubscribingTier] = useState<Tier | null>(null)
  const [justSubscribed, setJustSubscribed] = useState(false)

  function refresh() {
    Promise.all([getTiers(token), getMySubscription(token)])
      .then(([tierData, subData]) => {
        setTiers(tierData)
        setSubscription(subData)
      })
      .catch((err) => setError(err instanceof ApiRequestError ? err.message : 'Failed to load tiers.'))
  }

  useEffect(() => {
    refresh()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [token])

  if (error) return <p className="auth-error">{error}</p>
  if (tiers === null) return <p className="placeholder">Loading tiers…</p>
  if (tiers.length === 0) return <p className="placeholder">No membership tiers are available yet.</p>

  const hasActiveSubscription = subscription?.status === 'Active'

  return (
    <div className="tier-grid">
      {justSubscribed && <p className="tier-success">Subscribed! Your membership is now active.</p>}
      {tiers.map((tier) => {
        const isCurrentTier = subscription?.tierId === tier.id && hasActiveSubscription
        return (
          <article key={tier.id} className="dashboard-card">
            <h2>{tier.name}</h2>
            <p className="tier-price">${tier.price.toFixed(2)}/mo</p>
            <p>{tier.description}</p>
            {isCurrentTier ? (
              <span className="feed-badge">Current plan</span>
            ) : (
              <button
                type="button"
                disabled={hasActiveSubscription}
                title={hasActiveSubscription ? 'You already have an active subscription' : undefined}
                onClick={() => setSubscribingTier(tier)}
              >
                Subscribe
              </button>
            )}
          </article>
        )
      })}
      {subscribingTier && (
        <SubscribeModal
          tier={subscribingTier}
          onClose={() => setSubscribingTier(null)}
          onSubscribed={() => {
            setSubscribingTier(null)
            setJustSubscribed(true)
            refresh()
          }}
        />
      )}
    </div>
  )
}
