import { focusManager } from '@tanstack/react-query'

const IDLE_AFTER_MS = 10 * 60_000
const ACTIVITY_EVENTS = ['pointerdown', 'pointermove', 'keydown', 'wheel', 'touchstart'] as const

/**
 * TanStack Query runs `refetchInterval` polling only while the app counts as focused (hidden tabs already stop).
 * This also treats a visible tab without any input for 10 minutes as unfocused, so a page left open on a screen
 * stops polling the API and the serverless database can auto-pause. The first input resumes polling.
 */
export function pausePollingWhenIdle() {
  focusManager.setEventListener((setFocused) => {
    let lastActivity = Date.now()
    let idle = false
    const update = () => {
      idle = Date.now() - lastActivity > IDLE_AFTER_MS
      setFocused(document.visibilityState === 'visible' && !idle)
    }
    const onActivity = () => {
      lastActivity = Date.now()
      if (idle) update()
    }
    const onVisibility = () => {
      if (document.visibilityState === 'visible') lastActivity = Date.now()
      update()
    }

    ACTIVITY_EVENTS.forEach((name) => window.addEventListener(name, onActivity, { passive: true }))
    document.addEventListener('visibilitychange', onVisibility)
    const timer = window.setInterval(update, 60_000)
    update()
    return () => {
      ACTIVITY_EVENTS.forEach((name) => window.removeEventListener(name, onActivity))
      document.removeEventListener('visibilitychange', onVisibility)
      window.clearInterval(timer)
    }
  })
}
