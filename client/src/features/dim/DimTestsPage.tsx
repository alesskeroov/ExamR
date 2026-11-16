import { BookOpenCheck, CalendarClock, ClipboardList, Clock, Layers, Play, RotateCw, Trophy } from 'lucide-react'
import { useState, type ReactNode } from 'react'
import { Link, useNavigate } from 'react-router'
import { Alert } from '../../components/common/Alert'
import { SupportContact } from '../../components/common/SupportContact'
import { Button } from '../../components/common/Button'
import { EmptyState } from '../../components/common/EmptyState'
import { PageHeader } from '../../components/common/PageHeader'
import { StatusBadge } from '../../components/common/StatusBadge'
import { UsageMeter } from '../../components/common/UsageMeter'
import { ApiError } from '../../lib/api-client'
import { formatDate, formatNumber } from '../../lib/format'
import { cn } from '../../lib/utils'
import type { RevealMode } from '../exams/api'
import { useDimTests, useStartDimTest, type DimTest, type DimTrack } from './api'
import { DimStartDialog } from './DimStartDialog'
import { useUsageLimit } from '../usage/api'
import { useTranslation } from 'react-i18next'

type KindFilter = 'All' | 'Full' | 'Subject'

/** Titles and descriptions live in the locale files under dim.page.<track>. */
const copy: Record<DimTrack, { max: number }> = { Buraxilis: { max: 300 }, Blok: { max: 400 } }

/** Accent per subject, so cards are told apart at a glance. */
const accents: Record<string, string> = {
  riy: 'bg-indigo-50 text-indigo-600',
  az: 'bg-emerald-50 text-emerald-600',
  xdil: 'bg-amber-50 text-amber-600',
}

export function DimTestsPage({ track }: { track: DimTrack }) {
  const { t } = useTranslation()
  const tests = useDimTests(track)
  const [kind, setKind] = useState<KindFilter>('All')
  const visible = (tests.data ?? []).filter(test => kind === 'All' || test.kind === kind)

  return (
    <div className="mx-auto max-w-5xl">
      <PageHeader title={t(`dim.page.${track}.title`)} description={t(`dim.page.${track}.description`)} />

      <Alert tone="info" title={t('dim.page.noticeTitle')}>
        {t('dim.page.notice', { max: copy[track].max })}
      </Alert>
      <div className="mt-4"><UsageMeter kind="DimTest" /></div>

      <div role="tablist" aria-label={t('dim.page.kindAria')} className="mt-5 inline-flex w-full rounded-lg border border-slate-200 bg-white p-1 sm:w-auto">
        {([['All', t('exam.setup.all')], ['Full', t('dim.page.full')], ['Subject', t('dim.page.subject')]] as const).map(([value, label]) => (
          <button key={value} type="button" role="tab" aria-selected={kind === value} onClick={() => setKind(value)}
            className={cn('flex-1 whitespace-nowrap rounded-md px-2 py-1.5 text-sm font-medium transition-colors sm:flex-none sm:px-3',
              kind === value ? 'bg-indigo-600 text-white' : 'text-slate-600 hover:bg-slate-100')}>
            {label}
          </button>
        ))}
      </div>

      <div className="mt-5">
        {tests.isPending ? (
          <div className="grid gap-4 md:grid-cols-2" aria-label={t('common.loading')}>
            {[0, 1, 2, 3].map(i => <div key={i} className="h-56 animate-pulse rounded-xl bg-slate-200/60" />)}
          </div>
        ) : tests.isError ? (
          <Alert title={t('dim.page.loadFailed')}>{tests.error.message}<SupportContact className="mt-1" topic={t('dim.page.loadFailed')} /></Alert>
        ) : visible.length === 0 ? (
          <EmptyState icon={<ClipboardList className="size-6" />} title={t('dim.page.empty')}
            description={tests.data.length === 0 ? t('dim.page.emptyText') : t('dim.page.noMatch')} />
        ) : (
          <ul className="grid gap-4 md:grid-cols-2">
            {visible.map(test => <li key={test.id}><TestCard test={test} /></li>)}
          </ul>
        )}
      </div>
    </div>
  )
}

