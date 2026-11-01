import { Clock, Eye, EyeOff, Files, FileText, Loader2, PlayCircle } from 'lucide-react'
import { useId, useState, type ReactNode } from 'react'
import { Link, useNavigate } from 'react-router'
import { Alert } from '../../components/common/Alert'
import { Button } from '../../components/common/Button'
import { PageHeader } from '../../components/common/PageHeader'
import { ApiError } from '../../lib/api-client'
import { formatNumber } from '../../lib/format'
import { cn } from '../../lib/utils'
import { useCompletedDocuments } from '../documents/api'
import { useActiveExam, useCreateExam, useExamAvailability, type RevealMode } from './api'

type Source = 'all' | 'document'
const PRESETS = [10, 20, 30, 50]

export function ExamSetupPage() {
  const navigate = useNavigate()
  const active = useActiveExam()
  const documents = useCompletedDocuments()
  const create = useCreateExam()

  const [source, setSource] = useState<Source>('all')
  const [documentId, setDocumentId] = useState('')
  const [count, setCount] = useState(30)
  const [revealMode, setRevealMode] = useState<RevealMode>('AtEnd')

  const documentIds = source === 'document' && documentId ? [documentId] : []
  const availability = useExamAvailability(documentIds)
  const available = availability.data?.available ?? 0
  const max = Math.min(available, availability.data?.maxQuestions ?? 200)
  const minutes = Math.ceil(count * (availability.data?.minutesPerQuestion ?? 1))

  const sourceMissing = source === 'document' && !documentId
  const countInvalid = !Number.isInteger(count) || count < 1 || count > max
  const canStart = !sourceMissing && !countInvalid && !availability.isFetching && !create.isPending && !active.data

  const createError = create.error instanceof ApiError ? create.error : null

  function start() {
    create.mutate({ questionCount: count, documentIds, revealMode }, { onSuccess: (session) => navigate(`/exams/${session.id}`) })
  }

  return (
    <>
      <PageHeader title="İmtahana başla" description="Sual mənbəyini, sayını və cavab rejimini seçin." />

      {active.data && (
        <div className="mb-6">
          <Alert
            tone="info"
            title="Davam edən imtahanınız var"
            action={
              <Link to={`/exams/${active.data.id}`} className="inline-flex h-9 items-center rounded-lg bg-indigo-600 px-4 font-medium text-white hover:bg-indigo-700">
                Davam et
              </Link>
            }
          >
            Yeni imtahan başlamaq üçün əvvəlcə onu bitirin.
          </Alert>
        </div>
      )}

      <div className="grid gap-6 lg:grid-cols-[minmax(0,1fr)_320px]">
        <div className="flex min-w-0 flex-col gap-6">
          <Section step={1} title="Sual mənbəyi">
            <div className="grid gap-3 sm:grid-cols-2">
              <ChoiceCard name="source" checked={source === 'all'} onChange={() => setSource('all')} icon={<Files className="size-5" />} title="Bütün PDF-lər" description="Bazadakı bütün suallardan" />
              <ChoiceCard name="source" checked={source === 'document'} onChange={() => setSource('document')} icon={<FileText className="size-5" />} title="Konkret PDF" description="Yalnız seçilmiş fayldan" />
            </div>
            {source === 'document' && (
              <div className="mt-4">
                <label htmlFor="document" className="mb-1.5 block font-medium text-slate-700">
                  PDF
                </label>
                <select
                  id="document"
                  value={documentId}
                  onChange={(e) => setDocumentId(e.target.value)}
                  className="h-10 w-full rounded-lg border border-slate-300 bg-white px-3 focus:border-indigo-500 focus:ring-2 focus:ring-indigo-500/20 focus:outline-none"
                >
                  <option value="">{documents.isPending ? 'Yüklənir…' : 'PDF seçin'}</option>
                  {documents.data?.items.map((d) => (
                    <option key={d.id} value={d.id}>
                      {d.originalFileName} · {formatNumber(d.questionCount)} sual
                    </option>
                  ))}
                </select>
              </div>
            )}
          </Section>

          <Section step={2} title="Sual sayı">
            <div className="flex flex-wrap items-center gap-2">
              {PRESETS.map((preset) => (
                <button
                  key={preset}
                  type="button"
                  disabled={preset > max}
                  onClick={() => setCount(preset)}
                  aria-pressed={count === preset}
                  className={cn(
                    'h-10 min-w-14 rounded-lg border px-3 font-medium tabular-nums transition-colors duration-150 disabled:cursor-not-allowed disabled:opacity-40',
                    count === preset ? 'border-indigo-600 bg-indigo-600 text-white' : 'border-slate-300 bg-white text-slate-700 hover:bg-slate-50',
                  )}
                >
                  {preset}
                </button>
              ))}
              <label className="flex items-center gap-2">
                <span className="sr-only">Sual sayı</span>
                <input
                  type="number"
                  min={1}
                  max={max || undefined}
                  value={Number.isNaN(count) ? '' : count}
                  onChange={(e) => setCount(e.target.valueAsNumber)}
                  className="h-10 w-24 rounded-lg border border-slate-300 bg-white px-3 tabular-nums focus:border-indigo-500 focus:ring-2 focus:ring-indigo-500/20 focus:outline-none"
                />
              </label>
            </div>
            <p className={cn('mt-2', countInvalid && !availability.isFetching && !sourceMissing ? 'text-rose-600' : 'text-slate-500')}>
              {sourceMissing ? 'Əvvəlcə PDF seçin.' : `1 ilə ${formatNumber(max)} arasında. Suallar təsadüfi seçilir və təkrarlanmır.`}
            </p>
          </Section>

          <Section step={3} title="Düzgün cavablar nə vaxt göstərilsin?">
            <div className="grid gap-3 sm:grid-cols-2">
              <ChoiceCard name="reveal" checked={revealMode === 'AtEnd'} onChange={() => setRevealMode('AtEnd')} icon={<EyeOff className="size-5" />} title="İmtahan bitdikdə" description="Cavablar sona qədər dəyişdirilə bilər" />
              <ChoiceCard name="reveal" checked={revealMode === 'Immediate'} onChange={() => setRevealMode('Immediate')} icon={<Eye className="size-5" />} title="Hər sualdan sonra" description="Cavab verildikdən sonra dəyişmir" />
            </div>
          </Section>
        </div>

        <aside className="lg:sticky lg:top-8 lg:self-start">
          <div className="rounded-xl border border-slate-200 bg-white p-5 shadow-sm">
            <h2 className="mb-4 font-semibold">Xülasə</h2>
            <dl className="flex flex-col gap-3">
              <SummaryRow label="Mövcud unikal sual">
                {availability.isPending ? <Loader2 className="size-4 animate-spin" /> : formatNumber(available)}
              </SummaryRow>
              <SummaryRow label="Sual sayı">{countInvalid ? '—' : formatNumber(count)}</SummaryRow>
              <SummaryRow label="Vaxt">
                <span className="inline-flex items-center gap-1.5">
                  <Clock className="size-4 text-slate-400" aria-hidden />
                  {countInvalid ? '—' : `${minutes} dəqiqə`}
                </span>
              </SummaryRow>
              <SummaryRow label="Cavablar">{revealMode === 'AtEnd' ? 'Sonda' : 'Dərhal'}</SummaryRow>
            </dl>
            {!!availability.data?.ungraded && (
              <p className="mt-4 rounded-lg bg-amber-50 px-3 py-2 text-amber-800">
                {formatNumber(availability.data.ungraded)} sualın düzgün cavabı məlum deyil — düşsə, ballanmayacaq.
              </p>
            )}
            {create.error && (
              <p role="alert" className="mt-4 rounded-lg bg-rose-50 px-3 py-2 text-rose-700">
                {createError?.message ?? create.error.message}
              </p>
            )}
            <Button className="mt-5 w-full" disabled={!canStart} loading={create.isPending} onClick={start}>
              <PlayCircle className="size-4" aria-hidden />
              İmtahana başla
            </Button>
          </div>
        </aside>
      </div>
    </>
  )
}

