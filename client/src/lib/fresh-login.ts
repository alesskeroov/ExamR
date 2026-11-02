// One-shot marker for the post-login greeting animation. sessionStorage survives the Google OAuth redirect
// within the tab and disappears with it; the dashboard clears it after reading, so navigation never replays it.
const KEY = 'examr.freshLogin'

export function markFreshLogin() {
  try { sessionStorage.setItem(KEY, '1') } catch { /* storage unavailable: no animation, nothing else changes */ }
}

export function isFreshLogin() {
  try { return sessionStorage.getItem(KEY) === '1' } catch { return false }
}

export function clearFreshLogin() {
  try { sessionStorage.removeItem(KEY) } catch { /* ignore */ }
}
