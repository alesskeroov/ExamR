import { useEffect, useRef, useState } from 'react'
import { Check, FileText } from 'lucide-react'
import './landing.css'

// Sample questions the preview cycles through; illustrative content only.
const samples = [
  { q: 'OSI modelində IP ünvanlaması hansı səviyyədə həyata keçirilir?', options: ['Fiziki səviyyə', 'Kanal səviyyəsi', 'Şəbəkə səviyyəsi', 'Nəqliyyat səviyyəsi'], answer: 2 },
  { q: 'Veb səhifələri şifrələnmiş şəkildə hansı protokol ötürür?', options: ['HTTP', 'FTP', 'HTTPS', 'SMTP'], answer: 2 },
  { q: 'SQL-də cədvəldən məlumat seçmək üçün hansı əmr istifadə olunur?', options: ['SELECT', 'INSERT', 'UPDATE', 'DELETE'], answer: 0 },
  { q: 'Hansı məlumat strukturu LIFO prinsipi ilə işləyir?', options: ['Növbə', 'Stek', 'Massiv', 'Ağac'], answer: 1 },
]
const TICKS_PER_QUESTION = 4 // one tick per second: show question, pick an answer, pause, next
const START_NUMBER = 7
const START_SECONDS = 18 * 60 + 42
const pad = (n: number) => String(n).padStart(2, '0')

/** Illustrative ExamR screen built from the real exam layout; the content is sample data, labelled as such. */
export function ProductPreview() {
  const stage = useRef<HTMLDivElement>(null)
  const [tick, setTick] = useState(0)

  // Animate only while the preview is on screen; stay static for reduced motion.
  useEffect(() => {
    const el = stage.current
    if (!el || window.matchMedia('(prefers-reduced-motion: reduce)').matches || !('IntersectionObserver' in window)) return
    let timer: number | undefined
    const observer = new IntersectionObserver(([entry]) => {
      window.clearInterval(timer)
      if (entry.isIntersecting) timer = window.setInterval(() => setTick(t => t + 1), 1000)
    })
    observer.observe(el)
    return () => { observer.disconnect(); window.clearInterval(timer) }
  }, [])

  const step = Math.floor(tick / TICKS_PER_QUESTION)
  const picked = tick % TICKS_PER_QUESTION >= 2
  const sample = samples[step % samples.length]
  const number = START_NUMBER + (step % 12) // stays within the 20-question grid
  const done = number - 1 + (picked ? 1 : 0)
  const seconds = Math.max(0, START_SECONDS - tick)

  return (
    <div ref={stage} className="lp-stage" aria-label="ExamR interfeysinin nümunəsi" role="img">
      <div className="lp-sheetdoc" aria-hidden>
        <div className="flex items-center justify-between gap-2">
          <span className="flex items-center gap-1.5 text-[12px] font-semibold"><FileText className="size-3.5" />informatika-final.pdf</span>
          <span className="lp-chip"><Check className="size-3" strokeWidth={3} />48 sual</span>
        </div>
        <div className="lp-sheetdoc-line" style={{ width: '92%' }} />
        <div className="lp-sheetdoc-line" style={{ width: '78%' }} />
        <p key={step} className="lp-sheetdoc-q lp-swap">{number}. {sample.q}</p>
        <div className="lp-sheetdoc-line" style={{ width: '64%' }} />
        <div className="lp-sheetdoc-line" style={{ width: '85%' }} />
        <div className="lp-sheetdoc-line" style={{ width: '40%' }} />
      </div>

      <div className="lp-examcard" aria-hidden>
        <div className="lp-examcard-head">
          <span className="lp-mono">Sual {pad(number)} / 20</span>
          <span className="lp-timer lp-mono">{pad(Math.floor(seconds / 60))}:{pad(seconds % 60)}</span>
        </div>
        <div key={step} className="lp-examcard-body">
          <p className="lp-q lp-swap">{sample.q}</p>
          <div className="lp-options">
            {sample.options.map((option, i) => <div key={option} className="lp-option lp-swap" style={{ animationDelay: `${60 + i * 50}ms` }}
              data-state={picked && i === sample.answer ? 'picked' : undefined}><b>{'ABCD'[i]}</b>{option}</div>)}
          </div>
        </div>
        <div className="lp-grid20">{Array.from({ length: 20 }, (_, i) => <i key={i} data-s={i < done ? 'done' : i === number - 1 ? 'now' : 'todo'} />)}</div>
      </div>

      <div className="lp-float" aria-hidden>
        <span key={done} className="lp-mono lp-swap font-semibold">{done}/{done}</span>
        <span>düzgün · bu sessiya</span>
      </div>
      <span className="lp-tag">nümunə interfeys</span>
    </div>
  )
}
