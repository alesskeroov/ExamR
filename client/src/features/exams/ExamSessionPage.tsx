import './exam-flow.css'
import { answerDisplayText } from './answer-display'
import { useIsMutating } from '@tanstack/react-query'
import { AlertTriangle, Check, ChevronDown, ChevronLeft, ChevronRight, Clock, Loader2, Maximize2, Minimize2, X } from 'lucide-react'
import { useCallback, useEffect, useRef, useState } from 'react'
import { Link, Navigate, useNavigate, useParams, useSearchParams } from 'react-router'
import { Alert } from '../../components/common/Alert'
import { Button } from '../../components/common/Button'
import { cn } from '../../lib/utils'
import { NotificationBell } from '../friends/NotificationBell'
import {
  answerMutationKey,
  useAnswerQuestion,
  useExamSession,
  useFinishExam,
  type ExamSession,
  type ExamSessionQuestion,
} from './api'

export function ExamSessionPage() {
  const { id = '' } = useParams()
  const session = useExamSession(id)

  if (session.isPending) {
    return (
      <div className="flex min-h-dvh items-center justify-center text-slate-500">
        <Loader2 className="size-6 animate-spin" aria-label="Yüklənir" />
      </div>
    )
  }

  if (session.isError) {
    return (
      <div className="mx-auto max-w-lg px-4 py-16">
        <Alert title="İmtahan açılmadı">
          {session.error.message}{' '}
          <Link to="/exams" className="font-semibold underline">
            Tarixçəyə qayıt
          </Link>
        </Alert>
      </div>
    )
  }

  if (session.data.status === 'Completed') {
    return <Navigate to={`/exams/${id}/result`} replace />
  }

  return <ExamRunner session={session.data} />
}

