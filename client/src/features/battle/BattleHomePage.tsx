import { UsageMeter } from '../../components/common/UsageMeter'
import { useUsageLimit } from '../usage/api'
import { LogIn, Swords, Users } from 'lucide-react'
import { RankLeaderboard, RankSummary } from './RankSummary'
import { useId, useState, type FormEvent, type ReactNode } from 'react'
import { Link, useNavigate } from 'react-router'
import { Button } from '../../components/common/Button'
import { PageHeader } from '../../components/common/PageHeader'
import { ApiError } from '../../lib/api-client'
import { formatNumber } from '../../lib/format'
import { cn } from '../../lib/utils'
import { useCompletedDocuments } from '../documents/api'
import { useExamAvailability } from '../exams/api'
import { MAX_BATTLE_QUESTIONS, useCreateBattle } from './api'
import { useTranslation } from 'react-i18next'

const inputClass =
  'h-10 w-full rounded-lg border border-slate-300 bg-white px-3 tabular-nums focus:border-indigo-500 focus:ring-2 focus:ring-indigo-500/20 focus:outline-none disabled:bg-slate-50 disabled:text-slate-400'
const SECONDS = [15, 20, 25, 30]

export function BattleHomePage() {
  const { t } = useTranslation()
  return (
    <>
      <PageHeader title="Battle" description={t('battle.home.description')} />
      <Link to="/battle/rooms" className="mb-5 inline-block font-medium text-indigo-600 hover:text-indigo-700">{t('nav.items.rooms')} →</Link>
      <div className="mb-5"><RankSummary /></div>
      <div className="grid gap-6 lg:grid-cols-[minmax(0,1fr)_340px]">
        <CreateRoomCard />
        <JoinRoomCard />
      </div>
      <RankLeaderboard />
    </>
  )
}

function CreateRoomCard() {
  const { t } = useTranslation()
  const monthly = useUsageLimit('Battle')
  const navigate = useNavigate()
  const documents = useCompletedDocuments(true)
  const create = useCreateBattle()

  const [documentId, setDocumentId] = useState('')
  const [name, setName] = useState('')
  const [range, setRange] = useState<{ from: string; to: string } | null>(null)
  const [count, setCount] = useState('10')
  const [seconds, setSeconds] = useState(20)

  const span = useExamAvailability(documentId)
  const from = range ? Number(range.from) : (span.data?.firstNumber ?? null)
  const to = range ? Number(range.to) : (span.data?.lastNumber ?? null)
  const rangeValid =
    from !== null && to !== null && Number.isInteger(from) && Number.isInteger(to) && from <= to &&
    from >= (span.data?.firstNumber ?? 0) && to <= (span.data?.lastNumber ?? 0)
  const availability = useExamAvailability(rangeValid ? documentId : '', rangeValid ? { from: from!, to: to! } : undefined)
  // Battles only use questions with a known correct answer.
  const gradable = Math.max(0, (availability.data?.available ?? 0) - (availability.data?.ungraded ?? 0))
  const max = Math.min(gradable, MAX_BATTLE_QUESTIONS)
  const questionCount = Number(count)
  const countValid = Number.isInteger(questionCount) && questionCount >= 1 && questionCount <= max

  const existingCode = create.error instanceof ApiError && create.error.status === 409 ? (create.error.problem?.code as string | undefined) : undefined

  function submit(event: FormEvent) {
    event.preventDefault()
    if (monthly.blocked || !rangeValid || !countValid) return
    create.mutate(
      { name, documentId, rangeFrom: from!, rangeTo: to!, questionCount, secondsPerQuestion: seconds },
      { onSuccess: ({ code }) => navigate(`/battle/${code}`) },
    )
  }

  return (
    <Card icon={<Swords className="size-5" />} title={t('battle.home.create')} subtitle={t('battle.home.createText')}>
      <UsageMeter kind="Battle" />
      <form onSubmit={submit} className="flex flex-col gap-4" noValidate>
        <Field label={t('battle.home.roomName')}>
          {id => <input id={id} value={name} maxLength={100} onChange={e => setName(e.target.value)} placeholder={t('battle.home.roomNamePlaceholder')} className={inputClass} />}
        </Field>
        <Field label="PDF">
          {(id) => (
            <select
              id={id}
              value={documentId}
              onChange={(e) => {
                setDocumentId(e.target.value)
                setRange(null)
              }}
              className={inputClass}
            >
              <option value="">{documents.isPending ? t('common.loadingDots') : t('exam.setup.pickPdf')}</option>
              {documents.data?.items.map((d) => (
                <option key={d.id} value={d.id}>
                  {d.originalFileName} · {t('common.questions', { count: d.questionCount })}
                </option>
              ))}
            </select>
          )}
        </Field>

        <div className="grid grid-cols-2 gap-3">
          <Field label={t('exam.setup.fromQuestion')}>
            {(id) => (
              <input id={id} type="number" min={1} className={inputClass} disabled={!documentId || span.isPending}
                value={from ?? ''} onChange={(e) => setRange({ from: e.target.value, to: String(to ?? '') })} aria-invalid={!!documentId && !rangeValid} />
            )}
          </Field>
          <Field label={t('exam.setup.toQuestion')}>
            {(id) => (
              <input id={id} type="number" min={1} className={inputClass} disabled={!documentId || span.isPending}
                value={to ?? ''} onChange={(e) => setRange({ from: String(from ?? ''), to: e.target.value })} aria-invalid={!!documentId && !rangeValid} />
            )}
          </Field>
        </div>

        <div className="grid grid-cols-2 gap-3">
          <Field label={t('exam.setup.count')}>
            {(id) => (
              <input id={id} type="number" min={1} max={max || undefined} className={inputClass} disabled={!rangeValid}
                value={count} onChange={(e) => setCount(e.target.value)} aria-invalid={rangeValid && !countValid} />
            )}
          </Field>
          <Field label={t('battle.home.timePerQuestion')}>
            {(id) => (
              <select id={id} value={seconds} onChange={(e) => setSeconds(Number(e.target.value))} className={inputClass}>
                {SECONDS.map((s) => (
                  <option key={s} value={s}>
                    {t('battle.home.seconds', { count: s })}
                  </option>
                ))}
              </select>
            )}
          </Field>
        </div>

        <p className={cn(documentId && (!rangeValid || !countValid) && !span.isPending ? 'text-rose-600' : 'text-slate-500')}>
          {!documentId
            ? t('battle.home.pickPdfHint')
            : span.isPending
              ? t('common.loadingDots')
              : !rangeValid
                ? t('exam.setup.outOfRange', { first: span.data?.firstNumber, last: span.data?.lastNumber })
                : !countValid
                  ? t('battle.home.countRange', { max: formatNumber(max) })
                  : t('battle.home.available', { count: gradable })}
        </p>

        {create.error && (
          <p role="alert" className="rounded-lg bg-rose-50 px-3 py-2 text-rose-700">
            {create.error.message}{' '}
            {existingCode && (
              <button type="button" onClick={() => navigate(`/battle/${existingCode}`)} className="font-semibold underline">
                {t('battle.home.goToRoom')}
              </button>
            )}
          </p>
        )}

        <Button type="submit" disabled={monthly.blocked || !rangeValid || !countValid || availability.isFetching} loading={create.isPending}>
          <Users className="size-4" aria-hidden />
          {t('battle.home.create')}
        </Button>
      </form>
    </Card>
  )
}

