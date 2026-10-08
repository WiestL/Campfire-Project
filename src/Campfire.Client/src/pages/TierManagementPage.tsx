import { useEffect, useState, type FormEvent } from 'react'
import { useAuth } from '../auth/AuthContext'
import { createTier, deleteTier, getTiers, updateTier } from '../api/tiers'
import { ApiRequestError } from '../api/client'
import type { Tier, TierRequest } from '../api/types'

const emptyForm: TierRequest = { name: '', description: '', price: 0, level: 0 }

export function TierManagementPage() {
  const { token, user } = useAuth()
  const [tiers, setTiers] = useState<Tier[] | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [form, setForm] = useState<TierRequest>(emptyForm)
  const [editingId, setEditingId] = useState<string | null>(null)
  const [submitting, setSubmitting] = useState(false)

  async function refresh() {
    try {
      const all = await getTiers(token)
      setTiers(all.filter((t) => t.creatorId === user?.userId))
    } catch (err) {
      setError(err instanceof ApiRequestError ? err.message : 'Failed to load tiers.')
    }
  }

  useEffect(() => {
    refresh()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  function startEdit(tier: Tier) {
    setEditingId(tier.id)
    setForm({ name: tier.name, description: tier.description, price: tier.price, level: tier.level })
  }

  function cancelEdit() {
    setEditingId(null)
    setForm(emptyForm)
  }

  async function handleSubmit(e: FormEvent) {
    e.preventDefault()
    setError(null)
    setSubmitting(true)
    try {
      if (editingId) {
        await updateTier(editingId, form, token)
      } else {
        await createTier(form, token)
      }
      setForm(emptyForm)
      setEditingId(null)
      await refresh()
    } catch (err) {
      setError(err instanceof ApiRequestError ? err.message : 'Something went wrong. Try again.')
    } finally {
      setSubmitting(false)
    }
  }

  async function handleDelete(id: string) {
    setError(null)
    try {
      await deleteTier(id, token)
      await refresh()
    } catch (err) {
      setError(err instanceof ApiRequestError ? err.message : 'Failed to delete tier.')
    }
  }

  return (
    <div className="dashboard">
      <header className="dashboard-header">
        <h1>Membership Tiers</h1>
      </header>

      {error && <p className="auth-error">{error}</p>}

      <form className="tier-form" onSubmit={handleSubmit}>
        <h2>{editingId ? 'Edit tier' : 'New tier'}</h2>
        <label>
          Name
          <input value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} required maxLength={100} />
        </label>
        <label>
          Description
          <textarea value={form.description} onChange={(e) => setForm({ ...form, description: e.target.value })} required rows={3} />
        </label>
        <div className="tier-form-row">
          <label>
            Price (USD/month)
            <input
              type="number"
              min={0}
              step="0.01"
              value={form.price}
              onChange={(e) => setForm({ ...form, price: Number(e.target.value) })}
              required
            />
          </label>
          <label>
            Level
            <input
              type="number"
              min={0}
              step="1"
              value={form.level}
              onChange={(e) => setForm({ ...form, level: Number(e.target.value) })}
              required
            />
          </label>
        </div>
        <div className="tier-form-actions">
          <button type="submit" disabled={submitting}>
            {submitting ? 'Saving…' : editingId ? 'Save changes' : 'Create tier'}
          </button>
          {editingId && (
            <button type="button" className="secondary" onClick={cancelEdit}>
              Cancel
            </button>
          )}
        </div>
      </form>

      <h2 className="feed-heading">Your tiers</h2>
      {tiers === null ? (
        <p className="placeholder">Loading…</p>
      ) : tiers.length === 0 ? (
        <p className="placeholder">No tiers yet. Create one above.</p>
      ) : (
        <div className="tier-grid">
          {tiers.map((tier) => (
            <article key={tier.id} className="dashboard-card">
              <h2>{tier.name}</h2>
              <p className="tier-price">${tier.price.toFixed(2)}/mo · Level {tier.level}</p>
              <p>{tier.description}</p>
              <div className="tier-card-actions">
                <button type="button" onClick={() => startEdit(tier)}>Edit</button>
                <button type="button" className="secondary" onClick={() => handleDelete(tier.id)}>Delete</button>
              </div>
            </article>
          ))}
        </div>
      )}
    </div>
  )
}
