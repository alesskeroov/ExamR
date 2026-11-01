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

type Filter = 'all' | 'Incorrect' | 'Unanswered' | 'Ungraded'

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
  const [filter, setFilter] = useState<Filter>('all')

  const questions = filter === 'all' ? result.questions : result.questions.filter((q) => q.outcome === filter)
  const minutes = Math.floor(result.durationSeconds / 60)
  const seconds = result.durationSeconds % 60

  const filters: { value: Filter; label: string; count: number }[] = [
    { value: 'all', label: 'Hamısı', count: result.questionCount },
    { value: 'Incorrect', label: 'Yanlış', count: result.incorrect },
    { value: 'Unanswered', label: 'Cavabsız', count: result.unanswered },
    { value: 'Ungraded', label: 'Qiymətləndirilməyən', count: result.ungraded },
  ]

  return (
    <>
      <PageHeader
        title="İmtahan nəticəsi"
        description={[
          sourceName(result.source),
          sourceRange(result.source) && `suallar ${sourceRange(result.source)}`,
          formatDate(result.completedAt),
          result.completionReason === 'TimeExpired' ? 'Vaxt bitdi' : 'Tamamlandı',
        ].filter(Boolean).join(' · ')}
        actions={
          <>
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
          </>
        }
      />

      {retake.isError && (
        <div className="mb-6">
          <Alert title="Yeni imtahan başladılmadı">{retake.error.message}</Alert>
        </div>
      )}

      <section aria-label="Xülasə" className="mb-8 grid gap-4 lg:grid-cols-[240px_minmax(0,1fr)]">
        <div className="flex flex-col items-center justify-center rounded-xl border border-slate-200 bg-white p-6 shadow-sm">
          <ScoreRing percent={result.scorePercent} />
          <p className="mt-3 flex items-center gap-1.5 text-slate-500">
            <Clock className="size-4" aria-hidden />
            {minutes} dəq {seconds} san
          </p>
        </div>
        <dl className="grid grid-cols-2 gap-4 sm:grid-cols-4">
          <Stat label="Düzgün" value={result.correct} tone="text-emerald-700" />
          <Stat label="Yanlış" value={result.incorrect} tone="text-rose-700" />
          <Stat label="Cavabsız" value={result.unanswered} tone="text-slate-900" />
          <Stat label="Qiymətləndirilməyən" value={result.ungraded} tone="text-amber-700" hint="Düzgün cavabı məlum deyil" />
        </dl>
      </section>

      <section aria-labelledby="review-heading" className="flex flex-col gap-4">
        <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
          <h2 id="review-heading" className="text-lg font-semibold">
            Suallar
          </h2>
          <div role="tablist" aria-label="Sual filtri" className="flex flex-wrap gap-1 rounded-lg bg-slate-100 p-1">
            {filters.map((f) => (
              <button
                key={f.value}
                type="button"
                role="tab"
                aria-selected={filter === f.value}
                onClick={() => setFilter(f.value)}
                className={cn(
                  'h-8 rounded-md px-3 font-medium transition-colors duration-150',
                  filter === f.value ? 'bg-white text-slate-900 shadow-sm' : 'text-slate-600 hover:text-slate-900',
                )}
              >
                {f.label} <span className="text-slate-400 tabular-nums">{f.count}</span>
              </button>
            ))}
          </div>
        </div>

        {questions.length === 0 ? (
          <EmptyState title="Bu filtrə uyğun sual yoxdur" />
        ) : (
          questions.map((q) => <ReviewCard key={q.position} question={q} />)
        )}
      </section>
    </>
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
    <div className="relative size-36">
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
        <span className="text-3xl font-semibold tabular-nums">{percent === null ? '—' : `${formatNumber(percent)}%`}</span>
        <span className="text-xs text-slate-500">uğur faizi</span>
      </div>
    </div>
  )
}

function Stat({ label, value, tone, hint }: { label: string; value: number; tone: string; hint?: string }) {
  return (
    <div className="rounded-xl border border-slate-200 bg-white p-4 shadow-sm" title={hint}>
      <dt className="text-slate-500">{label}</dt>
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
    <article className="rounded-xl border border-slate-200 bg-white p-4 shadow-sm sm:p-5">
      <header className="mb-3 flex items-start gap-3">
        <span className="mt-0.5 shrink-0 rounded-md bg-slate-100 px-2 py-0.5 text-xs font-semibold text-slate-600 tabular-nums">
          {question.position + 1}
        </span>
        <h3 className="flex-1 text-[15px] leading-6 font-medium">{question.text}</h3>
        <span className={cn('shrink-0 rounded-full px-2 py-0.5 text-xs font-medium ring-1 ring-inset', badge.className)}>{badge.label}</span>
      </header>

      <ul className="flex flex-col gap-1.5">
        {question.options.map((option) => {
          const isCorrect = option.id === question.correctOptionId
          const isSelected = option.id === question.selectedOptionId
          const isWrongPick = isSelected && !isCorrect && question.correctOptionId !== null

          return (
            <li
              key={option.id}
              className={cn(
                'flex items-start gap-3 rounded-lg border px-3 py-2',
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
              <span className={cn('flex-1 leading-6', isCorrect ? 'font-medium text-emerald-900' : isWrongPick ? 'text-rose-900' : 'text-slate-700')}>
                {option.text}
              </span>
              {(isSelected || isCorrect) && (
                <span className="shrink-0 text-xs font-medium text-slate-500">
                  {isSelected && isCorrect ? 'Sizin cavab · düzgün' : isSelected ? 'Sizin cavab' : 'Düzgün cavab'}
                </span>
              )}
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
