import { useEffect, useState, type FormEvent } from 'react'
import { useAuth } from '../auth/AuthContext'
import { changePassword, getMe, updateProfile } from '../api/users'
import { getMyPaymentMethod, removePaymentMethod } from '../api/paymentMethods'
import { cancelSubscription, changeSubscriptionTier, getMySubscription } from '../api/subscriptions'
import { getTiers } from '../api/tiers'
import { ApiRequestError } from '../api/client'
import { NavBar } from '../components/NavBar'
import { PaymentMethodModal } from '../components/PaymentMethodModal'
import type { CurrentUser, PaymentMethodInfo, SubscriptionInfo, Tier } from '../api/types'

function ProfileSection() {
  const { token } = useAuth()
  const [profile, setProfile] = useState<CurrentUser | null>(null)
  const [displayName, setDisplayName] = useState('')
  const [email, setEmail] = useState('')
  const [currentPassword, setCurrentPassword] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [success, setSuccess] = useState(false)
  const [submitting, setSubmitting] = useState(false)

  useEffect(() => {
    getMe(token).then((me) => {
      setProfile(me)
      setDisplayName(me.displayName)
      setEmail(me.email)
    })
  }, [token])

  async function handleSubmit(e: FormEvent) {
    e.preventDefault()
    setError(null)
    setSuccess(false)
    setSubmitting(true)
    try {
      const updated = await updateProfile({ displayName, email, currentPassword }, token)
      setProfile(updated)
      setCurrentPassword('')
      setSuccess(true)
    } catch (err) {
      setError(err instanceof ApiRequestError ? err.message : 'Something went wrong. Try again.')
    } finally {
      setSubmitting(false)
    }
  }

  if (!profile) return <p className="placeholder">Loading…</p>

  return (
    <form className="tier-form" onSubmit={handleSubmit}>
      <h2>Profile</h2>
      {error && <p className="auth-error">{error}</p>}
      {success && <p className="tier-success">Profile updated.</p>}
      <label>
        Display name
        <input value={displayName} onChange={(e) => setDisplayName(e.target.value)} required maxLength={100} />
      </label>
      <label>
        Email
        <input type="email" value={email} onChange={(e) => setEmail(e.target.value)} required />
      </label>
      <label>
        Current password (required to save changes)
        <input type="password" value={currentPassword} onChange={(e) => setCurrentPassword(e.target.value)} required />
      </label>
      <div className="tier-form-actions">
        <button type="submit" disabled={submitting}>{submitting ? 'Saving…' : 'Save changes'}</button>
      </div>
    </form>
  )
}

