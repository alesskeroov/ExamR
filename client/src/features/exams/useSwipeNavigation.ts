import { useEffect, useRef, type RefObject } from 'react'

/** Horizontal distance (px) a finger must travel, and how clearly horizontal the gesture must be. */
const MIN_DISTANCE = 60
const DIRECTION_RATIO = 2
const MAX_DURATION_MS = 800

interface SwipeOptions {
  enabled: boolean
  /** Return false when there is no question in that direction (first/last): the content nudges instead. */
  onNext: () => boolean
  onPrev: () => boolean
}

/**
 * Touch swipe between questions: left = next, right = previous. Listeners are passive and never call
 * preventDefault, so vertical scrolling and pinch-zoom stay native. A swipe is ignored when more than one finger
 * is involved, the page is zoomed in (the finger is panning the zoomed view), text is being selected, or it starts
 * on a control or inside a horizontally scrollable area. The click that may follow a swipe is swallowed, so a swipe
 * over an answer option never selects it. One gesture moves at most one question (decided on touchend).
 */
export function useSwipeNavigation(ref: RefObject<HTMLElement | null>, { enabled, onNext, onPrev }: SwipeOptions) {
  const handlers = useRef({ onNext, onPrev })
  useEffect(() => { handlers.current = { onNext, onPrev } })

  useEffect(() => {
    const element = ref.current
    if (!element || !enabled) return

    let start: { x: number; y: number; time: number } | null = null
    let suppressClickUntil = 0

    const zoomed = () => (window.visualViewport?.scale ?? 1) > 1.01
    const excluded = (target: EventTarget | null) => {
      if (!(target instanceof Element)) return true
      if (target.closest('input, textarea, select, [contenteditable="true"], .exam-controls, [data-no-swipe]')) return true
      // Wide content (images, long formulas) that scrolls sideways keeps its own horizontal pan.
      for (let node: Element | null = target; node && node !== element; node = node.parentElement) {
        if (node.scrollWidth > node.clientWidth + 1 && /(auto|scroll)/.test(getComputedStyle(node).overflowX)) return true
      }
      return false
    }

    const onStart = (event: TouchEvent) => {
      start = event.touches.length === 1 && !zoomed() && !excluded(event.target)
        ? { x: event.touches[0].clientX, y: event.touches[0].clientY, time: event.timeStamp }
        : null
    }
    const onMove = (event: TouchEvent) => { if (event.touches.length > 1) start = null }
    const onEnd = (event: TouchEvent) => {
      const from = start
      start = null
      const touch = event.changedTouches[0]
      if (!from || !touch || event.touches.length > 0 || zoomed()) return
      if (window.getSelection()?.toString()) return
      const dx = touch.clientX - from.x
      const dy = touch.clientY - from.y
      if (Math.abs(dx) < MIN_DISTANCE || Math.abs(dx) < Math.abs(dy) * DIRECTION_RATIO) return
      if (event.timeStamp - from.time > MAX_DURATION_MS) return

      suppressClickUntil = event.timeStamp + 500
      const forward = dx < 0
      const moved = forward ? handlers.current.onNext() : handlers.current.onPrev()
      if (!moved && !window.matchMedia('(prefers-reduced-motion: reduce)').matches) {
        // Edge of the exam: a short nudge shows that the gesture was recognised.
        const offset = forward ? -12 : 12
        element.animate([{ transform: 'none' }, { transform: `translateX(${offset}px)` }, { transform: 'none' }], { duration: 220, easing: 'ease-out' })
      }
    }
    const onCancel = () => { start = null }
    const onClick = (event: MouseEvent) => {
      if (event.timeStamp < suppressClickUntil) {
        event.preventDefault()
        event.stopPropagation()
        suppressClickUntil = 0
      }
    }

    element.addEventListener('touchstart', onStart, { passive: true })
    element.addEventListener('touchmove', onMove, { passive: true })
    element.addEventListener('touchend', onEnd, { passive: true })
    element.addEventListener('touchcancel', onCancel, { passive: true })
    element.addEventListener('click', onClick, true)
    return () => {
      element.removeEventListener('touchstart', onStart)
      element.removeEventListener('touchmove', onMove)
      element.removeEventListener('touchend', onEnd)
      element.removeEventListener('touchcancel', onCancel)
      element.removeEventListener('click', onClick, true)
    }
  }, [enabled, ref])
}
