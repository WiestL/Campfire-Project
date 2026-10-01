const BASE_URL = '/api'

export class ApiRequestError extends Error {}

async function parseErrorMessage(response: Response): Promise<string> {
  const text = await response.text()
  if (!text) return response.statusText

  try {
    const data = JSON.parse(text)
    if (typeof data === 'string') return data
    if (data?.errors) {
      return Object.values(data.errors).flat().join(' ')
    }
    if (data?.title) return data.title
    return text
  } catch {
    return text
  }
}

export async function apiFetch<T>(
  path: string,
  options: RequestInit = {},
  token?: string | null,
): Promise<T> {
  const headers: Record<string, string> = {
    'Content-Type': 'application/json',
    ...(options.headers as Record<string, string> | undefined),
  }

  if (token) {
    headers.Authorization = `Bearer ${token}`
  }

  const response = await fetch(`${BASE_URL}${path}`, { ...options, headers })

  if (!response.ok) {
    throw new ApiRequestError(await parseErrorMessage(response))
  }

  if (response.status === 204) {
    return undefined as T
  }

  return (await response.json()) as T
}

export async function apiUpload<T>(path: string, file: File, token?: string | null): Promise<T> {
  const formData = new FormData()
  formData.append('file', file)

  const headers: Record<string, string> = {}
  if (token) {
    headers.Authorization = `Bearer ${token}`
  }

  const response = await fetch(`${BASE_URL}${path}`, { method: 'POST', body: formData, headers })

  if (!response.ok) {
    throw new ApiRequestError(await parseErrorMessage(response))
  }

  return (await response.json()) as T
}