function JoinRoomCard() {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const [code, setCode] = useState('')
  const normalized = code.trim().toUpperCase()
  const valid = /^[A-Z0-9]{6}$/.test(normalized)

  return (
    <Card icon={<LogIn className="size-5" />} title={t('battle.home.join')} subtitle={t('battle.home.joinText')}>
      <form
        onSubmit={(e) => {
          e.preventDefault()
          if (valid) navigate(`/battle/${normalized}`)
        }}
        className="flex flex-col gap-4"
      >
        <Field label={t('battle.home.code')}>
          {(id) => (
            <input
              id={id}
              value={code}
              onChange={(e) => setCode(e.target.value.toUpperCase())}
              maxLength={6}
              autoComplete="off"
              placeholder="ABC123"
              className={cn(inputClass, 'h-14 text-center font-mono text-2xl font-semibold tracking-[0.3em] uppercase')}
            />
          )}
        </Field>
        <Button type="submit" disabled={!valid}>
          {t('notifications.join')}
        </Button>
      </form>
    </Card>
  )
}

function Card({ icon, title, subtitle, children }: { icon: ReactNode; title: string; subtitle: string; children: ReactNode }) {
  return (
    <section className="self-start rounded-xl border border-slate-200 bg-white p-5 shadow-sm sm:p-6">
      <div className="mb-5 flex items-start gap-3">
        <div className="rounded-lg bg-indigo-50 p-2.5 text-indigo-600" aria-hidden>
          {icon}
        </div>
        <div>
          <h2 className="text-lg font-semibold">{title}</h2>
          <p className="text-slate-500">{subtitle}</p>
        </div>
      </div>
      {children}
    </section>
  )
}

function Field({ label, children }: { label: string; children: (id: string) => ReactNode }) {
  const id = useId()
  return (
    <div className="flex flex-col gap-1.5">
      <label htmlFor={id} className="font-medium text-slate-700">
        {label}
      </label>
      {children(id)}
    </div>
  )
}