function PasswordSection() {
  const { token } = useAuth()
  const [currentPassword, setCurrentPassword] = useState('')
  const [newPassword, setNewPassword] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [success, setSuccess] = useState(false)
  const [submitting, setSubmitting] = useState(false)

  async function handleSubmit(e: FormEvent) {
    e.preventDefault()
    setError(null)
    setSuccess(false)
    setSubmitting(true)
    try {
      await changePassword({ currentPassword, newPassword }, token)
      setCurrentPassword('')
      setNewPassword('')
      setSuccess(true)
    } catch (err) {
      setError(err instanceof ApiRequestError ? err.message : 'Something went wrong. Try again.')
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <form className="tier-form" onSubmit={handleSubmit}>
      <h2>Password</h2>
      {error && <p className="auth-error">{error}</p>}
      {success && <p className="tier-success">Password changed.</p>}
      <label>
        Current password
        <input type="password" value={currentPassword} onChange={(e) => setCurrentPassword(e.target.value)} required />
      </label>
      <label>
        New password
        <input type="password" value={newPassword} onChange={(e) => setNewPassword(e.target.value)} required minLength={8} />
      </label>
      <div className="tier-form-actions">
        <button type="submit" disabled={submitting}>{submitting ? 'Saving…' : 'Change password'}</button>
      </div>
    </form>
  )
}

function PaymentMethodSection() {
  const { token } = useAuth()
  // GET /payment-methods/me returns 204 (no JSON body at all) when there is no
  // card, and apiFetch resolves a 204 to `undefined`, not `null` - so loading
  // and "no card" can't both be told apart by undefined vs. null on the same
  // variable. A separate `loaded` flag is the fix; found live, since a brand
  // new member's "Loading..." never went away without it.
  const [method, setMethod] = useState<PaymentMethodInfo | null>(null)
  const [loaded, setLoaded] = useState(false)
  const [showModal, setShowModal] = useState(false)
  const [error, setError] = useState<string | null>(null)

  function refresh() {
    getMyPaymentMethod(token)
      .then((result) => { setMethod(result ?? null); setLoaded(true) })
      .catch((err) => setError(err instanceof ApiRequestError ? err.message : 'Failed to load payment method.'))
  }

  useEffect(refresh, [token])

  async function handleRemove() {
    setError(null)
    try {
      await removePaymentMethod(token)
      refresh()
    } catch (err) {
      setError(err instanceof ApiRequestError ? err.message : 'Failed to remove card.')
    }
  }

  return (
    <div className="tier-form">
      <h2>Payment method</h2>
      {error && <p className="auth-error">{error}</p>}
      {!loaded && <p className="placeholder">Loading…</p>}
      {loaded && method === null && <p className="placeholder">No card on file.</p>}
      {method && (
        <p>
          {method.brand.toUpperCase()} ending in {method.last4}, expires {method.expMonth}/{method.expYear}
        </p>
      )}
      <div className="tier-form-actions">
        <button type="button" onClick={() => setShowModal(true)}>
          {method ? 'Replace card' : 'Add card'}
        </button>
        {method && (
          <button type="button" className="secondary" onClick={handleRemove}>
            Remove
          </button>
        )}
      </div>
      {showModal && (
        <PaymentMethodModal
          onClose={() => setShowModal(false)}
          onSaved={() => {
            setShowModal(false)
            refresh()
          }}
        />
      )}
    </div>
  )
}

function SubscriptionSection() {
  const { token } = useAuth()
  // Same 204-means-undefined gotcha as PaymentMethodSection: GET /subscriptions/me
  // comes back as `undefined` (no body), not `null`, when there's no
  // subscription, so loading vs. empty needs its own flag rather than
  // overloading this one value with a third state.
  const [subscription, setSubscription] = useState<SubscriptionInfo | null>(null)
  const [loaded, setLoaded] = useState(false)
  const [tiers, setTiers] = useState<Tier[]>([])
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  function refresh() {
    Promise.all([getMySubscription(token), getTiers(token)])
      .then(([sub, allTiers]) => {
        setSubscription(sub ?? null)
        setTiers(allTiers)
        setLoaded(true)
      })
      .catch((err) => setError(err instanceof ApiRequestError ? err.message : 'Failed to load subscription.'))
  }

  useEffect(refresh, [token])

  async function handleCancel() {
    if (!window.confirm('Cancel your subscription? You will lose access to gated content immediately.')) return
    setError(null)
    setBusy(true)
    try {
      await cancelSubscription(token)
      refresh()
    } catch (err) {
      setError(err instanceof ApiRequestError ? err.message : 'Failed to cancel subscription.')
    } finally {
      setBusy(false)
    }
  }

  async function handleSwitch(newTierId: string) {
    setError(null)
    setBusy(true)
    try {
      await changeSubscriptionTier(newTierId, token)
      refresh()
    } catch (err) {
      setError(err instanceof ApiRequestError ? err.message : 'Failed to switch tiers.')
    } finally {
      setBusy(false)
    }
  }

  if (!loaded) return <div className="tier-form"><h2>Subscription</h2><p className="placeholder">Loading…</p></div>
  if (subscription === null) return <div className="tier-form"><h2>Subscription</h2><p className="placeholder">You don't have an active subscription.</p></div>

  const otherTiersFromSameCreator = tiers.filter((t) => t.id !== subscription.tierId
    && tiers.find((cur) => cur.id === subscription.tierId)?.creatorId === t.creatorId)

  return (
    <div className="tier-form">
      <h2>Subscription</h2>
      {error && <p className="auth-error">{error}</p>}
      <p>
        <strong>{subscription.tierName}</strong> · {subscription.status}
        {subscription.currentPeriodEnd && <> · renews {new Date(subscription.currentPeriodEnd).toLocaleDateString()}</>}
      </p>
      {otherTiersFromSameCreator.length > 0 && (
        <label>
          Switch tier
          <select disabled={busy} defaultValue="" onChange={(e) => e.target.value && handleSwitch(e.target.value)}>
            <option value="" disabled>Choose a tier…</option>
            {otherTiersFromSameCreator.map((t) => (
              <option key={t.id} value={t.id}>{t.name} (${t.price.toFixed(2)}/mo)</option>
            ))}
          </select>
        </label>
      )}
      <div className="tier-form-actions">
        <button type="button" className="secondary" onClick={handleCancel} disabled={busy}>
          Cancel subscription
        </button>
      </div>
    </div>
  )
}

export function AccountSettingsPage() {
  const { user } = useAuth()

  return (
    <>
      <NavBar />
      <div className="dashboard">
        <header className="dashboard-header">
          <h1>Account Settings</h1>
        </header>
        <div className="dashboard-grid">
          <ProfileSection />
          <PasswordSection />
          {user?.role === 'Member' && <PaymentMethodSection />}
          {user?.role === 'Member' && <SubscriptionSection />}
        </div>
      </div>
    </>
  )
}
