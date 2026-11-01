import { useLayoutEffect, useSyncExternalStore } from 'react'

type Theme = 'light' | 'dark'
const key = 'examr-auth-theme'
const eventName = 'examr-theme-change'
let selected: Theme | null = null
try {
  const saved = localStorage.getItem(key)
  if (saved === 'light' || saved === 'dark') selected = saved
} catch { /* Use the system preference when storage is unavailable. */ }

function snapshot(): Theme {
  return selected ?? (window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light')
}
function subscribe(notify: () => void) {
  const media = window.matchMedia('(prefers-color-scheme: dark)')
  const storage = (event: StorageEvent) => {
    if (event.key !== key && event.key !== null) return
    selected = event.newValue === 'dark' || event.newValue === 'light' ? event.newValue : null
    notify()
  }
  window.addEventListener(eventName, notify)
  window.addEventListener('storage', storage)
  media.addEventListener('change', notify)
  return () => {
    window.removeEventListener(eventName, notify)
    window.removeEventListener('storage', storage)
    media.removeEventListener('change', notify)
  }
}

export function useTheme() {
  const theme = useSyncExternalStore(subscribe, snapshot)
  useLayoutEffect(() => {
    document.documentElement.dataset.theme = theme
    document.documentElement.style.colorScheme = theme
  }, [theme])
  function toggleTheme() {
    selected = theme === 'dark' ? 'light' : 'dark'
    try { localStorage.setItem(key, selected) } catch { /* Keep the choice for this page session. */ }
    window.dispatchEvent(new Event(eventName))
  }
  return { dark: theme === 'dark', toggleTheme }
}
