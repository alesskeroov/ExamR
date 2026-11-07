import './exam-flow.css'
import { answerDisplayText } from './answer-display'
import { AlertTriangle, Check, Clock, History, RotateCw, X } from 'lucide-react'
import { useEffect, useState } from 'react'
import { Link, useParams } from 'react-router'
import { Alert } from '../../components/common/Alert'
import { EmptyState } from '../../components/common/EmptyState'
import { PageHeader } from '../../components/common/PageHeader'
import { QuestionVisuals } from '../../components/common/QuestionVisuals'
import { formatDate, formatNumber } from '../../lib/format'
import { cn } from '../../lib/utils'
import { bodyVisuals, optionVisuals, visibleText, visualUrls } from '../../lib/visuals'
import { useExamResult, type ExamOutcome, type ExamResult, type ExamResultQuestion } from './api'
import { sourceName, sourceRange } from './examSource'
import { DimReportCard } from '../dim/DimReportCard'

type Filter = 'All' | 'Correct' | 'Incorrect' | 'Unanswered'

const cellTone: Record<ExamOutcome, string> = {
  Correct: 'border-emerald-600 bg-emerald-600 text-white',
  Incorrect: 'border-rose-600 bg-rose-600 text-white',
  Unanswered: 'border-slate-300 bg-white text-slate-600 border-dashed',
  Ungraded: 'border-amber-400 bg-amber-100 text-amber-900',
}

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
  const [filter, setFilter] = useState<Filter>('All')
  const [focused, setFocused] = useState<number | null>(null)

  const questions = filter === 'All' ? result.questions : result.questions.filter((q) => q.outcome === filter)
  const minutes = Math.floor(result.durationSeconds / 60)
  const seconds = result.durationSeconds % 60
  // Counted from the per-question outcomes so the parts always add up to the list below.
  const count = (outcome: ExamOutcome) => result.questions.filter(q => q.outcome === outcome).length
  const total = result.questions.length

  const filters: { value: Filter; label: string; count: number }[] = [
    { value: 'All', label: 'Hamısı', count: total },
    { value: 'Correct', label: 'Düzgün', count: count('Correct') },
    { value: 'Incorrect', label: 'Səhv', count: count('Incorrect') },
    { value: 'Unanswered', label: 'Boş', count: count('Unanswered') },
  ]
  const segments: Segment[] = [
    { value: 'Correct', label: 'Düzgün', count: count('Correct'), swatch: 'bg-emerald-600' },
    { value: 'Incorrect', label: 'Səhv', count: count('Incorrect'), swatch: 'bg-rose-600' },
    { value: 'Unanswered', label: 'Boş', count: count('Unanswered'), swatch: 'bg-slate-300' },
    // Questions whose correct answer is unknown are not scored; listed only when present.
    ...(count('Ungraded') > 0 ? [{ value: 'Ungraded' as const, label: 'Qiymətləndirilməyib', count: count('Ungraded'), swatch: 'bg-amber-400' }] : []),
  ]

  const openQuestion = (q: ExamResultQuestion) => {
    setFocused(q.position)
    if (filter !== 'All' && filter !== q.outcome) setFilter('All')
    // Wait for the filtered list to render before scrolling to the card.
    requestAnimationFrame(() => document.getElementById(`result-q-${q.position}`)?.scrollIntoView({
      behavior: window.matchMedia('(prefers-reduced-motion: reduce)').matches ? 'auto' : 'smooth', block: 'start' }))
  }

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
              // Opens the setup with this exam's settings, so they can be reviewed and changed first.
              <Link to={`/exams/new?retake=${result.id}`} className="inline-flex h-10 items-center gap-2 rounded-lg btn-primary px-4 font-medium">
                <RotateCw className="size-4" aria-hidden />
                Yenidən imtahan ver
              </Link>
            )}
          </div>
        }
      />

      <section aria-label="Xülasə" className="result-reveal mb-5 rounded-xl border border-slate-200 bg-white p-4 sm:p-6">
        <div className="flex flex-wrap items-end justify-between gap-x-6 gap-y-2">
          <div className="min-w-0">
            <p className="text-xs font-medium tracking-wide text-slate-500 uppercase">Uğur faizi</p>
            <p className="mt-0.5 flex items-baseline gap-2">
              <ScoreValue percent={result.scorePercent} />
              <span className="text-sm text-slate-500 tabular-nums">{formatNumber(result.correct)} / {formatNumber(total)} düzgün</span>
            </p>
          </div>
          <p className="flex items-center gap-1.5 text-sm text-slate-500 tabular-nums">
            <Clock className="size-4" aria-hidden />
            {minutes} dəq {seconds} san
          </p>
        </div>

        <SegmentedBar segments={segments} total={total} />
        <dl className="mt-3 grid grid-cols-2 gap-x-4 gap-y-1.5 text-sm sm:flex sm:flex-wrap sm:gap-x-6">
          {segments.map(s => (
            <div key={s.value} className="flex items-center gap-2">
              <span className={cn('size-2.5 shrink-0 rounded-sm', s.swatch)} aria-hidden />
              <dt className="min-w-0 truncate text-slate-500">{s.label}</dt>
              <dd className="ml-auto shrink-0 font-semibold whitespace-nowrap tabular-nums sm:ml-0">{formatNumber(s.count)}</dd>
            </div>
          ))}
        </dl>

        <div className="mt-5 border-t border-slate-100 pt-4">
          <h2 className="mb-2.5 text-sm font-semibold">Sual xəritəsi</h2>
          <ol className="result-map grid grid-cols-[repeat(auto-fill,minmax(2.25rem,1fr))] gap-1.5" aria-label="Sual xəritəsi">
            {result.questions.map((q, i) => (
              <li key={q.position}>
                <button
                  type="button"
                  onClick={() => openQuestion(q)}
                  aria-current={focused === q.position ? 'true' : undefined}
                  aria-label={`Sual ${q.position + 1}: ${outcomeBadge[q.outcome].label}`}
                  style={{ animationDelay: `${Math.min(i, 40) * 8}ms` }}
                  className={cn('result-cell flex h-9 w-full items-center justify-center rounded-md border text-xs font-semibold tabular-nums', cellTone[q.outcome],
                    focused === q.position && 'ring-2 ring-[var(--app-ink)] ring-offset-2 ring-offset-[var(--app-surface)]')}
                >
                  {q.position + 1}
                </button>
              </li>
            ))}
          </ol>
        </div>
      </section>

      <DimReportCard examId={result.id} />

      <section aria-labelledby="review-heading" className="flex min-w-0 flex-col gap-3">
        <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
          <h2 id="review-heading" className="text-lg font-semibold">
            Cavabların icmalı
          </h2>
          <div role="tablist" aria-label="Sual filtri" className="grid grid-cols-4 gap-1 rounded-xl bg-slate-100 p-1">
            {filters.map((f, i) => (
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
                  const to = event.key === 'Home' ? 0 : event.key === 'End' ? filters.length - 1 : (i + (event.key === 'ArrowRight' ? 1 : filters.length - 1)) % filters.length
                  setFilter(filters[to].value)
                  document.getElementById(`exam-filter-${filters[to].value}`)?.focus()
                }}
                onClick={() => setFilter(f.value)}
                className={cn(
                  'flex min-h-11 min-w-0 flex-col items-center justify-center rounded-lg px-1 py-1.5 text-xs font-medium transition-colors duration-150 sm:flex-row sm:gap-1.5 sm:px-3 sm:text-sm',
                  filter === f.value ? 'bg-white text-slate-900 shadow-sm' : 'text-slate-600 hover:text-slate-900',
                )}
              >
                <span className="truncate">{f.label}</span> <span className="text-slate-400 tabular-nums">{f.count}</span>
              </button>
            ))}
          </div>
        </div>

        <div id="exam-review-panel" role="tabpanel" aria-labelledby={`exam-filter-${filter}`} tabIndex={0} className="space-y-3">
        {questions.length === 0 ? (
          <EmptyState title="Bu filtrə uyğun sual yoxdur" />
        ) : (
          questions.map((q) => <ReviewCard key={q.position} question={q} visualSrc={visualUrls.exam(result.id)} highlighted={focused === q.position} />)
        )}
        </div>
      </section>
    </div>
  )
}

