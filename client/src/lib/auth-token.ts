// The JWT now lives only in an HttpOnly cookie set by the API. This removes tokens stored by older builds.
const KEY = 'examflow.auth'

export function clearToken() {
  for (const storageName of ['sessionStorage', 'localStorage'] as const) {
    try { window[storageName].removeItem(KEY) } catch { /* Storage may be unavailable. */ }
  }
}
