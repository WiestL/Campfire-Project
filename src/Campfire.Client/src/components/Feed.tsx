import { useEffect, useState } from 'react'
import { useAuth } from '../auth/AuthContext'
import { getFeed, toggleLike } from '../api/content'
import { ApiRequestError } from '../api/client'
import { CommentThread } from './CommentThread'
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

function FeedItemEngagement({ item, onLikeToggled }: { item: ContentItem; onLikeToggled: (next: { likeCount: number; likedByCurrentUser: boolean }) => void }) {
  const { token } = useAuth()
  const [commentsOpen, setCommentsOpen] = useState(false)

  async function handleLike() {
    const status = await toggleLike(item.id, token)
    onLikeToggled(status)
  }

  return (
    <div className="feed-engagement">
      <div className="feed-engagement-bar">
        <button type="button" className={item.likedByCurrentUser ? 'like-button liked' : 'like-button'} onClick={handleLike}>
          {item.likedByCurrentUser ? 'Liked' : 'Like'} · {item.likeCount}
        </button>
        <button type="button" className="comment-toggle" onClick={() => setCommentsOpen((open) => !open)}>
          {item.commentCount} comment{item.commentCount === 1 ? '' : 's'}
        </button>
      </div>
      {commentsOpen && <CommentThread item={item} />}
    </div>
  )
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

  function updateItem(id: string, patch: Partial<ContentItem>) {
    setItems((current) => current && current.map((i) => (i.id === id ? { ...i, ...patch } : i)))
  }

  return (
    <div className="feed">
      {items.map((item) => (
        <article key={item.id} className={item.isLocked ? 'feed-item feed-item-locked' : 'feed-item'}>
          {item.isLocked ? (
            <div className="feed-locked-teaser">
              <span className="feed-lock-badge">Locked</span>
              <span>Subscribe to {item.requiredTierName ?? 'a paid tier'} to unlock</span>
            </div>
          ) : (
            item.mediaUrl && (
              <div className="feed-item-media">
                <MediaPreview item={item} />
              </div>
            )
          )}
          <div className="feed-item-body">
            <h3>{item.title}</h3>
            <p className="feed-meta">
              {item.creatorDisplayName} · {new Date(item.publishedAt ?? item.createdAt).toLocaleDateString()}
              {!item.mediaUrl && !item.isLocked && <> · {item.type}</>}
            </p>
            {!item.isLocked && <p>{item.body}</p>}
            {!item.isLocked && (
              <FeedItemEngagement item={item} onLikeToggled={(status) => updateItem(item.id, { likeCount: status.likeCount, likedByCurrentUser: status.likedByCurrentUser })} />
            )}
          </div>
        </article>
      ))}
    </div>
  )
}
