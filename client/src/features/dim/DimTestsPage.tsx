import { CalendarClock, ClipboardList, Clock, Layers, Play, RotateCw } from 'lucide-react'
import { useState } from 'react'
import { Link, useNavigate } from 'react-router'
import { Alert } from '../../components/common/Alert'
import { Button } from '../../components/common/Button'
import { EmptyState } from '../../components/common/EmptyState'
import { PageHeader } from '../../components/common/PageHeader'
import { StatusBadge } from '../../components/common/StatusBadge'
import { ApiError } from '../../lib/api-client'
import { formatDate, formatNumber } from '../../lib/format'
import { cn } from '../../lib/utils'
import { useDimCatalog, useDimTests, useStartDimTest, type DimGroup, type DimTest, type DimTrack } from './api'

type KindFilter = 'All' | 'Full' | 'Subject'

const copy: Record<DimTrack, { title: string; description: string; max: number }> = {
  Buraxilis: {
    title: 'Buraxılış imtahanı',
    description: 'XI sinif · Azərbaycan dili, Riyaziyyat, Xarici dil · 300 ballıq sistem',
    max: 300,
  },
  Blok: {
    title: 'Blok imtahanı',
    description: 'I–IV ixtisas qrupları · 3 fənn, əmsallar 1,5 / 1,5 / 1 · 400 ballıq sistem',
    max: 400,
  },
}

export function DimTestsPage({ track }: { track: DimTrack }) {
  const tests = useDimTests(track)
  const catalog = useDimCatalog()
  const [kind, setKind] = useState<KindFilter>('All')
  const [group, setGroup] = useState<string>('')
  const groups = track === 'Blok' ? catalog.data?.groups ?? [] : []
  const selectedGroup = groups.find(g => g.code === group)

  const visible = (tests.data ?? []).filter(t => (kind === 'All' || t.kind === kind) && inGroup(t, selectedGroup))

  return (
    <div className="mx-auto max-w-5xl">
      <PageHeader title={copy[track].title} description={copy[track].description} />

      <Alert tone="info" title="İlk versiya: yalnız qapalı (A–E) suallar">
        Rəsmi {copy[track].max} ballıq nəticə açıq tapşırıqları da əhatə edir. Nəticədə qapalı hissənin rəsmi düsturla balı və
        açıq hissədə eyni nəticə fərziyyəsi ilə təxmini bal göstərilir. Düzgün cavablar imtahan bitdikdən sonra açılır.
      </Alert>

      <div className="mt-5 flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
        <div role="tablist" aria-label="Sınaq növü" className="inline-flex w-full rounded-lg border border-slate-200 bg-white p-1 sm:w-auto">
          {([['All', 'Hamısı'], ['Full', 'Tam sınaqlar'], ['Subject', 'Fənn sınaqları']] as const).map(([value, label]) => (
            <button key={value} type="button" role="tab" aria-selected={kind === value} onClick={() => setKind(value)}
              className={cn('flex-1 whitespace-nowrap rounded-md px-2 py-1.5 text-sm font-medium transition-colors sm:flex-none sm:px-3',
                kind === value ? 'bg-indigo-600 text-white' : 'text-slate-600 hover:bg-slate-100')}>
              {label}
            </button>
          ))}
        </div>
        {groups.length > 0 && (
          <label className="flex items-center gap-2 text-sm text-slate-600">
            <span className="shrink-0">İxtisas qrupu</span>
            <select value={group} onChange={e => setGroup(e.target.value)}
              className="h-9 w-full rounded-lg border border-slate-300 bg-white px-2 text-sm text-slate-800 sm:w-48">
              <option value="">Bütün qruplar</option>
              {groups.map(g => <option key={g.code} value={g.code}>{g.name}</option>)}
            </select>
          </label>
        )}
      </div>

      <div className="mt-5">
        {tests.isPending ? (
          <div className="grid gap-4 md:grid-cols-2" aria-label="Yüklənir">
            {[0, 1, 2, 3].map(i => <div key={i} className="h-48 animate-pulse rounded-xl bg-slate-200/60" />)}
          </div>
        ) : tests.isError ? (
          <Alert title="Sınaqlar yüklənmədi">{tests.error.message}</Alert>
        ) : visible.length === 0 ? (
          <EmptyState icon={<ClipboardList className="size-6" />} title="Hələ sınaq yoxdur"
            description={tests.data.length === 0 ? 'Administrator sınaq dərc etdikdə burada görünəcək.' : 'Seçilmiş filtrə uyğun sınaq tapılmadı.'} />
        ) : (
          <ul className="grid gap-4 md:grid-cols-2">
            {visible.map(t => <li key={t.id}><TestCard test={t} /></li>)}
          </ul>
        )}
      </div>
    </div>
  )
}

