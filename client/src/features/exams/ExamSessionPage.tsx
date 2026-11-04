import './exam-flow.css'
import { answerDisplayText } from './answer-display'
import { useIsMutating } from '@tanstack/react-query'
import { AlertTriangle, Bookmark, BookmarkCheck, Check, ChevronLeft, ChevronRight, Clock, Eye, Flag, LayoutGrid, Loader2, Maximize2, Minimize2, X } from 'lucide-react'
import { useCallback, useEffect, useRef, useState } from 'react'
import { Link, Navigate, useNavigate, useParams, useSearchParams } from 'react-router'
import { Alert } from '../../components/common/Alert'
import { Button } from '../../components/common/Button'
import { cn } from '../../lib/utils'
import { NotificationBell } from '../friends/NotificationBell'
import {
  answerMutationKey,
  useAnswerQuestion,
  useExamHistory,
  useExamSession,
  useFinishExam,
  type ExamSession,
  type ExamSessionQuestion,
} from './api'
import { sourceName } from './examSource'

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
  const [revealed, toggleRevealed] = useStoredSet(`exam-revealed:${session.id}`)
  const [flagged, toggleFlag] = useStoredSet(`exam-flagged:${session.id}`)
  // The session has no title; the exam is also listed (newest first) in the history, which names its PDF.
  const history = useExamHistory(1)
  const summary = history.data?.items.find(e => e.id === session.id)
  const title = summary ? sourceName(summary.source) : null

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
      if (event.target instanceof HTMLElement && event.target.closest('input, textarea, select, .notify-root')) return
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

  // Mobile question sheet: lock the page behind it and close on Escape; the scroll position is kept.
  useEffect(() => {
    if (!mapOpen) return
    const close = (event: KeyboardEvent) => { if (event.key === 'Escape') setMapOpen(false) }
    const overflow = document.body.style.overflow
    document.body.style.overflow = 'hidden'
    window.addEventListener('keydown', close)
    return () => { document.body.style.overflow = overflow; window.removeEventListener('keydown', close) }
  }, [mapOpen])

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
        <div className="exam-shell mx-auto flex min-h-14 items-center gap-2 px-3 py-2 sm:min-h-16 sm:gap-3 sm:px-4 lg:px-6">
          <Link to="/" className="hidden items-center gap-2 font-semibold sm:flex" aria-label="ExamR ana səhifə">
            <img src="/favicon.svg" alt="" className="size-7" />
          </Link>
          <div className="min-w-0 flex-1">
            <p className="truncate text-xs leading-5 text-slate-500" title={title ?? undefined}>{title ?? 'İmtahan'}</p>
            <p className="truncate text-[15px] leading-5 font-semibold whitespace-nowrap text-slate-900 tabular-nums">
              Sual {index + 1}<span className="font-normal text-slate-500"> / {total}</span>
              <span className="hidden text-sm font-normal text-slate-500 sm:inline"> · {answered} cavablanıb</span>
            </p>
          </div>
          <ExamTimer expiresAt={session.expiresAt} serverNow={session.serverNow} onExpire={submit} />
          <FullscreenToggle />
          <div className="hidden sm:block"><NotificationBell safe /></div>
          <Button variant="secondary" onClick={() => setConfirmOpen(true)} loading={finish.isPending} className="h-10 shrink-0 px-3 text-sm sm:px-4">
            <Flag className="hidden size-4 sm:block" aria-hidden />
            Bitir
          </Button>
        </div>
        <div className="exam-progress h-1" role="progressbar" aria-label="Cavablanan suallar" aria-valuemin={0} aria-valuemax={total} aria-valuenow={answered}>
          <div className="h-full bg-indigo-600 transition-[width] duration-300" style={{ width: `${(answered / total) * 100}%` }} />
        </div>
      </header>

      <main className="exam-shell exam-layout mx-auto grid gap-3 px-3 pt-3 sm:px-4 sm:pt-4 lg:gap-6 lg:px-6 lg:pt-6">
        <div className="flex min-w-0 flex-col gap-4">
          {finish.isError && <Alert title="İmtahan bitirilmədi">{finish.error.message}</Alert>}

          <QuestionPanel
            key={question.examQuestionId}
            question={question}
            number={index + 1}
            immediate={immediate}
            locked={locked}
            revealed={revealed.has(question.examQuestionId)}
            onReveal={() => toggleRevealed(question.examQuestionId, true)}
            flagged={flagged.has(question.examQuestionId)}
            onFlag={() => toggleFlag(question.examQuestionId)}
            onChoose={choose}
          />

          <div className="exam-controls fixed inset-x-0 bottom-0 z-20 border-t px-3 pt-2.5 lg:static lg:border-0 lg:bg-transparent lg:p-0">
            <div className="relative mx-auto grid max-w-2xl grid-cols-[1fr_auto_1fr] items-center gap-2 lg:flex lg:max-w-none">
              <SaveStatus saving={saving} error={answer.isError ? answer.error.message : null} />
              <Button variant="secondary" disabled={index === 0} onClick={() => goTo(index - 1)} className="exam-nav-button min-w-0 gap-1 px-2 whitespace-nowrap sm:gap-2 sm:px-3 lg:order-1">
                <ChevronLeft className="size-4 shrink-0" aria-hidden />
                Əvvəlki
              </Button>
              <button type="button" onClick={() => setMapOpen(true)} aria-haspopup="dialog" aria-expanded={mapOpen} aria-controls="exam-question-map"
                className="exam-map-button inline-flex h-11 items-center justify-center gap-1.5 rounded-xl px-3 text-sm font-semibold tabular-nums lg:hidden">
                <LayoutGrid className="size-4" aria-hidden />
                <span className="sr-only">Suallar, </span>{index + 1}/{total}
              </button>
              {index < total - 1 ? (
                <Button onClick={() => goTo(index + 1)} className="exam-nav-button min-w-0 gap-1 px-2 whitespace-nowrap sm:gap-2 sm:px-3 lg:order-3 lg:ml-auto">
                  Növbəti
                  <ChevronRight className="size-4 shrink-0" aria-hidden />
                </Button>
              ) : (
                <Button onClick={() => setConfirmOpen(true)} className="exam-nav-button min-w-0 gap-1 px-2 whitespace-nowrap sm:gap-2 sm:px-3 lg:order-3 lg:ml-auto">
                  <Flag className="size-4 shrink-0" aria-hidden />
                  Bitir
                </Button>
              )}
            </div>
          </div>
        </div>

        <QuestionNavigator questions={session.questions} current={index} immediate={immediate} revealed={revealed} flagged={flagged}
          onSelect={goTo} open={mapOpen} onClose={() => setMapOpen(false)} />
      </main>

      <FinishDialog
        open={confirmOpen}
        answered={answered}
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
  revealed: boolean
  onReveal: () => void
  flagged: boolean
  onFlag: () => void
  onChoose: (optionId: string | null) => void
}

