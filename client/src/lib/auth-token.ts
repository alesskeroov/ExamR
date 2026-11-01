// JWT access token storage. localStorage keeps the session across reloads; the token is short-lived
// (60 min) and the server revokes it on logout / password reset.

const KEY = 'examflow.auth'

interface StoredToken {
  accessToken: string
  expiresAt: string
}

export function saveToken(token: StoredToken) {
  try {
    localStorage.setItem(KEY, JSON.stringify(token))
  } catch {
    // Storage unavailable (private mode): the session lasts until reload.
    memory = token
  }
}

let memory: StoredToken | null = null

/** The access token, or null when missing or expired. */
export function getToken(): string | null {
  let stored = memory
  try {
    const raw = localStorage.getItem(KEY)
    if (raw) stored = JSON.parse(raw) as StoredToken
  } catch {
    // Fall back to the in-memory copy.
  }

  if (!stored || Date.parse(stored.expiresAt) <= Date.now()) return null
  return stored.accessToken
}

export function clearToken() {
  memory = null
  try {
    localStorage.removeItem(KEY)
  } catch {
    // Nothing stored.
  }
}
