import { AlertCircle, AlertTriangle, ChevronDown, FileText, Loader2, RotateCw } from 'lucide-react'
import { useState } from 'react'
import { Button } from '../../components/common/Button'
import { EmptyState } from '../../components/common/EmptyState'
import { StatusBadge } from '../../components/common/StatusBadge'
import { formatBytes, formatNumber } from '../../lib/format'
import { cn } from '../../lib/utils'
import { QuestionCard } from '../questions/QuestionCard'
import {
  useDocument,
  useDocumentQuestions,
  useReprocessDocument,
  type DocumentDto,
  type QuestionFilter,
} from './api'
import { documentStatus } from './documentStatus'
import { useTranslation } from 'react-i18next'

/** Status, parsing report and extracted questions of one uploaded document. */
export function DocumentResult({ documentId }: { documentId: string }) {
  const { t } = useTranslation()
  const { data: document, isPending, error } = useDocument(documentId)

  if (isPending) {
    return <PanelSkeleton />
  }

  if (error || !document) {
    return (
      <Alert tone="danger" title={t('documents.loadFailed')}>
        {error?.message ?? t('documents.notFound')}
      </Alert>
    )
  }

  return (
    <div className="flex flex-col gap-6">
      <DocumentHeader document={document} />
      {(document.status === 'Pending' || document.status === 'Processing') && <ProcessingState />}
      {document.status === 'Failed' && <FailedState document={document} />}
      {document.status === 'Completed' && document.processingReport && (
        <>
          <ReportStats document={document} />
          <QuestionList documentId={document.id} />
        </>
      )}
    </div>
  )
}

function DocumentHeader({ document }: { document: DocumentDto }) {
  const { t } = useTranslation()
  const status = documentStatus[document.status]
  return (
    <div className="flex items-center gap-3 rounded-xl border border-slate-200 bg-white p-4 shadow-sm">
      <div className="rounded-lg bg-rose-50 p-2.5 text-rose-600">
        <FileText className="size-5" aria-hidden />
      </div>
      <div className="min-w-0 flex-1">
        <p className="truncate font-semibold" title={document.originalFileName}>
          {document.originalFileName}
        </p>
        <p className="text-slate-500">
          {formatBytes(document.fileSizeBytes)}
          {document.pageCount !== null && ` · ${t('documents.pages', { count: document.pageCount })}`}
          {` · ${document.uploadedBy.displayName}`}
        </p>
      </div>
      <StatusBadge tone={status.tone}>{t(`documents.status.${document.status}`)}</StatusBadge>
    </div>
  )
}

function ProcessingState() {
  const { t } = useTranslation()
  return (
    <div role="status" className="flex items-center gap-3 rounded-xl border border-indigo-100 bg-indigo-50 px-4 py-5 text-indigo-800">
      <Loader2 className="size-5 shrink-0 animate-spin" aria-hidden />
      <div>
        <p className="font-medium">{t('documents.extracting')}</p>
        <p className="text-indigo-700/80">{t('documents.extractingText')}</p>
      </div>
    </div>
  )
}

function FailedState({ document }: { document: DocumentDto }) {
  const { t } = useTranslation()
  const reprocess = useReprocessDocument()
  return (
    <Alert tone="danger" title={t('documents.processingFailed')}>
      <p>{document.errorMessage ?? t('documents.unknownError')}</p>
      {document.canEdit && (
        <Button variant="secondary" className="mt-3" loading={reprocess.isPending} onClick={() => reprocess.mutate(document.id)}>
          <RotateCw className="size-4" aria-hidden />
          {t('documents.reprocess')}
        </Button>
      )}
    </Alert>
  )
}