function QuestionPanel({ question, number, immediate, locked, revealed, onReveal, flagged, onFlag, onChoose }: QuestionPanelProps) {
  // Immediate mode gets the result with the saved answer, but shows it only after the student asks for it.
  const reveal = revealed ? question.reveal : null
  const headingId = `question-${question.examQuestionId}`

  return (
    <section aria-labelledby={headingId} className="exam-question-enter min-w-0 rounded-xl border border-slate-200 bg-white px-3.5 pt-2 pb-4 sm:px-6 sm:pb-6 lg:px-7 lg:pb-7">
      <div className="mb-1 flex min-h-10 items-center justify-between gap-2 sm:mt-2">
        <p className="text-xs font-medium tracking-wide text-slate-500 uppercase">Sual {number}</p>
        <button type="button" onClick={onFlag} aria-pressed={flagged}
          className={cn('-mr-2 inline-flex h-10 items-center gap-1.5 rounded-lg px-2.5 text-xs font-medium transition-colors', flagged ? 'text-amber-800 hover:bg-amber-50' : 'text-slate-500 hover:bg-slate-100 hover:text-slate-800')}>
          {flagged ? <BookmarkCheck className="size-4" aria-hidden /> : <Bookmark className="size-4" aria-hidden />}
          {flagged ? 'İşarələnib' : 'İşarələ'}
        </button>
      </div>
      <h1 id={headingId} className="exam-question-text max-w-[72ch] text-base leading-7 font-semibold text-slate-900 sm:text-[17px] lg:text-lg lg:leading-8">
        {question.text}
      </h1>

      <div role="radiogroup" aria-labelledby={headingId} className="mt-5 flex flex-col gap-2 sm:gap-2.5">
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
                'exam-option flex min-h-12 w-full items-start gap-3 rounded-xl border px-3 py-3 text-left text-sm text-slate-700 sm:px-4 sm:text-[15px]',
                isCorrect && 'border-emerald-300 bg-emerald-50/60',
                isWrongPick && 'border-rose-300 bg-rose-50/60',
                !reveal && selected && 'border-indigo-500 bg-indigo-50 text-slate-900',
                !reveal && !selected && 'border-slate-200 hover:border-slate-300 hover:bg-slate-50',
                reveal && !isCorrect && !isWrongPick && 'border-slate-200',
                locked && 'cursor-default',
              )}
            >
              <span
                className={cn(
                  'flex size-6 shrink-0 items-center justify-center rounded-md border text-xs font-semibold transition-colors duration-150',
                  isCorrect ? 'border-emerald-200 bg-emerald-50 text-emerald-800'
                    : isWrongPick ? 'border-rose-200 bg-rose-50 text-rose-800'
                      : selected ? 'border-indigo-600 bg-indigo-600 text-white'
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
              <Check className={cn('mt-1 size-4 shrink-0 text-indigo-600', !(selected && !reveal) && 'invisible')} aria-hidden />
            </button>
          )
        })}
      </div>

      {reveal && <RevealNote reveal={reveal} selectedOptionId={question.selectedOptionId} />}

      {!reveal && question.reveal && (
        <div className="mt-4 flex flex-col gap-2 sm:flex-row sm:items-center sm:justify-between">
          <p className="text-sm text-slate-500">Cavabın yadda saxlanıldı.</p>
          <Button variant="secondary" onClick={onReveal} className="h-11 w-full sm:w-auto">
            <Eye className="size-4" aria-hidden />
            Cavabı göstər
          </Button>
        </div>
      )}

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
    <p aria-live="polite" className={cn('exam-save-status pointer-events-none absolute inset-x-0 bottom-full mb-2 text-center text-xs lg:static lg:order-2 lg:mb-0 lg:flex-1', error ? 'text-rose-600' : 'text-slate-500')}>
      {saving ? 'Saxlanılır…' : error ? `Saxlanmadı: ${error}` : ''}
    </p>
  )
}

