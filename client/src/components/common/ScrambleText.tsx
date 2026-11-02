import { useEffect, useRef } from 'react'

const LOWER = 'abcçdeəfgğhxıijkqlmnoöprsştuüvyz'
const UPPER = 'ABCÇDEƏFGĞHXIİJKQLMNOÖPRSŞTUÜVYZ'
const DIGITS = '0123456789'

/** Random glyph of the same kind (upper/lower/digit) so the scrambled word keeps roughly its final width. */
function glyph(target: string) {
  const pool = DIGITS.includes(target) ? DIGITS : target === target.toLocaleUpperCase('az') && target !== target.toLocaleLowerCase('az') ? UPPER : LOWER
  return pool[Math.floor(Math.random() * pool.length)]
}

/**
 * Decodes text from random letters into the real value, settling left to right.
 * Frames write straight to the DOM (no React renders); screen readers only get the real text.
 */
export function ScrambleText({ text, play = true, duration = 1000, className }: { text: string; play?: boolean; duration?: number; className?: string }) {
  const settledRef = useRef<HTMLSpanElement>(null)
  const scrambleRef = useRef<HTMLSpanElement>(null)

  useEffect(() => {
    const settled = settledRef.current
    const scramble = scrambleRef.current
    if (!settled || !scramble) return
    const chars = [...text] // code points, so multi-byte letters stay whole
    const reduce = window.matchMedia('(prefers-reduced-motion: reduce)').matches
    if (!play || reduce || chars.length === 0) {
      settled.textContent = text
      scramble.textContent = ''
      return
    }
    // Each character settles at its own time; the first quarter of the run is pure noise.
    const settleAt = chars.map((_, i) => duration * (0.25 + 0.75 * ((i + 1) / chars.length)))
    let frame = 0
    let last = 0
    const start = performance.now()
    const tick = (now: number) => {
      const elapsed = now - start
      if (now - last >= 45 || elapsed >= duration) { // ~22 glyph changes per second: lively without flicker
        last = now
        const done = settleAt.filter(t => elapsed >= t).length
        settled.textContent = chars.slice(0, done).join('')
        scramble.textContent = chars.slice(done).map(c => (c.trim() ? glyph(c) : c)).join('')
      }
      if (elapsed < duration) frame = requestAnimationFrame(tick)
      else { settled.textContent = text; scramble.textContent = '' }
    }
    frame = requestAnimationFrame(tick)
    return () => cancelAnimationFrame(frame)
  }, [text, play, duration])

  return (
    <span className={className}>
      <span className="sr-only">{text}</span>
      <span aria-hidden>
        <span ref={settledRef}>{play ? '' : text}</span>
        <span ref={scrambleRef} className="scramble-noise" />
      </span>
    </span>
  )
}