/** Retake needs one source PDF and a range; older exams (all PDFs) cannot be repeated. A deleted PDF is explained on the setup page. */
const canRetake = ({ source }: ExamResult) => source.documentId !== null && source.rangeFrom !== null && source.rangeTo !== null

interface Segment { value: ExamOutcome; label: string; count: number; swatch: string }

/** Score with a short count-up; the final value is what screen readers get. */
function ScoreValue({ percent }: { percent: number | null }) {
  const shown = useCountUp(percent ?? 0, 500)
  if (percent === null) return <span className="text-4xl font-semibold tracking-tight tabular-nums">—</span>
  return (
    <span className="text-4xl font-semibold tracking-tight tabular-nums sm:text-5xl" aria-label={`${formatNumber(percent)}%`}>
      <span aria-hidden>{formatNumber(Math.round(shown))}%</span>
    </span>
  )
}

function useCountUp(target: number, durationMs: number) {
  const [value, setValue] = useState(() => (prefersReducedMotion() ? target : 0))
  useEffect(() => {
    if (prefersReducedMotion()) return
    let frame = 0
    const start = performance.now()
    const tick = (now: number) => {
      const t = Math.min(1, (now - start) / durationMs)
      setValue(target * (1 - (1 - t) ** 3))
      if (t < 1) frame = requestAnimationFrame(tick)
    }
    frame = requestAnimationFrame(tick)
    return () => cancelAnimationFrame(frame)
  }, [durationMs, target])
  return value
}