function ExamRunner({ session }: { session: ExamSession }) {
  const navigate = useNavigate()
  const [searchParams, setSearchParams] = useSearchParams()
  const total = session.questions.length
  const index = Math.min(Math.max(Number(searchParams.get('q') ?? 1) - 1 || 0, 0), total - 1)
  const question = session.questions[index]
  const immediate = session.revealMode === 'Immediate'

  const answer = useAnswerQuestion(session.id)
  const finish = useFinishExam(session.id)
  const saving = useIsMutating({ mutationKey: answerMutationKey(session.id) }) > 0
  const [confirmOpen, setConfirmOpen] = useState(false)
  const [mapOpen, setMapOpen] = useState(false)

  const answered = session.questions.filter((q) => q.selectedOptionId).length
  const locked = immediate && question.selectedOptionId !== null

  const goTo = useCallback(
    (i: number) => {
      setSearchParams({ q: String(Math.min(Math.max(i, 0), total - 1) + 1) }, { replace: true })
      setMapOpen(false)
      window.scrollTo({ top: 0, behavior: 'instant' })
    },
    [setSearchParams, total],
  )

  const choose = useCallback(
    (optionId: string | null) => {
      if (locked || finish.isPending) return
      answer.mutate({ examQuestionId: question.examQuestionId, selectedOptionId: optionId })
    },
    [answer, finish.isPending, locked, question.examQuestionId],
  )

  const submit = useCallback(() => {
    if (finish.isPending || finish.isSuccess) return
    finish.mutate(undefined, { onSuccess: () => navigate(`/exams/${session.id}/result`, { replace: true }) })
  }, [finish, navigate, session.id])

  // Keyboard: ←/→ navigate, A–F or 1–6 choose an option.
  useEffect(() => {
    function onKey(event: KeyboardEvent) {
      if (confirmOpen || event.ctrlKey || event.metaKey || event.altKey) return
      if (event.target instanceof HTMLElement && event.target.closest('input, textarea, select')) return
      if (event.key === 'ArrowLeft') goTo(index - 1)
      else if (event.key === 'ArrowRight') goTo(index + 1)
      else {
        const key = event.key.toUpperCase()
        const position = /^[A-F]$/.test(key) ? key.charCodeAt(0) - 65 : /^[1-6]$/.test(key) ? Number(key) - 1 : -1
        const option = question.options[position]
        if (option) choose(option.id)
      }
    }
    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
  }, [choose, confirmOpen, goTo, index, question.options])

  // Warn before leaving while an answer is still being saved.
  useEffect(() => {
    if (!saving) return
    const warn = (event: BeforeUnloadEvent) => event.preventDefault()
    window.addEventListener('beforeunload', warn)
    return () => window.removeEventListener('beforeunload', warn)
  }, [saving])

  return (
    <div className="exam-flow exam-session min-h-dvh bg-slate-50">
      <header className="sticky top-0 z-20 border-b border-slate-200 bg-white/95 backdrop-blur">
        <div className="mx-auto flex min-h-16 max-w-[1100px] flex-wrap items-center gap-2 px-3 py-2 sm:flex-nowrap sm:px-4 lg:px-6">
          <Link to="/" className="hidden items-center gap-2 font-semibold sm:flex" aria-label="ExamR ana səhifə">
            <img src="/favicon.svg" alt="" className="size-7" />
          </Link>
          <div className="min-w-0 flex-1 basis-[calc(100%-12rem)]">
            <div className="flex items-baseline justify-between gap-2 text-slate-600">
              <span className="font-medium text-slate-900 tabular-nums">
                Sual {index + 1} / {total}
              </span>
              <span className="hidden tabular-nums sm:inline">{answered} cavablanıb</span>
            </div>
            <div
              className="mt-1.5 h-1.5 overflow-hidden rounded-full bg-slate-100"
              role="progressbar"
              aria-label="Cavablanan suallar"
              aria-valuemin={0}
              aria-valuemax={total}
              aria-valuenow={answered}
            >
              <div className="h-full rounded-full bg-indigo-600 transition-[width] duration-300" style={{ width: `${(answered / total) * 100}%` }} />
            </div>
          </div>
          <ExamTimer expiresAt={session.expiresAt} serverNow={session.serverNow} onExpire={submit} />
          <FullscreenToggle />
          <span className="hidden sm:block"><NotificationBell /></span>
          <Button onClick={() => setConfirmOpen(true)} loading={finish.isPending} className="h-11 shrink-0 px-3 text-sm sm:px-4">
            Bitir
          </Button>
        </div>
      </header>

      <main className="mx-auto grid max-w-[1100px] gap-3 px-3 pt-4 pb-28 lg:pb-6 lg:grid-cols-[minmax(0,1fr)_232px] lg:gap-5 lg:px-6">
        <div className="flex min-w-0 flex-col gap-4">
          {finish.isError && <Alert title="İmtahan bitirilmədi">{finish.error.message}</Alert>}

          <QuestionPanel
            key={question.examQuestionId}
            question={question}
            number={index + 1}
            immediate={immediate}
            locked={locked}
            onChoose={choose}
          />

          <div className="exam-controls fixed inset-x-0 bottom-0 z-10 grid grid-cols-2 gap-2 border-t px-3 pt-3 lg:static lg:mx-0 lg:rounded-xl lg:border lg:p-3">
            <Button variant="secondary" disabled={index === 0} onClick={() => goTo(index - 1)}>
              <ChevronLeft className="size-4" aria-hidden />
              Əvvəlki
            </Button>
            {index < total - 1 ? (
              <Button variant="secondary" onClick={() => goTo(index + 1)}>
                Növbəti
                <ChevronRight className="size-4" aria-hidden />
              </Button>
            ) : (
              <Button onClick={() => setConfirmOpen(true)}>Bitir</Button>
            )}
            <div className="col-span-2 min-w-0"><SaveStatus saving={saving} error={answer.isError ? answer.error.message : null} /></div>
          </div>
        </div>

        <QuestionNavigator questions={session.questions} current={index} immediate={immediate} onSelect={goTo} open={mapOpen} onToggle={() => setMapOpen(value => !value)} />
      </main>

      <FinishDialog
        open={confirmOpen}
        unanswered={total - answered}
        onCancel={() => setConfirmOpen(false)}
        onConfirm={() => {
          setConfirmOpen(false)
          submit()
        }}
      />
    </div>
  )
}

interface QuestionPanelProps {
  question: ExamSessionQuestion
  number: number
  immediate: boolean
  locked: boolean
  onChoose: (optionId: string | null) => void
}

