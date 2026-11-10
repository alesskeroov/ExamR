import { useLayoutEffect, useSyncExternalStore } from 'react'
import { currentTheme } from './use-theme'

export type ExamTheme = 'auto' | 'paper' | 'calm' | 'night' | 'contrast'

/** Reading themes for the exam screen. `base` is the site mode the theme is drawn on (null = follow the site). */
export const examThemes: readonly { id: ExamTheme; label: string; hint: string; swatch: string; base: 'light' | 'dark' | null }[] = [
  { id: 'auto', label: 'Sistem', hint: 'Saytın açıq/tünd temasını izləyir', swatch: 'linear-gradient(135deg, #f5f6f2 50%, #161e1b 50%)', base: null },
  { id: 'paper', label: 'Sepiya', hint: 'İsti kağız rəngi, uzun oxuma üçün', swatch: '#f3ead8', base: 'light' },
  { id: 'calm', label: 'Sakit', hint: 'Yumşaq soyuq fon', swatch: 'linear-gradient(160deg, #e6eef5, #eeeaf6)', base: 'light' },
  { id: 'night', label: 'Gecə', hint: 'Tünd və az parlaq', swatch: '#14181c', base: 'dark' },
  { id: 'contrast', label: 'Yüksək kontrast', hint: 'Ağ fon, qara mətn və sərhədlər', swatch: 'linear-gradient(135deg, #fff 50%, #000 50%)', base: 'light' },
]

const key = 'examr-exam-theme'
const eventName = 'examr-exam-theme-change'
let selected: ExamTheme = 'auto'
try {
  const saved = localStorage.getItem(key)
  if (examThemes.some(t => t.id === saved)) selected = saved as ExamTheme
} catch { /* Default theme when storage is unavailable. */ }

function subscribe(notify: () => void) {
  const storage = (event: StorageEvent) => {
    if (event.key !== key && event.key !== null) return
    selected = examThemes.some(t => t.id === event.newValue) ? event.newValue as ExamTheme : 'auto'
    notify()
  }
  window.addEventListener(eventName, notify)
  window.addEventListener('storage', storage)
  return () => {
    window.removeEventListener(eventName, notify)
    window.removeEventListener('storage', storage)
  }
}

/** The chosen exam reading theme (shared by the exam screen and the settings card). */
export function useExamTheme() {
  const theme = useSyncExternalStore(subscribe, () => selected)
  function setTheme(next: ExamTheme) {
    selected = next
    try { localStorage.setItem(key, next) } catch { /* Keep the choice for this page session. */ }
    window.dispatchEvent(new Event(eventName))
  }
  return { theme, setTheme }
}

/** Exam screen only: while mounted, switches the page to the theme's light/dark base and restores the site theme after. */
export function useExamThemeBase(theme: ExamTheme) {
  const base = examThemes.find(t => t.id === theme)?.base ?? null
  useLayoutEffect(() => {
    if (!base) return
    const root = document.documentElement
    root.dataset.theme = base
    root.style.colorScheme = base
    return () => {
      const site = currentTheme()
      root.dataset.theme = site
      root.style.colorScheme = site
    }
  }, [base])
}