const prefersReducedMotion = () => typeof window !== 'undefined' && window.matchMedia('(prefers-reduced-motion: reduce)').matches

/** One bar split by outcome; parts grow in from the left. */
function SegmentedBar({ segments, total }: { segments: Segment[]; total: number }) {
  return (
    <div className="result-bar mt-4 flex h-2.5 w-full gap-0.5 overflow-hidden rounded-full bg-slate-100" role="img"
      aria-label={segments.map(s => `${s.label}: ${s.count}`).join(', ')}>
      {segments.filter(s => s.count > 0).map((s, i) => (
        <span key={s.value} className={cn('result-bar-part h-full', s.swatch)}
          style={{ width: `${(s.count / Math.max(total, 1)) * 100}%`, animationDelay: `${i * 90}ms` }} />
      ))}
    </div>
  )
}

const outcomeBadge: Record<ExamOutcome, { label: string; className: string }> = {
  Correct: { label: 'Düzgün', className: 'bg-emerald-50 text-emerald-700 ring-emerald-200' },
  Incorrect: { label: 'Yanlış', className: 'bg-rose-50 text-rose-700 ring-rose-200' },
  Unanswered: { label: 'Cavablandırılmayıb', className: 'bg-slate-100 text-slate-700 ring-slate-200' },
  Ungraded: { label: 'Qiymətləndirilməyib', className: 'bg-amber-50 text-amber-800 ring-amber-200' },
}

function ReviewCard({ question, visualSrc, highlighted }: { question: ExamResultQuestion; visualSrc: (id: string) => string; highlighted: boolean }) {
  const badge = outcomeBadge[question.outcome]
  const unanswered = question.outcome === 'Unanswered'
  // Unanswered questions list every option so the student sees what they skipped.
  const options = unanswered ? question.options : question.options.filter(option => option.id === question.selectedOptionId || option.id === question.correctOptionId)

  return (
    <article id={`result-q-${question.position}`} className={cn('result-card scroll-mt-20 rounded-xl border bg-white p-4 sm:p-5', highlighted ? 'border-indigo-300 ring-2 ring-indigo-500/20' : 'border-slate-200')}>
      <header className="mb-3 flex flex-wrap items-start gap-2">
        <span className="mt-0.5 shrink-0 rounded-md bg-slate-100 px-2 py-0.5 text-xs font-semibold text-slate-600 tabular-nums">
          {question.position + 1}
        </span>
        <h3 className="exam-question-text min-w-0 basis-[calc(100%-3rem)] flex-1 text-[15px] leading-6 font-medium">
          {visibleText(question.text, bodyVisuals(question.visuals).length > 0) ?? <span className="sr-only">Şəkilli sual</span>}
        </h3>
        <span className={cn('shrink-0 rounded-full px-2 py-0.5 text-xs font-medium ring-1 ring-inset', badge.className)}>{badge.label}</span>
      </header>
      <QuestionVisuals visuals={bodyVisuals(question.visuals)} src={visualSrc} className="mb-3" />

      {unanswered && (
        <p className="mb-2.5 rounded-lg border border-dashed border-slate-300 px-3 py-2 text-sm text-slate-600">Bu suala cavab verməmisiniz.</p>
      )}

      <ul className="flex flex-col gap-1.5">
        {options.map((option) => {
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
                {(isSelected || isCorrect) && <span className="mb-1 block text-xs font-medium text-slate-500">{isSelected && isCorrect ? 'Sizin cavab · düzgün' : isSelected ? 'Sizin cavab' : 'Düzgün cavab'} · {option.label}</span>}
                {visibleText(answerDisplayText(option.text), optionVisuals(question.visuals, option.id).length > 0)}
                <QuestionVisuals visuals={optionVisuals(question.visuals, option.id)} src={visualSrc} size="option" />
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