function QuestionPanel({ question, number, immediate, locked, onChoose }: QuestionPanelProps) {
  const reveal = question.reveal
  const headingId = `question-${question.examQuestionId}`

  return (
    <section aria-labelledby={headingId} className="min-w-0 rounded-xl border border-slate-200 bg-white p-4 sm:p-5">
      <p className="mb-2 text-xs font-medium text-slate-500">Sual {number}</p>
      <h1 id={headingId} className="exam-question-text text-base leading-7 font-semibold text-slate-900 sm:text-lg">
        {question.text}
      </h1>

      <div role="radiogroup" aria-labelledby={headingId} className="mt-4 flex flex-col gap-2">
        {question.options.map((option) => {
          const selected = question.selectedOptionId === option.id
          const isCorrect = reveal?.correctOptionId === option.id
          const isWrongPick = !!reveal?.isGradable && selected && !isCorrect

          return (
            <button
              key={option.id}
              type="button"
              role="radio"
              aria-checked={selected}
              aria-disabled={locked || undefined}
              onClick={() => onChoose(option.id)}
              className={cn(
                'flex min-h-11 w-full items-start gap-2.5 rounded-xl border px-3 py-2.5 text-left text-sm text-slate-700 transition-colors duration-150 motion-reduce:transition-none sm:gap-3 sm:px-4 sm:text-[15px]',
                isCorrect && 'border-emerald-300 bg-emerald-50/60',
                isWrongPick && 'border-rose-300 bg-rose-50/60',
                !reveal && selected && 'border-indigo-400 bg-indigo-50/60',
                !reveal && !selected && 'border-slate-200 hover:border-slate-400 hover:bg-slate-50',
                reveal && !isCorrect && !isWrongPick && 'border-slate-200',
                locked && 'cursor-default',
              )}
            >
              <span
                className={cn(
                  'flex size-6 shrink-0 items-center justify-center rounded-md border text-xs font-medium',
                  isCorrect ? 'border-emerald-200 bg-emerald-50 text-emerald-800'
                    : isWrongPick ? 'border-rose-200 bg-rose-50 text-rose-800'
                      : selected ? 'border-indigo-200 bg-indigo-50 text-indigo-700'
                        : 'border-slate-200 bg-slate-50 text-slate-500',
                )}
                aria-hidden
              >
                {option.label}
              </span>
              <span className="exam-question-text min-w-0 flex-1 leading-6">
                {answerDisplayText(option.text)}
                {isCorrect && <span className="sr-only"> (düzgün cavab)</span>}
                {isWrongPick && <span className="sr-only"> (sizin cavabınız, yanlış)</span>}
              </span>
              {isCorrect && <Check className="mt-1 size-4 shrink-0 text-emerald-700" aria-hidden />}
              {isWrongPick && <X className="mt-1 size-4 shrink-0 text-rose-700" aria-hidden />}
              {selected && !reveal && <Check className="mt-1 size-4 shrink-0 text-indigo-600" aria-hidden />}
            </button>
          )
        })}
      </div>

      {reveal && <RevealNote reveal={reveal} selectedOptionId={question.selectedOptionId} />}

      {!immediate && question.selectedOptionId && (
        <button type="button" onClick={() => onChoose(null)} className="mt-2 min-h-11 text-sm text-slate-500 hover:text-slate-800">
          Cavabı təmizlə
        </button>
      )}
    </section>
  )
}

function RevealNote({ reveal, selectedOptionId }: { reveal: NonNullable<ExamSessionQuestion['reveal']>; selectedOptionId: string | null }) {
  if (!reveal.isGradable) {
    return (
      <p role="status" className="mt-4 flex items-start gap-2 rounded-lg bg-amber-50 px-3 py-2 text-amber-800">
        <AlertTriangle className="mt-0.5 size-4 shrink-0" aria-hidden />
        Bu sualın düzgün cavabı məlum deyil, ballanmayacaq.
      </p>
    )
  }

  const correct = reveal.correctOptionId === selectedOptionId
  return (
    <p role="status" className={cn('mt-4 rounded-lg px-3 py-2 font-medium', correct ? 'bg-emerald-50 text-emerald-800' : 'bg-rose-50 text-rose-800')}>
      {correct ? 'Düzgün cavab!' : 'Yanlış cavab. Düzgün variant yaşılla göstərilib.'}
    </p>
  )
}

