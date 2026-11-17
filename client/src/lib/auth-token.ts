import { ApiError } from './api-client'

// The JWT now lives only in an HttpOnly cookie set by the API. This removes tokens stored by older builds.
const KEY = 'examflow.auth'

export function clearToken() {
  for (const storageName of ['sessionStorage', 'localStorage'] as const) {
    try { window[storageName].removeItem(KEY) } catch { /* Storage may be unavailable. */ }
  }
}

/**
 * When the API ended this browser's session (the account signed in on too many devices, or the owner ended it from
 * the devices list), open the sign-in form with an explanation instead of silently showing the landing page.
 */
export function showSessionEnded(error: unknown) {
  if (!(error instanceof ApiError) || error.status !== 401 || error.problem?.code !== 'session_revoked') return
  const reason = error.problem.reason === 'limit' ? 'session-limit' : 'session-ended'
  window.history.replaceState(null, '', `/?auth=login&account=${reason}`)
  // React Router follows popstate, so the app picks up the new URL without a reload.
  window.dispatchEvent(new PopStateEvent('popstate'))
}