/** A full test belongs to its group; a subject test to every group that has the subject. */
function inGroup(test: DimTest, group: DimGroup | undefined) {
  if (!group) return true
  return test.kind === 'Full' ? test.groupCode === group.code : test.sections.every(s => group.subjects.some(g => g.code === s.subject))
}

function TestCard({ test }: { test: DimTest }) {
  const navigate = useNavigate()
  const start = useStartDimTest()
  const last = test.lastAttempt
  const inProgress = last?.status === 'InProgress'
  const limitReached = test.attemptLimit !== null && test.attemptsUsed >= test.attemptLimit
  const activeExamId = start.error instanceof ApiError ? start.error.problem?.activeExamId as string | undefined : undefined

  return (
    <article className="flex h-full flex-col rounded-xl border border-slate-200 bg-white p-4 shadow-sm sm:p-5">
      <div className="flex items-start justify-between gap-3">
        <div className="min-w-0">
          <p className="text-xs font-medium tracking-wide text-indigo-700 uppercase">
            {test.kind === 'Full' ? 'Tam sınaq' : 'Fənn sınağı'}{test.groupName && ` · ${test.groupName}`}
          </p>
          <h2 className="mt-1 text-lg leading-snug font-semibold text-slate-900">{test.title}</h2>
        </div>
        {inProgress ? <StatusBadge tone="info">Davam edir</StatusBadge>
          : !test.isOpen ? <StatusBadge tone="warning">Hazırda bağlıdır</StatusBadge>
          : null}
      </div>

      <ul className="mt-3 flex flex-wrap gap-1.5" aria-label="Fənlər">
        {test.sections.map(s => (
          <li key={s.subject} className="rounded-full bg-slate-100 px-2.5 py-0.5 text-xs font-medium text-slate-700">
            {s.name} · {s.questionCount}
          </li>
        ))}
      </ul>

      <dl className="mt-4 grid grid-cols-2 gap-x-4 gap-y-2 text-sm text-slate-600">
        <div className="flex items-center gap-1.5"><Layers className="size-4 text-slate-400" aria-hidden /><dt className="sr-only">Suallar</dt><dd>{test.questionCount} sual</dd></div>
        <div className="flex items-center gap-1.5"><Clock className="size-4 text-slate-400" aria-hidden /><dt className="sr-only">Müddət</dt><dd>{test.durationMinutes} dəq</dd></div>
        <div className="flex items-center gap-1.5"><RotateCw className="size-4 text-slate-400" aria-hidden /><dt className="sr-only">Cəhdlər</dt>
          <dd>{test.attemptLimit === null ? `${test.attemptsUsed} cəhd · limitsiz` : `${test.attemptsUsed} / ${test.attemptLimit} cəhd`}</dd></div>
        {(test.availableFrom || test.availableTo) && (
          <div className="col-span-2 flex items-center gap-1.5"><CalendarClock className="size-4 text-slate-400" aria-hidden /><dt className="sr-only">Vaxt</dt>
            <dd>{[test.availableFrom && formatDate(test.availableFrom), test.availableTo && formatDate(test.availableTo)].filter(Boolean).join(' – ')}</dd></div>
        )}
      </dl>

      {start.isError && (
        <div className="mt-3">
          <Alert title="Sınaq başlamadı">
            {start.error.message}
            {activeExamId && <> <Link to={`/exams/${activeExamId}`} className="font-medium underline">Davam edən imtahana keç</Link></>}
          </Alert>
        </div>
      )}

      <div className="mt-auto flex flex-wrap items-center gap-2 pt-4">
        {inProgress && last ? (
          <Link to={`/exams/${last.examId}`} className="btn-primary inline-flex h-10 items-center gap-2 rounded-lg px-4 font-medium">
            <Play className="size-4" aria-hidden />Davam et
          </Link>
        ) : (
          <Button disabled={!test.isOpen || limitReached} loading={start.isPending}
            onClick={() => start.mutate(test.id, { onSuccess: ({ examId }) => navigate(`/exams/${examId}`) })}>
            <Play className="size-4" aria-hidden />{limitReached ? 'Cəhd limiti bitib' : 'Başla'}
          </Button>
        )}
        {last && !inProgress && (
          <Link to={`/exams/${last.examId}/result`} className="inline-flex h-10 items-center rounded-lg border border-slate-300 bg-white px-4 font-medium text-slate-700 hover:bg-slate-50">
            Son nəticə{last.scorePercent !== null && ` · ${formatNumber(last.scorePercent)}%`}
          </Link>
        )}
      </div>
    </article>
  )
}