function SaveStatus({ saving, error }: { saving: boolean; error: string | null }) {
  return (
    <p aria-live="polite" className={cn('text-center text-xs', error ? 'text-rose-600' : 'text-slate-500')}>
      {saving ? 'Saxlanılır…' : error ? `Saxlanmadı: ${error}` : ''}
    </p>
  )
}

interface QuestionNavigatorProps {
  questions: ExamSessionQuestion[]
  current: number
  immediate: boolean
  onSelect: (index: number) => void
  open: boolean
  onToggle: () => void
}

function QuestionNavigator({ questions, current, immediate, onSelect, open, onToggle }: QuestionNavigatorProps) {
  return (
    <nav aria-label="Suallar" className="order-first min-w-0 rounded-xl border border-slate-200 bg-white p-3 lg:order-last lg:sticky lg:top-24 lg:self-start">
      <button type="button" className="exam-panel-toggle min-h-11 w-full items-center justify-between gap-2 text-sm font-medium" aria-expanded={open} aria-controls="exam-question-map" onClick={onToggle}>
        <span>Suallar · {current + 1}/{questions.length}</span><span className="inline-flex items-center gap-2 text-xs text-slate-500">{questions.filter(q => q.selectedOptionId).length} cavablanıb<ChevronDown className={cn('size-4 transition-transform', open && 'rotate-180')} aria-hidden /></span>
      </button>
      <h2 className="mb-3 hidden text-sm font-semibold lg:block">Suallar</h2>
      <div id="exam-question-map" className="exam-map-content" data-open={open}>
      <ol className="exam-question-map grid grid-cols-[repeat(auto-fill,minmax(44px,1fr))] gap-1.5">
        {questions.map((q, i) => {
          const reveal = immediate ? q.reveal : null
          const state = !q.selectedOptionId
            ? 'empty'
            : !reveal ? 'answered'
              : !reveal.isGradable ? 'ungraded'
                : reveal.correctOptionId === q.selectedOptionId ? 'correct' : 'wrong'

          return (
            <li key={q.examQuestionId}>
              <button
                type="button"
                onClick={() => onSelect(i)}
                aria-current={i === current ? 'step' : undefined}
                aria-label={`Sual ${i + 1}${q.selectedOptionId ? ', cavablanıb' : ''}`}
                className={cn(
                  'flex h-11 w-full items-center justify-center rounded-md border text-xs font-semibold tabular-nums transition-colors duration-150',
                  state === 'empty' && 'border-slate-200 bg-white text-slate-600 hover:bg-slate-50',
                  state === 'answered' && 'border-indigo-600 bg-indigo-600 text-white',
                  state === 'correct' && 'border-emerald-600 bg-emerald-600 text-white',
                  state === 'wrong' && 'border-rose-600 bg-rose-600 text-white',
                  state === 'ungraded' && 'border-amber-400 bg-amber-100 text-amber-900',
                  i === current && 'ring-2 ring-indigo-500 ring-offset-2',
                )}
              >
                {i + 1}
              </button>
            </li>
          )
        })}
      </ol>
      <div className="mt-4 flex flex-wrap gap-x-4 gap-y-1 text-xs text-slate-500">
        <Legend className="border-slate-300 bg-white" label="Boş" />
        {immediate ? (
          <>
            <Legend className="border-emerald-600 bg-emerald-600" label="Düzgün" />
            <Legend className="border-rose-600 bg-rose-600" label="Yanlış" />
          </>
        ) : (
          <Legend className="border-indigo-600 bg-indigo-600" label="Cavablanıb" />
        )}
      </div>
      <p className="mt-3 hidden text-xs text-slate-400 lg:block">Klaviatura: ← → keçid, A–F seçim</p>
      </div>
    </nav>
  )
}

function Legend({ className, label }: { className: string; label: string }) {
  return (
    <span className="inline-flex items-center gap-1.5">
      <span className={cn('size-3 rounded-sm border', className)} aria-hidden />
      {label}
    </span>
  )
}