function TestCard({ test }: { test: DimTest }) {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const start = useStartDimTest()
  const monthly = useUsageLimit('DimTest')
  const [choosing, setChoosing] = useState(false)
  const last = test.lastAttempt
  const inProgress = last?.status === 'InProgress'
  const limitReached = test.attemptLimit !== null && test.attemptsUsed >= test.attemptLimit
  const activeExamId = start.error instanceof ApiError ? start.error.problem?.activeExamId as string | undefined : undefined
  const subject = test.kind === 'Subject' ? test.sections[0]?.subject : undefined
  const accent = (subject && accents[subject]) || 'bg-slate-100 text-slate-600'

  const begin = (revealMode: RevealMode) => start.mutate({ testId: test.id, revealMode }, {
    onSuccess: ({ examId }) => navigate(`/exams/${examId}`),
    onSettled: () => setChoosing(false),
  })

  return (
    <article className="flex h-full flex-col rounded-xl border border-slate-200 bg-white p-4 shadow-sm transition-shadow hover:shadow-md sm:p-5">
      <div className="flex items-start gap-3">
        <span className={cn('grid size-11 shrink-0 place-items-center rounded-xl', accent)} aria-hidden>
          {test.kind === 'Full' ? <Layers className="size-5" /> : <BookOpenCheck className="size-5" />}
        </span>
        <div className="min-w-0 flex-1">
          <p className="text-xs font-medium tracking-wide text-indigo-700 uppercase">{test.kind === 'Full' ? t('dim.kind.Full') : t('dim.kind.Subject')}</p>
          <h2 className="mt-0.5 text-lg leading-snug font-semibold break-words text-slate-900">{test.title}</h2>
        </div>
        {inProgress ? <StatusBadge tone="info">{t('profile.activity.inProgress')}</StatusBadge>
          : !test.isOpen ? <StatusBadge tone="warning">{t('dim.page.closed')}</StatusBadge>
          : null}
      </div>

      {test.sections.length > 1 && (
        <ul className="mt-3 flex flex-wrap gap-1.5" aria-label={t('dim.page.subjects')}>
          {test.sections.map(s => (
            <li key={s.subject} className="rounded-full bg-slate-100 px-2.5 py-0.5 text-xs font-medium text-slate-700">{s.name} · {s.questionCount}</li>
          ))}
        </ul>
      )}

      <dl className="mt-4 flex flex-wrap gap-x-5 gap-y-1.5 text-sm text-slate-600">
        <Stat icon={<Layers className="size-4" />} label={t('exam.session.questions')}>{t('common.questions', { count: test.questionCount })}</Stat>
        <Stat icon={<Clock className="size-4" />} label={t('dim.page.duration')}>{t('dim.page.minutesShort', { n: test.durationMinutes })}</Stat>
        <Stat icon={<RotateCw className="size-4" />} label={t('dim.page.attempts')}>
          {test.attemptLimit === null ? t('dim.page.attemptsUnlimited', { used: test.attemptsUsed }) : t('dim.page.attemptsOf', { used: test.attemptsUsed, limit: test.attemptLimit })}
        </Stat>
      </dl>

      {(test.availableFrom || test.availableTo) && (
        <p className="mt-3 flex items-center gap-1.5 text-sm text-slate-600">
          <CalendarClock className="size-4 text-slate-400" aria-hidden />
          {[test.availableFrom && formatDate(test.availableFrom), test.availableTo && formatDate(test.availableTo)].filter(Boolean).join(' – ')}
        </p>
      )}

      {last && !inProgress && last.scorePercent !== null && (
        <Link to={`/exams/${last.examId}/result`} className="mt-3 inline-flex items-center gap-1.5 self-start text-sm text-slate-600 hover:text-indigo-700">
          <Trophy className="size-4 text-amber-500" aria-hidden />{t('dim.page.lastResult')}
          <span className="font-semibold text-slate-900 tabular-nums">{formatNumber(last.scorePercent)}%</span>
        </Link>
      )}

      {start.isError && (
        <div className="mt-3">
          <Alert title={t('dim.page.startFailed')}>
            {start.error.message}
            {activeExamId && <> <Link to={`/exams/${activeExamId}`} className="font-medium underline">{t('dim.page.goActive')}</Link></>}
            {!activeExamId && <SupportContact className="mt-1" topic={t('dim.start.failedTopic')} reference={test.title} />}
          </Alert>
        </div>
      )}

      <div className="mt-auto pt-4">
        {inProgress && last ? (
          <Link to={`/exams/${last.examId}`} className="btn-primary inline-flex h-11 w-full items-center justify-center gap-2 rounded-[10px] px-4 font-semibold">
            <Play className="size-4" aria-hidden />{t('dashboard.continue')}
          </Link>
        ) : (
          <Button className="w-full" disabled={!test.isOpen || limitReached || monthly.blocked} loading={start.isPending} onClick={() => { start.reset(); setChoosing(true) }}>
            <Play className="size-4" aria-hidden />{limitReached ? t('dim.page.limitReached') : last ? t('dim.page.restart') : t('dim.page.start')}
          </Button>
        )}
      </div>

      {choosing && (
        <DimStartDialog testId={test.id} title={test.title} questionCount={test.questionCount} durationMinutes={test.durationMinutes}
          starting={start.isPending} onStart={begin} onClose={() => setChoosing(false)} />
      )}
    </article>
  )
}

function Stat({ icon, label, children }: { icon: ReactNode; label: string; children: ReactNode }) {
  return (
    <div className="flex items-center gap-1.5">
      <dt className="text-slate-400"><span aria-hidden>{icon}</span><span className="sr-only">{label}</span></dt>
      <dd className="tabular-nums">{children}</dd>
    </div>
  )
}

