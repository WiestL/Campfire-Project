export type UserRole = 'Creator' | 'Member' | 'Admin'

export interface AuthResponse {
  token: string
  expiresAt: string
  userId: string
  email: string
  displayName: string
  role: UserRole
}

export interface CurrentUser {
  id: string
  email: string
  displayName: string
  role: UserRole
  createdAt: string
}

export type ContentType = 'Text' | 'Video' | 'Audio' | 'BookClub'

export interface ContentItem {
  id: string
  creatorId: string
  creatorDisplayName: string
  type: ContentType
  title: string
  body: string
  mediaUrl: string | null
  requiredTierId: string | null
  createdAt: string
  publishedAt: string | null
}

export interface CreateContentRequest {
  type: ContentType
  title: string
  body: string
  mediaPath: string | null
  requiredTierId: string | null
  publish: boolean
}

export interface UploadResponse {
  storagePath: string
  fileName: string
  sizeBytes: number
}

export interface Tier {
  id: string
  creatorId: string
  creatorDisplayName: string
  name: string
  description: string
  price: number
  level: number
}

export interface TierRequest {
  name: string
  description: string
  price: number
  level: number
}

export interface ApiError {
  message: string
}