/** Counts down to the server deadline, corrected for the difference between client and server clocks. */
function ExamTimer({ expiresAt, serverNow, onExpire }: { expiresAt: string; serverNow: string; onExpire: () => void }) {
  const [offset] = useState(() => Date.parse(serverNow) - Date.now())
  const [remaining, setRemaining] = useState(() => Date.parse(expiresAt) - (Date.now() + offset))
  const expiredRef = useRef(false)

  useEffect(() => {
    const timer = window.setInterval(() => setRemaining(Date.parse(expiresAt) - (Date.now() + offset)), 500)
    return () => window.clearInterval(timer)
  }, [expiresAt, offset])

  useEffect(() => {
    if (remaining <= 0 && !expiredRef.current) {
      expiredRef.current = true
      onExpire()
    }
  }, [onExpire, remaining])

  const seconds = Math.max(0, Math.ceil(remaining / 1000))
  const hours = Math.floor(seconds / 3600)
  const mm = String(Math.floor((seconds % 3600) / 60)).padStart(2, '0')
  const ss = String(seconds % 60).padStart(2, '0')
  const label = hours > 0 ? `${hours}:${mm}:${ss}` : `${mm}:${ss}`
  const minutesLeft = Math.ceil(seconds / 60)

  return (
    <div
      className={cn(
        'flex items-center gap-1.5 rounded-lg px-2.5 py-1.5 font-semibold tabular-nums',
        seconds <= 60 ? 'bg-rose-50 text-rose-700' : seconds <= 300 ? 'bg-amber-50 text-amber-800' : 'bg-slate-100 text-slate-700',
      )}
    >
      <Clock className="size-4" aria-hidden />
      <span>{label}</span>
      {/* Announce only at the 5- and 1-minute marks, not every second. */}
      <span className="sr-only" aria-live="polite">
        {minutesLeft === 5 || minutesLeft === 1 ? `${minutesLeft} dəqiqə qalıb` : ''}
      </span>
    </div>
  )
}

interface FinishDialogProps {
  open: boolean
  unanswered: number
  onCancel: () => void
  onConfirm: () => void
}

function FinishDialog({ open, unanswered, onCancel, onConfirm }: FinishDialogProps) {
  const ref = useRef<HTMLDialogElement>(null)

  useEffect(() => {
    const dialog = ref.current
    if (!dialog) return
    if (open && !dialog.open) dialog.showModal()
    if (!open && dialog.open) dialog.close()
  }, [open])

  return (
    <dialog
      ref={ref}
      onCancel={(e) => {
        e.preventDefault()
        onCancel()
      }}
      aria-labelledby="finish-title"
      className="m-auto w-[calc(100%-2rem)] max-w-md rounded-xl border border-slate-200 bg-white p-6 shadow-xl backdrop:bg-slate-900/40"
    >
      <h2 id="finish-title" className="text-lg font-semibold">
        İmtahanı bitirmək istəyirsiniz?
      </h2>
      <p className="mt-2 text-slate-600">
        {unanswered > 0 ? `${unanswered} sual cavabsızdır. Bitirdikdən sonra cavabları dəyişmək mümkün olmayacaq.` : 'Bütün suallar cavablanıb.'}
      </p>
      <div className="mt-6 flex justify-end gap-2">
        <Button variant="secondary" onClick={onCancel}>
          Davam et
        </Button>
        <Button onClick={onConfirm}>Bitir</Button>
      </div>
    </dialog>
  )
}

/** Optional distraction-free mode; browsers only allow it after a click, so it is offered rather than forced. */
function FullscreenToggle() {
  const [active, setActive] = useState(() => !!document.fullscreenElement)
  useEffect(() => {
    const update = () => setActive(!!document.fullscreenElement)
    document.addEventListener('fullscreenchange', update)
    return () => document.removeEventListener('fullscreenchange', update)
  }, [])
  // Leave fullscreen when the exam screen closes (finish, navigation).
  useEffect(() => () => { if (document.fullscreenElement) void document.exitFullscreen().catch(() => undefined) }, [])
  if (!document.fullscreenEnabled) return null
  return (
    <button type="button" onClick={() => (active ? document.exitFullscreen() : document.documentElement.requestFullscreen()).catch(() => undefined)}
      aria-pressed={active} aria-label={active ? 'Tam ekrandan çıx' : 'Tam ekran'} title={active ? 'Tam ekrandan çıx' : 'Tam ekran'}
      className="hidden size-9 shrink-0 place-items-center rounded-lg text-slate-500 hover:bg-slate-100 hover:text-slate-900 sm:grid">
      {active ? <Minimize2 className="size-4" aria-hidden /> : <Maximize2 className="size-4" aria-hidden />}
    </button>
  )
}
