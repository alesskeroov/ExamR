import './exam-flow.css'
import { Clock, Eye, EyeOff, Loader2, PlayCircle } from 'lucide-react'
import { useId, useState, type ReactNode } from 'react'
import { Link, useNavigate, useSearchParams } from 'react-router'
import { Alert } from '../../components/common/Alert'
import { Button } from '../../components/common/Button'
import { PageHeader } from '../../components/common/PageHeader'
import { formatNumber } from '../../lib/format'
import { cn } from '../../lib/utils'
import { useCompletedDocuments } from '../documents/api'
import { useActiveExam, useCreateExam, useExamAvailability, type RevealMode } from './api'

const PRESETS = [10, 20, 30, 50]
const inputClass =
  'h-11 rounded-lg border border-slate-300 bg-white px-3 tabular-nums focus:border-indigo-500 focus:ring-2 focus:ring-indigo-500/20 focus:outline-none aria-invalid:border-rose-400'

export function ExamSetupPage() {
  const navigate = useNavigate()
  const active = useActiveExam()
  const documents = useCompletedDocuments()
  const create = useCreateExam()

  const [searchParams] = useSearchParams()
  const [documentId, setDocumentId] = useState(() => searchParams.get('document') ?? '')
  // Typed range; null = use the PDF's full question-number span.
  const [rangeInput, setRangeInput] = useState<{ from: string; to: string } | null>(null)
  const [count, setCount] = useState(20)
  const [revealMode, setRevealMode] = useState<RevealMode>('AtEnd')

  const span = useExamAvailability(documentId)
  const first = span.data?.firstNumber ?? null
  const last = span.data?.lastNumber ?? null
  const parse = (value: string) => (value.trim() === '' ? null : Number(value))
  const from = rangeInput ? parse(rangeInput.from) : first
  const to = rangeInput ? parse(rangeInput.to) : last

  const rangeError = validateRange(from, to, first, last)
  const range = documentId && !rangeError && from !== null && to !== null ? { from, to } : undefined
  const availability = useExamAvailability(range ? documentId : '', range)

  const available = range ? (availability.data?.available ?? 0) : 0
  const max = Math.min(available, availability.data?.maxQuestions ?? 200)
  const countError = !range
    ? null
    : !Number.isInteger(count) || count < 1
      ? 'Sual sayı ən azı 1 olmalıdır.'
      : count > max
        ? `Bu aralıqda ən çox ${formatNumber(max)} sual seçmək olar.`
        : null
  const minutes = Math.ceil(count * (availability.data?.minutesPerQuestion ?? 1))
  const selectedDocument = documents.data?.items.find((d) => d.id === documentId)

  const canStart =
    !!range && !countError && !availability.isFetching && !create.isPending && !active.data

  function selectDocument(id: string) {
    setDocumentId(id)
    setRangeInput(null)
  }

  function updateRange(field: 'from' | 'to', value: string) {
    setRangeInput((current) => ({ from: String(current?.from ?? from ?? ''), to: String(current?.to ?? to ?? ''), [field]: value }))
  }

  function start() {
    if (!range) return
    create.mutate(
      { questionCount: count, documentId, rangeFrom: range.from, rangeTo: range.to, revealMode },
      { onSuccess: (session) => navigate(`/exams/${session.id}`) },
    )
  }

  return (
    <div className="exam-flow mx-auto max-w-5xl">
      <PageHeader title="İmtahana başla" description="PDF-i, sual aralığını, sual sayını və cavab rejimini seçin." />

      {active.data && (
        <div className="mb-6">
          <Alert
            tone="info"
            title="Davam edən imtahanınız var"
            action={
              <Link to={`/exams/${active.data.id}`} className="inline-flex h-9 items-center rounded-lg btn-primary px-4 font-medium">
                Davam et
              </Link>
            }
          >
            Yeni imtahan başlamaq üçün əvvəlcə onu bitirin.
          </Alert>
        </div>
      )}

      <div className="grid gap-4 lg:grid-cols-[minmax(0,1fr)_300px]">
        <div className="flex min-w-0 flex-col gap-4">
          <Section step={1} title="PDF">
            <label htmlFor="document" className="sr-only">
              PDF
            </label>
            <select
              id="document"
              value={documentId}
              onChange={(e) => selectDocument(e.target.value)}
              className={cn(inputClass, 'w-full')}
            >
              <option value="">{documents.isPending ? 'Yüklənir…' : 'PDF seçin'}</option>
              {documents.data?.items.map((d) => (
                <option key={d.id} value={d.id}>
                  {d.originalFileName} · {formatNumber(d.questionCount)} sual
                </option>
              ))}
            </select>
            {documents.data?.items.length === 0 && (
              <p className="mt-2 text-slate-500">
                Hələ emal olunmuş PDF yoxdur.{' '}
                <Link to="/upload" className="font-medium text-indigo-600 hover:text-indigo-700">
                  PDF yükləyin
                </Link>
              </p>
            )}
          </Section>

          <Section step={2} title="Sual aralığı">
            <div className="grid grid-cols-[minmax(0,1fr)_auto_minmax(0,1fr)] items-end gap-2">
              <NumberField label="Başlanğıc sual" value={from} disabled={!documentId || span.isPending} invalid={!!rangeError} onChange={(v) => updateRange('from', v)} />
              <span className="pb-2.5 text-slate-400" aria-hidden>
                –
              </span>
              <NumberField label="Son sual" value={to} disabled={!documentId || span.isPending} invalid={!!rangeError} onChange={(v) => updateRange('to', v)} />
            </div>
            <p className={cn('mt-2', rangeError && documentId && !span.isPending ? 'text-rose-600' : 'text-slate-500')}>
              {!documentId
                ? 'Əvvəlcə PDF seçin.'
                : span.isPending
                  ? 'Yüklənir…'
                  : rangeError ?? `PDF-dəki orijinal sual nömrələri: ${first}–${last}. Suallar yalnız bu aralıqdan təsadüfi seçilir.`}
            </p>
          </Section>

          <Section step={3} title="Sual sayı">
            <div className="flex flex-wrap items-center gap-2">
              {PRESETS.map((preset) => (
                <button
                  key={preset}
                  type="button"
                  disabled={!range || preset > max}
                  onClick={() => setCount(preset)}
                  aria-pressed={count === preset}
                  className={cn(
                    'h-11 min-w-11 rounded-lg border px-3 font-medium tabular-nums transition-colors duration-150 disabled:cursor-not-allowed disabled:opacity-40',
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
                  aria-invalid={!!countError}
                  className={cn(inputClass, 'w-24')}
                />
              </label>
            </div>
            <p className={cn('mt-2', countError ? 'text-rose-600' : 'text-slate-500')}>
              {countError ?? (range ? `Aralıqda ${formatNumber(available)} unikal sual var. Suallar təkrarlanmır.` : 'Əvvəlcə aralığı təyin edin.')}
            </p>
          </Section>

          <Section step={4} title="Düzgün cavablar nə vaxt göstərilsin?">
            <div className="grid gap-3 sm:grid-cols-2">
              <ChoiceCard name="reveal" checked={revealMode === 'AtEnd'} onChange={() => setRevealMode('AtEnd')} icon={<EyeOff className="size-5" />} title="İmtahan bitdikdə" description="Cavablar sona qədər dəyişdirilə bilər" />
              <ChoiceCard name="reveal" checked={revealMode === 'Immediate'} onChange={() => setRevealMode('Immediate')} icon={<Eye className="size-5" />} title="Hər sualdan sonra" description="Cavab verildikdən sonra dəyişmir" />
            </div>
          </Section>
        </div>

        <aside className="min-w-0 lg:sticky lg:top-8 lg:self-start">
          <div className="rounded-xl border border-slate-200 bg-white p-4 sm:p-5">
            <h2 className="mb-4 font-semibold">Seçdiyiniz imtahan</h2>
            <dl className="flex flex-col gap-2.5 text-sm">
              <SummaryRow label="PDF">
                <span className="block max-w-full break-words text-right" title={selectedDocument?.originalFileName}>
                  {selectedDocument?.originalFileName ?? '—'}
                </span>
              </SummaryRow>
              <SummaryRow label="Aralıq">{range ? `${range.from}–${range.to}` : '—'}</SummaryRow>
              <SummaryRow label="Mövcud sual">
                {range && availability.isFetching ? <Loader2 className="size-4 animate-spin" /> : range ? formatNumber(available) : '—'}
              </SummaryRow>
              <SummaryRow label="Sual sayı">{range && !countError ? formatNumber(count) : '—'}</SummaryRow>
              <SummaryRow label="Vaxt">
                <span className="inline-flex items-center gap-1.5">
                  <Clock className="size-4 text-slate-400" aria-hidden />
                  {range && !countError ? `${minutes} dəqiqə` : '—'}
                </span>
              </SummaryRow>
              <SummaryRow label="Cavablar">{revealMode === 'AtEnd' ? 'Sonda' : 'Dərhal'}</SummaryRow>
            </dl>
            {range && !!availability.data?.ungraded && (
              <p className="mt-4 rounded-lg bg-amber-50 px-3 py-2 text-amber-800">
                Aralıqdakı {formatNumber(availability.data.ungraded)} sualın düzgün cavabı məlum deyil — düşsə, ballanmayacaq.
              </p>
            )}
            {create.error && (
              <p role="alert" className="mt-4 rounded-lg bg-rose-50 px-3 py-2 text-rose-700">
                {create.error.message}
              </p>
            )}
            <Button className="mt-5 w-full" disabled={!canStart} loading={create.isPending} onClick={start}>
              <PlayCircle className="size-4" aria-hidden />
              İmtahana başla
            </Button>
          </div>
        </aside>
      </div>
    </div>
  )
}

/** Returns an error message, or null when the range is valid for the PDF's numbering. */
function validateRange(from: number | null, to: number | null, first: number | null, last: number | null) {
  if (first === null || last === null) return 'Bu PDF-də sual yoxdur.'
  if (from === null || to === null || !Number.isInteger(from) || !Number.isInteger(to)) return 'Başlanğıc və son sual nömrəsini daxil edin.'
  if (from > to) return 'Başlanğıc nömrə son nömrədən böyük ola bilməz.'
  if (from < first || to > last) return `Aralıq PDF-dəki nömrələr daxilində olmalıdır (${first}–${last}).`
  return null
}

interface NumberFieldProps {
  label: string
  value: number | null
  disabled: boolean
  invalid: boolean
  onChange: (value: string) => void
}

function NumberField({ label, value, disabled, invalid, onChange }: NumberFieldProps) {
  const id = useId()
  return (
    <div className="flex min-w-0 flex-col gap-1.5">
      <label htmlFor={id} className="font-medium text-slate-700">
        {label}
      </label>
      <input
        id={id}
        type="number"
        min={1}
        value={value === null || Number.isNaN(value) ? '' : value}
        disabled={disabled}
        aria-invalid={invalid}
        onChange={(e) => onChange(e.target.value)}
        className={cn(inputClass, 'w-full disabled:bg-slate-50 disabled:text-slate-400')}
      />
    </div>
  )
}

function Section({ step, title, children }: { step: number; title: string; children: ReactNode }) {
  const id = useId()
  return (
    <section aria-labelledby={id} className="rounded-xl border border-slate-200 bg-white p-4 sm:p-5">
      <h2 id={id} className="mb-3 flex items-center gap-2 text-sm font-semibold">
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
        'flex cursor-pointer items-start gap-3 rounded-lg border p-3 text-sm transition-colors duration-150 has-[:focus-visible]:ring-2 has-[:focus-visible]:ring-indigo-500',
        checked ? 'border-indigo-500 bg-indigo-50' : 'border-slate-200 hover:border-slate-300 hover:bg-slate-50',
      )}
    >
      <input type="radio" name={name} checked={checked} onChange={onChange} className="sr-only" />
      <span className={cn('mt-0.5', checked ? 'text-indigo-600' : 'text-slate-400')} aria-hidden>
        {icon}
      </span>
      <span>
        <span className="block font-medium">{title}</span>
        <span className="mt-1 block text-xs leading-5 text-slate-500">{description}</span>
      </span>
    </label>
  )
}

function SummaryRow({ label, children }: { label: string; children: ReactNode }) {
  return (
    <div className="grid grid-cols-[minmax(0,1fr)_minmax(0,1.5fr)] items-start gap-3">
      <dt className="text-slate-500">{label}</dt>
      <dd className="min-w-0 text-right font-medium tabular-nums">{children}</dd>
    </div>
  )
}
