import { AlertTriangle, Check } from 'lucide-react'
import { QuestionVisuals } from '../../components/common/QuestionVisuals'
import { StatusBadge, type BadgeTone } from '../../components/common/StatusBadge'
import { cn } from '../../lib/utils'
import { bodyVisuals, optionVisuals, visibleText, visualUrls } from '../../lib/visuals'
import type { QuestionDto, VerificationStatus } from '../documents/api'

const statusBadge: Record<VerificationStatus, { tone: BadgeTone; label: string }> = {
  AutoDetected: { tone: 'info', label: 'Avtomatik' },
  Verified: { tone: 'success', label: 'Təsdiqlənmiş' },
  Unverified: { tone: 'warning', label: 'Cavab bilinmir' },
}

export function QuestionCard({ question }: { question: QuestionDto }) {
  const badge = statusBadge[question.verificationStatus]
  const visualSrc = visualUrls.document(question.documentId)
  const images = bodyVisuals(question.visuals)

  return (
    <article className="rounded-xl border border-slate-200 bg-white p-4 shadow-sm sm:p-5" aria-labelledby={`q-${question.id}`}>
      <header className="mb-3 flex items-start gap-3">
        <span className="mt-0.5 shrink-0 rounded-md bg-slate-100 px-2 py-0.5 text-xs font-semibold text-slate-600 tabular-nums">
          #{question.sourceNumber}
        </span>
        <h3 id={`q-${question.id}`} className="flex-1 text-[15px] leading-6 font-medium text-slate-900">
          {visibleText(question.text, images.length > 0) ?? <span className="sr-only">Şəkilli sual #{question.sourceNumber}</span>}
        </h3>
        <span className="shrink-0">
          <StatusBadge tone={badge.tone}>{badge.label}</StatusBadge>
        </span>
      </header>
      <QuestionVisuals visuals={images} src={visualSrc} className="mb-3" />

      <ul className="flex flex-col gap-1.5">
        {question.options.map((option) => (
          <li
            key={option.id}
            className={cn(
              'flex items-start gap-3 rounded-lg border px-3 py-2',
              option.isCorrect ? 'border-emerald-200 bg-emerald-50' : 'border-transparent',
            )}
          >
            <span
              className={cn(
                'flex size-6 shrink-0 items-center justify-center rounded-full text-xs font-semibold',
                option.isCorrect ? 'bg-emerald-600 text-white' : 'bg-slate-100 text-slate-600',
              )}
              aria-hidden
            >
              {option.isCorrect ? <Check className="size-3.5" strokeWidth={3} /> : option.label}
            </span>
            <span className={cn('flex-1 leading-6', option.isCorrect ? 'font-medium text-emerald-900' : 'text-slate-700')}>
              <span className="sr-only">
                {option.label}){option.isCorrect ? ' (düzgün cavab)' : ''}{' '}
              </span>
              {visibleText(option.text, optionVisuals(question.visuals, option.id).length > 0)}
              <QuestionVisuals visuals={optionVisuals(question.visuals, option.id)} src={visualSrc} size="option" />
            </span>
          </li>
        ))}
      </ul>

      {question.parseWarning && (
        <p className="mt-3 flex items-start gap-2 rounded-lg bg-amber-50 px-3 py-2 text-amber-800">
          <AlertTriangle className="mt-0.5 size-4 shrink-0" aria-hidden />
          {question.parseWarning}
        </p>
      )}
    </article>
  )
}
