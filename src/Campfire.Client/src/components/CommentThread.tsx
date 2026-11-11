import { useEffect, useState, type FormEvent } from 'react'
import { useAuth } from '../auth/AuthContext'
import { createComment, deleteComment, getComments, toggleHideComment } from '../api/content'
import { ApiRequestError } from '../api/client'
import type { Comment, ContentItem } from '../api/types'

function CommentRow({ comment, contentCreatorId, onChanged }: { comment: Comment; contentCreatorId: string; onChanged: () => void }) {
  const { user, token } = useAuth()
  const canModerate = user?.role === 'Admin' || user?.userId === contentCreatorId
  const canDelete = canModerate || user?.userId === comment.userId

  async function handleHide() {
    await toggleHideComment(comment.id, token)
    onChanged()
  }

  async function handleDelete() {
    await deleteComment(comment.id, token)
    onChanged()
  }

  return (
    <li className="comment-row">
      <p className="comment-meta">
        <strong>{comment.authorDisplayName}</strong> · {new Date(comment.createdAt).toLocaleDateString()}
        {comment.isHidden && <span className="comment-hidden-tag"> · Hidden</span>}
      </p>
      <p className="comment-body">{comment.isHidden ? (comment.body ?? '[hidden]') : comment.body}</p>
      {(canModerate || canDelete) && (
        <div className="comment-actions">
          {canModerate && (
            <button type="button" className="comment-action" onClick={handleHide}>
              {comment.isHidden ? 'Unhide' : 'Hide'}
            </button>
          )}
          {canDelete && (
            <button type="button" className="comment-action" onClick={handleDelete}>
              Delete
            </button>
          )}
        </div>
      )}
    </li>
  )
}

export function CommentThread({ item }: { item: ContentItem }) {
  const { token } = useAuth()
  const [comments, setComments] = useState<Comment[] | null>(null)
  const [newComment, setNewComment] = useState('')
  const [replyTo, setReplyTo] = useState<string | null>(null)
  const [replyBody, setReplyBody] = useState('')
  const [error, setError] = useState<string | null>(null)

  function refresh() {
    getComments(item.id, token)
      .then(setComments)
      .catch((err) => setError(err instanceof ApiRequestError ? err.message : 'Failed to load comments.'))
  }

  useEffect(refresh, [item.id, token])

  async function handleSubmitTopLevel(e: FormEvent) {
    e.preventDefault()
    if (!newComment.trim()) return
    await createComment(item.id, newComment, null, token)
    setNewComment('')
    refresh()
  }

  async function handleSubmitReply(e: FormEvent, parentId: string) {
    e.preventDefault()
    if (!replyBody.trim()) return
    await createComment(item.id, replyBody, parentId, token)
    setReplyBody('')
    setReplyTo(null)
    refresh()
  }

  if (error) return <p className="auth-error">{error}</p>
  if (comments === null) return <p className="placeholder">Loading comments…</p>

  return (
    <div className="comment-thread">
      <form className="comment-form" onSubmit={handleSubmitTopLevel}>
        <input
          value={newComment}
          onChange={(e) => setNewComment(e.target.value)}
          placeholder="Add a comment…"
          maxLength={2000}
        />
        <button type="submit">Post</button>
      </form>

      {comments.length === 0 && <p className="placeholder">No comments yet.</p>}

      <ul className="comment-list">
        {comments.map((comment) => (
          <li key={comment.id} className="comment-thread-item">
            <ul className="comment-list">
              <CommentRow comment={comment} contentCreatorId={item.creatorId} onChanged={refresh} />
            </ul>
            {comment.replies.length > 0 && (
              <ul className="comment-list replies">
                {comment.replies.map((reply) => (
                  <CommentRow key={reply.id} comment={reply} contentCreatorId={item.creatorId} onChanged={refresh} />
                ))}
              </ul>
            )}
            {replyTo === comment.id ? (
              <form className="comment-form reply-form" onSubmit={(e) => handleSubmitReply(e, comment.id)}>
                <input
                  value={replyBody}
                  onChange={(e) => setReplyBody(e.target.value)}
                  placeholder="Write a reply…"
                  maxLength={2000}
                  autoFocus
                />
                <button type="submit">Reply</button>
                <button type="button" className="secondary" onClick={() => setReplyTo(null)}>Cancel</button>
              </form>
            ) : (
              <button type="button" className="comment-action reply-toggle" onClick={() => setReplyTo(comment.id)}>
                Reply
              </button>
            )}
          </li>
        ))}
      </ul>
    </div>
  )
}