function Section({ step, title, children }: { step: number; title: string; children: ReactNode }) {
  const id = useId()
  return (
    <section aria-labelledby={id} className="rounded-xl border border-slate-200 bg-white p-5 shadow-sm">
      <h2 id={id} className="mb-4 flex items-center gap-2 font-semibold">
        <span className="flex size-6 items-center justify-center rounded-full bg-slate-100 text-xs text-slate-600">{step}</span>
        {title}
      </h2>
      {children}
    </section>
  )
}

interface ChoiceCardProps {
  name: string
  checked: boolean
  onChange: () => void
  icon: ReactNode
  title: string
  description: string
}

function ChoiceCard({ name, checked, onChange, icon, title, description }: ChoiceCardProps) {
  return (
    <label
      className={cn(
        'flex cursor-pointer items-start gap-3 rounded-lg border p-4 transition-colors duration-150 has-[:focus-visible]:ring-2 has-[:focus-visible]:ring-indigo-500',
        checked ? 'border-indigo-500 bg-indigo-50' : 'border-slate-200 hover:border-slate-300 hover:bg-slate-50',
      )}
    >
      <input type="radio" name={name} checked={checked} onChange={onChange} className="sr-only" />
      <span className={cn('mt-0.5', checked ? 'text-indigo-600' : 'text-slate-400')} aria-hidden>
        {icon}
      </span>
      <span>
        <span className="block font-medium">{title}</span>
        <span className="block text-slate-500">{description}</span>
      </span>
    </label>
  )
}

function SummaryRow({ label, children }: { label: string; children: ReactNode }) {
  return (
    <div className="flex items-center justify-between gap-3">
      <dt className="text-slate-500">{label}</dt>
      <dd className="font-medium tabular-nums">{children}</dd>
    </div>
  )
}
