import { useEffect, useState } from 'react'
import { useAuth } from '../auth/AuthContext'
import { getMyAnalytics } from '../api/analytics'
import { ApiRequestError } from '../api/client'
import type { CreatorAnalytics } from '../api/types'

export function AnalyticsPanel() {
  const { token } = useAuth()
  const [data, setData] = useState<CreatorAnalytics | null>(null)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    let cancelled = false
    getMyAnalytics(token)
      .then((result) => { if (!cancelled) setData(result) })
      .catch((err) => { if (!cancelled) setError(err instanceof ApiRequestError ? err.message : 'Failed to load analytics.') })
    return () => { cancelled = true }
  }, [token])

  if (error) return <p className="auth-error">{error}</p>
  if (data === null) return <p className="placeholder">Loading analytics…</p>

  return (
    <div className="analytics-panel">
      <div className="stat-row">
        <div className="stat-tile">
          <span className="stat-value">{data.totalActiveSubscribers}</span>
          <span className="stat-label">Active subscribers</span>
        </div>
        <div className="stat-tile">
          <span className="stat-value">${data.monthlyRecurringRevenue.toFixed(2)}</span>
          <span className="stat-label">Monthly recurring revenue</span>
        </div>
        <div className="stat-tile">
          <span className="stat-value">{data.publishedPostCount}</span>
          <span className="stat-label">Posts published</span>
        </div>
      </div>

      {data.subscribersByTier.length > 0 && (
        <table className="stat-table">
          <thead>
            <tr>
              <th>Tier</th>
              <th>Active subscribers</th>
            </tr>
          </thead>
          <tbody>
            {data.subscribersByTier.map((tier) => (
              <tr key={tier.tierId}>
                <td>{tier.tierName}</td>
                <td>{tier.activeSubscribers}</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </div>
  )
}