interface QuestionNavigatorProps {
  questions: ExamSessionQuestion[]
  current: number
  immediate: boolean
  revealed: ReadonlySet<string>
  flagged: ReadonlySet<string>
  onSelect: (index: number) => void
  open: boolean
  onClose: () => void
}

/** Sidebar from laptop width; on smaller screens a bottom sheet opened from the action bar. */
function QuestionNavigator({ questions, current, immediate, revealed, flagged, onSelect, open, onClose }: QuestionNavigatorProps) {
  const answeredCount = questions.filter(q => q.selectedOptionId).length
  return (
    <>
      <div aria-hidden onClick={onClose} className="exam-sheet-backdrop" data-open={open} />
      <nav id="exam-question-map" aria-label="Suallar" data-open={open}
        className="exam-sheet min-w-0 border border-slate-200 bg-white p-4 lg:order-last lg:sticky lg:top-24 lg:self-start">
        <span className="exam-sheet-handle" aria-hidden />
        <div className="mb-3 flex items-center gap-2">
          <h2 className="text-sm font-semibold">Suallar</h2>
          <span className="ml-auto text-xs text-slate-500 tabular-nums">{answeredCount} cavablanıb · {questions.length - answeredCount} boş</span>
          <button type="button" onClick={onClose} aria-label="Sualları bağla"
            className="-mr-2 grid size-10 shrink-0 place-items-center rounded-lg text-slate-500 hover:bg-slate-100 hover:text-slate-900 lg:hidden">
            <X className="size-5" aria-hidden />
          </button>
        </div>
        <ol className="exam-question-map grid grid-cols-[repeat(auto-fill,minmax(44px,1fr))] gap-1.5 lg:grid-cols-[repeat(auto-fill,minmax(40px,1fr))]">
          {questions.map((q, i) => {
            const reveal = immediate && revealed.has(q.examQuestionId) ? q.reveal : null
            const isFlagged = flagged.has(q.examQuestionId)
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
                  aria-label={`Sual ${i + 1}${q.selectedOptionId ? ', cavablanıb' : ''}${isFlagged ? ', işarələnib' : ''}`}
                  className={cn(
                    'relative flex h-11 w-full items-center justify-center rounded-md border text-xs font-semibold tabular-nums transition-colors duration-150 lg:h-9',
                    state === 'empty' && 'border-slate-200 bg-white text-slate-600 hover:border-slate-300 hover:bg-slate-50',
                    state === 'answered' && 'border-indigo-600 bg-indigo-600 text-white',
                    state === 'correct' && 'border-emerald-600 bg-emerald-600 text-white',
                    state === 'wrong' && 'border-rose-600 bg-rose-600 text-white',
                    state === 'ungraded' && 'border-amber-400 bg-amber-100 text-amber-900',
                    i === current && 'ring-2 ring-[var(--app-ink)] ring-offset-2 ring-offset-[var(--app-surface)]',
                  )}
                >
                  {i + 1}
                  {isFlagged && <span className="exam-flag-dot" aria-hidden />}
                </button>
              </li>
            )
          })}
        </ol>
        <div className="mt-4 flex flex-wrap gap-x-4 gap-y-1.5 text-xs text-slate-500">
          <Legend className="border-slate-300 bg-white" label="Boş" />
          <Legend className="border-indigo-600 bg-indigo-600" label="Cavablanıb" />
          {immediate && (
            <>
              <Legend className="border-emerald-600 bg-emerald-600" label="Düzgün" />
              <Legend className="border-rose-600 bg-rose-600" label="Yanlış" />
            </>
          )}
          <Legend className="rounded-full border-amber-500 bg-amber-500" label="İşarələnmiş" />
        </div>
        <p className="mt-3 hidden text-xs text-slate-400 lg:block">Klaviatura: ← → keçid, A–F seçim</p>
      </nav>
    </>
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
        'flex min-w-[5.25rem] shrink-0 items-center justify-center gap-1.5 rounded-lg px-2.5 py-1.5 font-semibold whitespace-nowrap tabular-nums transition-colors duration-300',
        seconds <= 60 ? 'bg-rose-50 text-rose-700' : seconds <= 300 ? 'bg-amber-50 text-amber-800' : 'bg-slate-100 text-slate-700',
      )}
    >
      <Clock className="size-4 shrink-0" aria-hidden />
      <span aria-label={`Qalan vaxt ${label}`}>{label}</span>
      {/* Announce only at the 5- and 1-minute marks, not every second. */}
      <span className="sr-only" aria-live="polite">
        {minutesLeft === 5 || minutesLeft === 1 ? `${minutesLeft} dəqiqə qalıb` : ''}
      </span>
    </div>
  )
}

