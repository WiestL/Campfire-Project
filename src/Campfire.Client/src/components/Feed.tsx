import { useEffect, useState } from 'react'
import { useAuth } from '../auth/AuthContext'
import { getFeed } from '../api/content'
import { ApiRequestError } from '../api/client'
import type { ContentItem } from '../api/types'

function MediaPreview({ item }: { item: ContentItem }) {
  if (!item.mediaUrl) return null
  if (item.type === 'Video') {
    return <video className="feed-media" src={item.mediaUrl} controls />
  }
  if (item.type === 'Audio') {
    return <audio className="feed-media" src={item.mediaUrl} controls />
  }
  return null
}

export function Feed() {
  const { token } = useAuth()
  const [items, setItems] = useState<ContentItem[] | null>(null)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    let cancelled = false
    getFeed(token)
      .then((data) => { if (!cancelled) setItems(data) })
      .catch((err) => { if (!cancelled) setError(err instanceof ApiRequestError ? err.message : 'Failed to load the feed.') })
    return () => { cancelled = true }
  }, [token])

  if (error) return <p className="auth-error">{error}</p>
  if (items === null) return <p className="placeholder">Loading feed…</p>
  if (items.length === 0) return <p className="placeholder">No content published yet.</p>

  return (
    <div className="feed">
      {items.map((item) => (
        <article key={item.id} className="feed-item">
          {item.mediaUrl && (
            <div className="feed-item-media">
              <MediaPreview item={item} />
            </div>
          )}
          <div className="feed-item-body">
            <h3>{item.title}</h3>
            <p className="feed-meta">
              {item.creatorDisplayName} · {new Date(item.publishedAt ?? item.createdAt).toLocaleDateString()}
              {!item.mediaUrl && <> · {item.type}</>}
            </p>
            <p>{item.body}</p>
          </div>
        </article>
      ))}
    </div>
  )
}
