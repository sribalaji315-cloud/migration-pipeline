const API_BASE_URL = import.meta.env.VITE_API_BASE_URL ?? ''
const TOKEN_KEY = 'mdPipelineToken'

export class ApiError extends Error {
  readonly status: number

  constructor(message: string, status: number) {
    super(message)
    this.status = status
  }
}

export function getStoredToken() {
  return localStorage.getItem(TOKEN_KEY)
}

export function setStoredToken(token: string) {
  localStorage.setItem(TOKEN_KEY, token)
}

export function clearStoredToken() {
  localStorage.removeItem(TOKEN_KEY)
}

async function send<T>(path: string, init: RequestInit, json: boolean): Promise<T> {
  const headers = new Headers(init.headers)
  if (json) {
    headers.set('Content-Type', 'application/json')
  }

  const token = getStoredToken()
  if (token) {
    headers.set('Authorization', `Bearer ${token}`)
  }

  const response = await fetch(`${API_BASE_URL}${path}`, { ...init, headers })
  if (!response.ok) {
    const body = await response.json().catch(() => null)
    throw new ApiError(body?.error ?? response.statusText, response.status)
  }

  return response.json() as Promise<T>
}

export function apiRequest<T>(path: string, init: RequestInit = {}) {
  return send<T>(path, init, true)
}

export function apiFormRequest<T>(path: string, body: FormData) {
  return send<T>(path, { method: 'POST', body }, false)
}

/** Downloads an authenticated file (a plain link cannot send the bearer token). */
export async function apiDownload(path: string, fileName: string) {
  const headers = new Headers()
  const token = getStoredToken()
  if (token) {
    headers.set('Authorization', `Bearer ${token}`)
  }

  const response = await fetch(`${API_BASE_URL}${path}`, { headers })
  if (!response.ok) {
    const body = await response.json().catch(() => null)
    throw new ApiError(body?.error ?? response.statusText, response.status)
  }

  const url = URL.createObjectURL(await response.blob())
  const link = document.createElement('a')
  link.href = url
  link.download = fileName
  link.click()
  URL.revokeObjectURL(url)
}
