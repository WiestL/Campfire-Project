import { apiFetch, apiUpload } from './client'
import type { ContentItem, CreateContentRequest, UploadResponse } from './types'

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
