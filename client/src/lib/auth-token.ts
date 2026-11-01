// Store only the server-issued JWT and expiry; never store credentials.
const KEY = 'examflow.auth'

interface StoredToken {
  accessToken: string
  expiresAt: string
}

let memory: StoredToken | null = null

export function saveToken(token: StoredToken, persistent = true) {
  clearToken()
  try {
    const storage = persistent ? localStorage : sessionStorage
    storage.setItem(KEY, JSON.stringify(token))
  } catch {
    memory = token
    // Storage unavailable: retain the session in memory until reload.
  }
}

/** The access token, or null when missing or expired. */
export function getToken(): string | null {
  let stored = memory
  if (!stored) {
    for (const storageName of ['sessionStorage', 'localStorage'] as const) {
      try {
        const raw = window[storageName].getItem(KEY)
        if (raw) { stored = JSON.parse(raw) as StoredToken; break }
      } catch {
        // Try the other storage when one is unavailable.
      }
    }
  }
  if (!stored || !Number.isFinite(Date.parse(stored.expiresAt)) || Date.parse(stored.expiresAt) <= Date.now()) return null
  return stored.accessToken
}

export function clearToken() {
  memory = null
  for (const storageName of ['sessionStorage', 'localStorage'] as const) {
    try { window[storageName].removeItem(KEY) } catch { /* Storage may be unavailable. */ }
  }
}
