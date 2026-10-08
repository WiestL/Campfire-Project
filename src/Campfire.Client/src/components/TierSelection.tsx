import { useEffect, useState } from 'react'
import { useAuth } from '../auth/AuthContext'
import { getTiers } from '../api/tiers'
import { ApiRequestError } from '../api/client'
import type { Tier } from '../api/types'

export function TierSelection() {
  const { token } = useAuth()
  const [tiers, setTiers] = useState<Tier[] | null>(null)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    let cancelled = false
    getTiers(token)
      .then((data) => { if (!cancelled) setTiers(data) })
      .catch((err) => { if (!cancelled) setError(err instanceof ApiRequestError ? err.message : 'Failed to load tiers.') })
    return () => { cancelled = true }
  }, [token])

  if (error) return <p className="auth-error">{error}</p>
  if (tiers === null) return <p className="placeholder">Loading tiers…</p>
  if (tiers.length === 0) return <p className="placeholder">No membership tiers are available yet.</p>

  return (
    <div className="tier-grid">
      {tiers.map((tier) => (
        <article key={tier.id} className="dashboard-card">
          <h2>{tier.name}</h2>
          <p className="tier-price">${tier.price.toFixed(2)}/mo</p>
          <p>{tier.description}</p>
          <button type="button" disabled title="Payments arrive in Week 6">Subscribe</button>
        </article>
      ))}
    </div>
  )
}