interface FinishDialogProps {
  open: boolean
  answered: number
  unanswered: number
  onCancel: () => void
  onConfirm: () => void
}

function FinishDialog({ open, answered, unanswered, onCancel, onConfirm }: FinishDialogProps) {
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
      aria-describedby="finish-description"
      className="exam-dialog m-auto w-[calc(100%-2rem)] max-w-md rounded-xl border border-slate-200 bg-white p-5 shadow-xl backdrop:bg-slate-900/40 sm:p-6"
    >
      <h2 id="finish-title" className="text-lg font-semibold">
        İmtahanı bitirmək istəyirsiniz?
      </h2>
      <dl className="mt-4 grid grid-cols-2 gap-2 text-center">
        <div className="rounded-lg bg-slate-50 px-3 py-2"><dt className="text-xs text-slate-500">Cavablanıb</dt><dd className="text-lg font-semibold tabular-nums">{answered}</dd></div>
        <div className={cn('rounded-lg px-3 py-2', unanswered > 0 ? 'bg-amber-50' : 'bg-slate-50')}><dt className="text-xs text-slate-500">Boş</dt><dd className={cn('text-lg font-semibold tabular-nums', unanswered > 0 && 'text-amber-800')}>{unanswered}</dd></div>
      </dl>
      <p id="finish-description" className="mt-3 text-sm text-slate-600">
        {unanswered > 0 ? `${unanswered} sual cavabsızdır. Bitirdikdən sonra cavabları dəyişmək mümkün olmayacaq.` : 'Bütün suallar cavablanıb. Bitirdikdən sonra cavabları dəyişmək mümkün olmayacaq.'}
      </p>
      <div className="mt-6 flex flex-col-reverse gap-2 sm:flex-row sm:justify-end">
        <Button variant="secondary" onClick={onCancel} autoFocus>
          İmtahana qayıt
        </Button>
        <Button onClick={onConfirm}>Bəli, bitir</Button>
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

/** Per-exam set of question ids (revealed results, flags) kept in sessionStorage so a reload keeps them. */
function useStoredSet(storageKey: string): [ReadonlySet<string>, (id: string, on?: boolean) => void] {
  const [items, setItems] = useState<ReadonlySet<string>>(() => {
    try { return new Set(JSON.parse(sessionStorage.getItem(storageKey) ?? '[]') as string[]) } catch { return new Set() }
  })
  const toggle = useCallback((id: string, on?: boolean) => {
    setItems(current => {
      const next = new Set(current)
      if (on ?? !next.has(id)) next.add(id); else next.delete(id)
      try { sessionStorage.setItem(storageKey, JSON.stringify([...next])) } catch { /* storage blocked */ }
      return next
    })
  }, [storageKey])
  return [items, toggle]
}