function ReportStats({ document }: { document: DocumentDto }) {
  const { t } = useTranslation()
  const report = document.processingReport!
  const [showWarnings, setShowWarnings] = useState(false)

  const stats = [
    { label: t('documents.report.found'), value: report.questionsFound, tone: 'text-slate-900' },
    { label: t('documents.report.answered'), value: report.autoDetected, tone: 'text-emerald-700' },
    { label: t('documents.report.unverified'), value: report.unverified, tone: report.unverified > 0 ? 'text-amber-700' : 'text-slate-900' },
    { label: t('documents.report.duplicates'), value: report.duplicatesSkipped, tone: 'text-slate-900' },
  ]

  return (
    <section aria-label={t('documents.report.aria')} className="flex flex-col gap-3">
      <dl className="grid grid-cols-2 gap-3 lg:grid-cols-4">
        {stats.map((stat) => (
          <div key={stat.label} className="rounded-xl border border-slate-200 bg-white p-4 shadow-sm">
            <dt className="text-slate-500">{stat.label}</dt>
            <dd className={cn('mt-1 text-2xl font-semibold tabular-nums', stat.tone)}>{formatNumber(stat.value)}</dd>
          </div>
        ))}
      </dl>

      {report.warnings.length > 0 ? (
        <div className="rounded-xl border border-amber-200 bg-amber-50 text-amber-900">
          <button
            type="button"
            onClick={() => setShowWarnings((v) => !v)}
            aria-expanded={showWarnings}
            className="flex w-full items-center gap-2 px-4 py-3 text-left font-medium"
          >
            <AlertTriangle className="size-4 shrink-0" aria-hidden />
            <span className="flex-1">{t('documents.report.warnings', { n: report.warnings.length })}</span>
            <ChevronDown className={cn('size-4 transition-transform duration-150', showWarnings && 'rotate-180')} aria-hidden />
          </button>
          {showWarnings && (
            <ul className="max-h-60 list-disc overflow-y-auto border-t border-amber-200 px-4 py-3 pl-8">
              {report.warnings.map((warning, i) => (
                <li key={i}>{warning}</li>
              ))}
            </ul>
          )}
        </div>
      ) : (
        <p className="text-slate-500">{t('documents.report.noWarnings')}</p>
      )}
    </section>
  )
}

const filters: { value: QuestionFilter; label: string }[] = [
  { value: 'all', label: 'exam.setup.all' },
  { value: 'answered', label: 'documents.report.answered' },
  { value: 'unverified', label: 'documents.report.unverified' },
  { value: 'warnings', label: 'documents.filterWarnings' },
]

function QuestionList({ documentId }: { documentId: string }) {
  const { t } = useTranslation()
  const [filter, setFilter] = useState<QuestionFilter>('all')
  const questions = useDocumentQuestions(documentId, filter, true)
  const items = questions.data?.pages.flatMap((p) => p.items) ?? []
  const total = questions.data?.pages[0]?.totalCount ?? 0

  return (
    <section aria-labelledby="questions-heading" className="flex flex-col gap-4">
      <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
        <h2 id="questions-heading" className="text-lg font-semibold">
          {t('documents.extracted')}
        </h2>
        <div role="tablist" aria-label={t('exam.result.filter')} className="flex flex-wrap gap-1 rounded-lg bg-slate-100 p-1">
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
              {t(f.label)}
            </button>
          ))}
        </div>
      </div>

      {questions.isPending ? (
        <PanelSkeleton />
      ) : questions.isError ? (
        <Alert tone="danger" title={t('documents.questionsFailed')}>
          {questions.error.message}
        </Alert>
      ) : items.length === 0 ? (
        <EmptyState title={t('exam.result.noMatch')} />
      ) : (
        <>
          <p className="text-slate-500" aria-live="polite">
            {t('documents.showing', { shown: formatNumber(items.length), total: formatNumber(total) })}
          </p>
          <div className={cn('flex flex-col gap-3', questions.isPlaceholderData && 'opacity-60')}>
            {items.map((question) => (
              <QuestionCard key={question.id} question={question} />
            ))}
          </div>
          {questions.hasNextPage && (
            <Button
              variant="secondary"
              className="self-center"
              loading={questions.isFetchingNextPage}
              onClick={() => questions.fetchNextPage()}
            >
              {t('documents.showMore')}
            </Button>
          )}
        </>
      )}
    </section>
  )
}

function Alert({ tone, title, children }: { tone: 'danger'; title: string; children: React.ReactNode }) {
  return (
    <div role="alert" className={cn('flex gap-3 rounded-xl border px-4 py-4', tone === 'danger' && 'border-rose-200 bg-rose-50 text-rose-800')}>
      <AlertCircle className="mt-0.5 size-5 shrink-0" aria-hidden />
      <div>
        <p className="font-semibold">{title}</p>
        <div className="mt-0.5">{children}</div>
      </div>
    </div>
  )
}

function PanelSkeleton() {
  return (
    <div className="flex flex-col gap-3" aria-hidden>
      {[0, 1, 2].map((i) => (
        <div key={i} className="h-24 animate-pulse rounded-xl bg-slate-200/60" />
      ))}
    </div>
  )
}
