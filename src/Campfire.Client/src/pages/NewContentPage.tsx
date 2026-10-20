import { useState, type FormEvent } from 'react'
import { useNavigate } from 'react-router-dom'
import { useAuth } from '../auth/AuthContext'
import { createContent, uploadMedia } from '../api/content'
import { ApiRequestError } from '../api/client'
import { NavBar } from '../components/NavBar'
import type { ContentType } from '../api/types'

const MEDIA_TYPES: ContentType[] = ['Video', 'Audio']

export function NewContentPage() {
  const { token } = useAuth()
  const navigate = useNavigate()
  const [type, setType] = useState<ContentType>('Text')
  const [title, setTitle] = useState('')
  const [body, setBody] = useState('')
  const [file, setFile] = useState<File | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [submitting, setSubmitting] = useState(false)

  const needsMedia = MEDIA_TYPES.includes(type)

  async function handleSubmit(e: FormEvent) {
    e.preventDefault()
    setError(null)

    if (needsMedia && !file) {
      setError('Please choose a file to upload for this content type.')
      return
    }

    setSubmitting(true)
    try {
      let mediaPath: string | null = null
      if (needsMedia && file) {
        const upload = await uploadMedia(file, token)
        mediaPath = upload.storagePath
      }

      await createContent({
        type,
        title,
        body,
        mediaPath,
        requiredTierId: null,
        publish: true,
      }, token)

      navigate('/')
    } catch (err) {
      setError(err instanceof ApiRequestError ? err.message : 'Something went wrong. Try again.')
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <>
      <NavBar />
      <div className="auth-page">
      <form className="auth-form wide" onSubmit={handleSubmit}>
        <h1>New post</h1>
        {error && <p className="auth-error">{error}</p>}
        <label>
          Type
          <select value={type} onChange={(e) => setType(e.target.value as ContentType)}>
            <option value="Text">Text</option>
            <option value="Video">Video</option>
            <option value="Audio">Audio</option>
            <option value="BookClub">Book Club</option>
          </select>
        </label>
        <label>
          Title
          <input value={title} onChange={(e) => setTitle(e.target.value)} required maxLength={200} />
        </label>
        <label>
          Body
          <textarea value={body} onChange={(e) => setBody(e.target.value)} required rows={5} />
        </label>
        {needsMedia && (
          <label>
            {type === 'Video' ? 'Video file (.mp4, .mov, .webm, up to 500MB)' : 'Audio file (.mp3, .wav, .m4a, up to 100MB)'}
            <input
              type="file"
              accept={type === 'Video' ? 'video/mp4,video/quicktime,video/webm' : 'audio/mpeg,audio/wav,audio/mp4'}
              onChange={(e) => setFile(e.target.files?.[0] ?? null)}
            />
          </label>
        )}
        <button type="submit" disabled={submitting}>
          {submitting ? 'Publishing…' : 'Publish'}
        </button>
      </form>
      </div>
    </>
  )
}
