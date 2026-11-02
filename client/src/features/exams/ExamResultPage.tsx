import './exam-flow.css'
import { answerDisplayText } from './answer-display'
import { AlertTriangle, Check, Clock, History, RotateCw, X } from 'lucide-react'
import { useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router'
import { Alert } from '../../components/common/Alert'
import { Button } from '../../components/common/Button'
import { EmptyState } from '../../components/common/EmptyState'
import { PageHeader } from '../../components/common/PageHeader'
import { formatDate, formatNumber } from '../../lib/format'
import { cn } from '../../lib/utils'
import { useCreateExam, useExamResult, type ExamOutcome, type ExamResult, type ExamResultQuestion } from './api'
import { sourceName, sourceRange } from './examSource'

type Filter = 'Correct' | 'Incorrect'

export function ExamResultPage() {
  const { id = '' } = useParams()
  const result = useExamResult(id)

  if (result.isPending) {
    return <div className="h-64 animate-pulse rounded-xl bg-slate-200/60" aria-label="Yüklənir" />
  }

  if (result.isError) {
    return <Alert title="Nəticə yüklənmədi">{result.error.message}</Alert>
  }

  return <ResultView result={result.data} />
}

function ResultView({ result }: { result: ExamResult }) {
  const navigate = useNavigate()
  const retake = useCreateExam()
  const [filter, setFilter] = useState<Filter>('Correct')

  const questions = result.questions.filter((q) => q.outcome === filter)
  const minutes = Math.floor(result.durationSeconds / 60)
  const seconds = result.durationSeconds % 60

  const filters: { value: Filter; label: string; count: number }[] = [
    { value: 'Correct', label: 'Düzgün cavablar', count: result.correct },
    { value: 'Incorrect', label: 'Səhv cavablar', count: result.incorrect },
  ]

  return (
    <div className="exam-flow mx-auto max-w-5xl">
      <PageHeader
        title="İmtahan nəticəsi"
        description={[
          sourceName(result.source),
          sourceRange(result.source) && `suallar ${sourceRange(result.source)}`,
          formatDate(result.completedAt),
          result.completionReason === 'TimeExpired' ? 'Vaxt bitdi' : 'Tamamlandı',
        ].filter(Boolean).join(' · ')}
        actions={
          <div className="flex min-w-0 flex-wrap gap-2">
            <Link to="/exams" className="inline-flex h-10 items-center gap-2 rounded-lg border border-slate-300 bg-white px-4 font-medium text-slate-700 hover:bg-slate-50">
              <History className="size-4" aria-hidden />
              Tarixçə
            </Link>
            {canRetake(result) && (
              <Button
                loading={retake.isPending}
                onClick={() =>
                  retake.mutate(
                    {
                      questionCount: result.questionCount,
                      documentId: result.source.documentId!,
                      rangeFrom: result.source.rangeFrom!,
                      rangeTo: result.source.rangeTo!,
                      revealMode: result.revealMode,
                    },
                    { onSuccess: (session) => navigate(`/exams/${session.id}`) },
                  )
                }
              >
                <RotateCw className="size-4" aria-hidden />
                Yenidən imtahan ver
              </Button>
            )}
          </div>
        }
      />

      {retake.isError && (
        <div className="mb-6">
          <Alert title="Yeni imtahan başladılmadı">{retake.error.message}</Alert>
        </div>
      )}

      <section aria-label="Xülasə" className="mb-5 grid gap-3 sm:grid-cols-[minmax(0,1fr)_minmax(0,2fr)]">
        <div className="flex items-center justify-center gap-4 rounded-xl border border-slate-200 bg-white p-4">
          <ScoreRing percent={result.scorePercent} />
          <p className="flex flex-wrap items-center gap-1.5 text-sm text-slate-500">
            <Clock className="size-4" aria-hidden />
            {minutes} dəq {seconds} san
          </p>
        </div>
        <dl className="grid grid-cols-2 gap-3">
          <Stat label="Düzgün" value={result.correct} tone="text-emerald-700" />
          <Stat label="Səhv" value={result.incorrect} tone="text-rose-700" />
        </dl>
      </section>

      <section aria-labelledby="review-heading" className="flex min-w-0 flex-col gap-3">
        <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
          <h2 id="review-heading" className="text-lg font-semibold">
            Cavabların icmalı
          </h2>
          <div role="tablist" aria-label="Sual filtri" className="grid grid-cols-2 gap-1 rounded-xl bg-slate-100 p-1">
            {filters.map((f) => (
              <button
                key={f.value}
                type="button"
                role="tab"
                id={`exam-filter-${f.value}`}
                aria-controls="exam-review-panel"
                tabIndex={filter === f.value ? 0 : -1}
                aria-selected={filter === f.value}
                onKeyDown={event => {
                  if (!['ArrowLeft', 'ArrowRight', 'Home', 'End'].includes(event.key)) return
                  event.preventDefault()
                  const next: Filter = event.key === 'Home' ? 'Correct' : event.key === 'End' ? 'Incorrect' : filter === 'Correct' ? 'Incorrect' : 'Correct'
                  setFilter(next)
                  document.getElementById(`exam-filter-${next}`)?.focus()
                }}
                onClick={() => setFilter(f.value)}
                className={cn(
                  'min-h-11 rounded-lg px-2 py-2 text-sm font-medium transition-colors duration-150',
                  filter === f.value ? 'bg-white text-slate-900 shadow-sm' : 'text-slate-600 hover:text-slate-900',
                )}
              >
                {f.label} <span className="text-slate-400 tabular-nums">{f.count}</span>
              </button>
            ))}
          </div>
        </div>

        <div id="exam-review-panel" role="tabpanel" aria-labelledby={`exam-filter-${filter}`} tabIndex={0} className="space-y-3">
        {questions.length === 0 ? (
          <EmptyState title="Bu filtrə uyğun sual yoxdur" />
        ) : (
          questions.map((q) => <ReviewCard key={q.position} question={q} />)
        )}
        </div>
      </section>
    </div>
  )
}

/** Retake needs the same PDF and range; older exams (all PDFs) and deleted PDFs cannot be repeated. */
const canRetake = ({ source }: ExamResult) => !!source.documentName && source.rangeFrom !== null && source.rangeTo !== null

function ScoreRing({ percent }: { percent: number | null }) {
  const radius = 52
  const circumference = 2 * Math.PI * radius
  const value = percent ?? 0
  const color = percent === null ? 'text-slate-300' : value >= 80 ? 'text-emerald-500' : value >= 50 ? 'text-amber-500' : 'text-rose-500'

  return (
    <div className="relative size-24 shrink-0">
      <svg viewBox="0 0 120 120" className="size-full -rotate-90" aria-hidden>
        <circle cx="60" cy="60" r={radius} fill="none" strokeWidth="10" className="stroke-slate-100" />
        <circle
          cx="60"
          cy="60"
          r={radius}
          fill="none"
          strokeWidth="10"
          strokeLinecap="round"
          stroke="currentColor"
          strokeDasharray={circumference}
          strokeDashoffset={circumference * (1 - value / 100)}
          className={cn('transition-[stroke-dashoffset] duration-500', color)}
        />
      </svg>
      <div className="absolute inset-0 flex flex-col items-center justify-center">
        <span className="text-xl font-semibold tabular-nums">{percent === null ? '—' : `${formatNumber(percent)}%`}</span>
        <span className="text-xs text-slate-500">uğur faizi</span>
      </div>
    </div>
  )
}

function Stat({ label, value, tone, hint }: { label: string; value: number; tone: string; hint?: string }) {
  return (
    <div className="rounded-xl border border-slate-200 bg-white p-4" title={hint}>
      <dt className="text-xs text-slate-500">{label}</dt>
      <dd className={cn('mt-1 text-2xl font-semibold tabular-nums', tone)}>{formatNumber(value)}</dd>
    </div>
  )
}

const outcomeBadge: Record<ExamOutcome, { label: string; className: string }> = {
  Correct: { label: 'Düzgün', className: 'bg-emerald-50 text-emerald-700 ring-emerald-200' },
  Incorrect: { label: 'Yanlış', className: 'bg-rose-50 text-rose-700 ring-rose-200' },
  Unanswered: { label: 'Cavabsız', className: 'bg-slate-100 text-slate-700 ring-slate-200' },
  Ungraded: { label: 'Qiymətləndirilməyib', className: 'bg-amber-50 text-amber-800 ring-amber-200' },
}

function ReviewCard({ question }: { question: ExamResultQuestion }) {
  const badge = outcomeBadge[question.outcome]

  return (
    <article className="rounded-xl border border-slate-200 bg-white p-4 sm:p-5">
      <header className="mb-3 flex flex-wrap items-start gap-2">
        <span className="mt-0.5 shrink-0 rounded-md bg-slate-100 px-2 py-0.5 text-xs font-semibold text-slate-600 tabular-nums">
          {question.position + 1}
        </span>
        <h3 className="exam-question-text min-w-0 basis-[calc(100%-3rem)] flex-1 text-[15px] leading-6 font-medium">{question.text}</h3>
        <span className={cn('shrink-0 rounded-full px-2 py-0.5 text-xs font-medium ring-1 ring-inset', badge.className)}>{badge.label}</span>
      </header>

      <ul className="flex flex-col gap-1.5">
        {question.options.filter(option => option.id === question.selectedOptionId || option.id === question.correctOptionId).map((option) => {
          const isCorrect = option.id === question.correctOptionId
          const isSelected = option.id === question.selectedOptionId
          const isWrongPick = isSelected && !isCorrect && question.correctOptionId !== null

          return (
            <li
              key={option.id}
              className={cn(
                'flex items-start gap-2.5 rounded-lg border px-3 py-2.5',
                isCorrect ? 'border-emerald-200 bg-emerald-50' : isWrongPick ? 'border-rose-200 bg-rose-50' : isSelected ? 'border-slate-300 bg-slate-50' : 'border-transparent',
              )}
            >
              <span
                className={cn(
                  'flex size-6 shrink-0 items-center justify-center rounded-full text-xs font-semibold',
                  isCorrect ? 'bg-emerald-600 text-white' : isWrongPick ? 'bg-rose-600 text-white' : 'bg-slate-100 text-slate-600',
                )}
                aria-hidden
              >
                {isCorrect ? <Check className="size-3.5" strokeWidth={3} /> : isWrongPick ? <X className="size-3.5" strokeWidth={3} /> : option.label}
              </span>
              <span className={cn('exam-question-text min-w-0 flex-1 text-sm leading-6', isCorrect ? 'font-medium text-emerald-900' : isWrongPick ? 'text-rose-900' : 'text-slate-700')}>
                <span className="mb-1 block text-xs font-medium text-slate-500">{isSelected && isCorrect ? 'Sizin cavab · düzgün' : isSelected ? 'Sizin cavab' : 'Düzgün cavab'} · {option.label}</span>
                {answerDisplayText(option.text)}
              </span>

            </li>
          )
        })}
      </ul>

      {question.outcome === 'Ungraded' && (
        <p className="mt-3 flex items-start gap-2 rounded-lg bg-amber-50 px-3 py-2 text-amber-800">
          <AlertTriangle className="mt-0.5 size-4 shrink-0" aria-hidden />
          Bu sualın düzgün cavabı məlum deyil, nəticəyə daxil edilmədi.
        </p>
      )}
    </article>
  )
}
