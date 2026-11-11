import { apiFetch, apiUpload } from './client'
import type { Comment, ContentItem, CreateContentRequest, LikeStatus, UploadResponse } from './types'

export function getFeed(token: string | null) {
  return apiFetch<ContentItem[]>('/content', {}, token)
}

export function createContent(request: CreateContentRequest, token: string | null) {
  return apiFetch<ContentItem>('/content', {
    method: 'POST',
    body: JSON.stringify(request),
  }, token)
}

export function uploadMedia(file: File, token: string | null) {
  return apiUpload<UploadResponse>('/content/upload', file, token)
}

export function toggleLike(contentId: string, token: string | null) {
  return apiFetch<LikeStatus>(`/content/${contentId}/like`, { method: 'POST' }, token)
}

export function getComments(contentId: string, token: string | null) {
  return apiFetch<Comment[]>(`/content/${contentId}/comments`, {}, token)
}

export function createComment(contentId: string, body: string, parentCommentId: string | null, token: string | null) {
  return apiFetch<Comment>(`/content/${contentId}/comments`, {
    method: 'POST',
    body: JSON.stringify({ body, parentCommentId }),
  }, token)
}

export function toggleHideComment(commentId: string, token: string | null) {
  return apiFetch<{ id: string; isHidden: boolean }>(`/comments/${commentId}/hide`, { method: 'POST' }, token)
}

export function deleteComment(commentId: string, token: string | null) {
  return apiFetch<void>(`/comments/${commentId}`, { method: 'DELETE' }, token)
}
