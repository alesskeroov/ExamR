import { Pencil, Plus, Trash2 } from 'lucide-react'
import { useState, type ReactNode } from 'react'
import { cn } from '../../lib/utils'
import { Alert } from '../../components/common/Alert'
import { Button } from '../../components/common/Button'
import { EmptyState } from '../../components/common/EmptyState'
import { StatusBadge } from '../../components/common/StatusBadge'
import { formatDate } from '../../lib/format'
import { useConfirm } from '../../lib/use-confirm'
import {
  useCreateDimTest, useDeleteDimTest, useDimAdminTests, useDimBankStats, useDimCatalog, useUpdateDimTest,
  type DimAdminTest, type DimCatalog, type DimTestKind, type DimTestSettings, type DimTrack,
} from './api'
import { fromLocalInput, inputClass, toLocalInput, trackLabel } from './ui'

interface SettingsForm { title: string; duration: string; limit: string; from: string; to: string; publish: boolean }

const toSettings = (f: SettingsForm): DimTestSettings => ({
  title: f.title.trim(),
  durationMinutes: Number(f.duration),
  attemptLimit: f.limit ? Number(f.limit) : null,
  availableFrom: fromLocalInput(f.from),
  availableTo: fromLocalInput(f.to),
  isPublished: f.publish,
})

const fromTest = (t: DimAdminTest): SettingsForm => ({
  title: t.title, duration: String(t.durationMinutes), limit: t.attemptLimit ? String(t.attemptLimit) : '',
  from: toLocalInput(t.availableFrom), to: toLocalInput(t.availableTo), publish: t.isPublished,
})

/** Subjects a new test must contain, with the official closed-question count as the default size. */
function sectionSubjects(catalog: DimCatalog, track: DimTrack, kind: DimTestKind, group: string, subject: string) {
  const specs = track === 'Buraxilis' ? catalog.buraxilis : catalog.blok
  const codes = kind === 'Subject' ? (subject ? [subject] : [])
    : track === 'Buraxilis' ? specs.map(s => s.code)
    : catalog.groups.find(g => g.code === group)?.subjects.map(s => s.code) ?? []
  return codes.map(code => specs.find(s => s.code === code)!).filter(Boolean)
}

export function DimTestsAdmin() {
  const tests = useDimAdminTests()
  const [creating, setCreating] = useState(false)

  return (
    <div className="flex flex-col gap-5">
      {creating ? <CreateTest onDone={() => setCreating(false)} /> : (
        <div><Button onClick={() => setCreating(true)}><Plus className="size-4" aria-hidden />Yeni sınaq</Button></div>
      )}
      {tests.isPending ? <div className="h-40 animate-pulse rounded-xl bg-slate-200/60" aria-label="Yüklənir" />
        : tests.isError ? <Alert title="Sınaqlar yüklənmədi">{tests.error.message}</Alert>
        : tests.data.length === 0 ? <EmptyState title="Hələ sınaq yoxdur" description="Sual bankından ilk sınağı yaradın." />
        : <ul className="flex flex-col gap-3">{tests.data.map(t => <li key={t.id}><TestRow test={t} /></li>)}</ul>}
    </div>
  )
}

/** Name, duration and publish are always shown; rarely used settings sit under "Əlavə parametrlər". */
function SettingsFields({ value, onChange, advanced, openAdvanced = false }: {
  value: SettingsForm; onChange: (next: SettingsForm) => void; advanced?: ReactNode; openAdvanced?: boolean
}) {
  const set = (patch: Partial<SettingsForm>) => onChange({ ...value, ...patch })
  return (
    <div className="flex flex-col gap-3">
      <div className="grid gap-3 sm:grid-cols-[1fr_8rem]">
        <label className="flex flex-col gap-1.5 text-sm font-medium text-slate-700">Ad
          <input className={inputClass} value={value.title} maxLength={200} onChange={e => set({ title: e.target.value })} placeholder="məs. Buraxılış sınağı №1" />
        </label>
        <label className="flex flex-col gap-1.5 text-sm font-medium text-slate-700">Müddət (dəq)
          <input className={inputClass} type="number" min={1} max={600} value={value.duration} onChange={e => set({ duration: e.target.value })} />
        </label>
      </div>
      <label className="flex items-center gap-2 text-sm font-medium text-slate-700">
        <input type="checkbox" className="size-4 accent-indigo-600" checked={value.publish} onChange={e => set({ publish: e.target.checked })} />
        Dərhal dərc et (istifadəçilər görsün)
      </label>
      <details className="rounded-lg border border-slate-200 p-3 text-sm" open={openAdvanced || undefined}>
        <summary className="cursor-pointer font-medium text-slate-600">Əlavə parametrlər</summary>
        <div className="mt-3 flex flex-col gap-4">
          {advanced}
          <div className="grid gap-3 sm:grid-cols-3">
            <label className="flex flex-col gap-1.5 font-medium text-slate-700">Cəhd limiti (boş = limitsiz)
              <input className={inputClass} type="number" min={1} max={100} value={value.limit} onChange={e => set({ limit: e.target.value })} />
            </label>
            <label className="flex flex-col gap-1.5 font-medium text-slate-700">Başlama vaxtı (boş = istənilən vaxt)
              <input className={inputClass} type="datetime-local" value={value.from} onChange={e => set({ from: e.target.value })} />
            </label>
            <label className="flex flex-col gap-1.5 font-medium text-slate-700">Bitmə vaxtı
              <input className={inputClass} type="datetime-local" value={value.to} onChange={e => set({ to: e.target.value })} />
            </label>
          </div>
        </div>
      </details>
    </div>
  )
}

function CreateTest({ onDone }: { onDone: () => void }) {
  const catalog = useDimCatalog()
  const stats = useDimBankStats()
  const create = useCreateDimTest()
  const [track, setTrack] = useState<DimTrack>('Buraxilis')
  const [kind, setKind] = useState<DimTestKind>('Full')
  const [group, setGroup] = useState('')
  const [subject, setSubject] = useState('')
  const [perAttempt, setPerAttempt] = useState(true)
  const [counts, setCounts] = useState<Record<string, string>>({})
  const [settings, setSettings] = useState<SettingsForm>({ title: '', duration: '180', limit: '', from: '', to: '', publish: false })

  if (!catalog.data) return <div className="h-40 animate-pulse rounded-xl bg-slate-200/60" aria-label="Yüklənir" />
  const sections = sectionSubjects(catalog.data, track, kind, group, subject)
  const subjectOptions = track === 'Buraxilis' ? catalog.data.buraxilis : catalog.data.blok
  const inBank = (code: string) => stats.data?.find(s => s.track === track && s.subject === code)?.count ?? 0
  const countOf = (code: string, closed: number) => counts[`${track}-${code}`] ?? String(closed)
  const reset = (patch: () => void) => { patch(); create.reset() }
  const shortSubjects = sections.filter(s => Number(countOf(s.code, s.closed)) > inBank(s.code))

  const submit = () => create.mutate({
    ...toSettings(settings), track, kind, groupCode: track === 'Blok' && kind === 'Full' ? group : null,
    sections: sections.map(s => ({ subject: s.code, randomCount: Number(countOf(s.code, s.closed)), drawPerAttempt: perAttempt })),
  }, { onSuccess: onDone })

  return (
    <section className="rounded-xl border border-indigo-200 bg-white p-4 sm:p-5">
      <h2 className="font-semibold">Yeni sınaq</h2>
      <div className="mt-3 grid gap-3 sm:grid-cols-3">
        <label className="flex flex-col gap-1.5 text-sm font-medium text-slate-700">İmtahan
          <select className={inputClass} value={track} onChange={e => reset(() => { setTrack(e.target.value as DimTrack); setSubject(''); setGroup('') })}>
            <option value="Buraxilis">Buraxılış (XI sinif, 300 bal)</option>
            <option value="Blok">Blok (400 bal)</option>
          </select>
        </label>
        <label className="flex flex-col gap-1.5 text-sm font-medium text-slate-700">Növ
          <select className={inputClass} value={kind} onChange={e => reset(() => {
            const next = e.target.value as DimTestKind
            setKind(next)
            setSettings(s => ({ ...s, duration: next === 'Full' ? '180' : '60' }))
          })}>
            <option value="Full">Tam sınaq</option>
            <option value="Subject">Fənn sınağı</option>
          </select>
        </label>
        {kind === 'Subject' ? (
          <label className="flex flex-col gap-1.5 text-sm font-medium text-slate-700">Fənn
            <select className={inputClass} value={subject} onChange={e => setSubject(e.target.value)}>
              <option value="">Fənn seçin</option>
              {subjectOptions.map(s => <option key={s.code} value={s.code}>{s.name}</option>)}
            </select>
          </label>
        ) : track === 'Blok' ? (
          <label className="flex flex-col gap-1.5 text-sm font-medium text-slate-700">İxtisas qrupu
            <select className={inputClass} value={group} onChange={e => setGroup(e.target.value)}>
              <option value="">Qrup seçin</option>
              {catalog.data.groups.map(g => <option key={g.code} value={g.code}>{g.name}</option>)}
            </select>
          </label>
        ) : null}
      </div>

      {sections.length > 0 && (
        <p className={cn('mt-3 text-sm', shortSubjects.length > 0 ? 'text-rose-700' : 'text-slate-600')}>
          {sections.map(s => `${s.name} ${countOf(s.code, s.closed)}`).join(' · ')}
          {shortSubjects.length > 0 && ` — bankda kifayət qədər sual yoxdur: ${shortSubjects.map(s => s.name).join(', ')}`}
        </p>
      )}

      <div className="mt-4"><SettingsFields value={settings} onChange={setSettings} openAdvanced={shortSubjects.length > 0} advanced={sections.length > 0 && (
        <fieldset>
          <legend className="mb-2 font-medium text-slate-700">Fənlər üzrə sual sayı (default: rəsmi qapalı sual sayı)</legend>
          <ul className="flex flex-col gap-2">
            {sections.map(s => {
              const value = countOf(s.code, s.closed)
              const short = Number(value) > inBank(s.code)
              return (
                <li key={s.code} className="flex flex-wrap items-center gap-3">
                  <span className="min-w-36 font-medium">{s.name}</span>
                  <input className={`${inputClass} w-24`} type="number" min={1} max={200} aria-label={`${s.name} sual sayı`} value={value}
                    onChange={e => setCounts(c => ({ ...c, [`${track}-${s.code}`]: e.target.value }))} />
                  <span className={short ? 'text-rose-700' : 'text-slate-500'}>bankda {inBank(s.code)} sual</span>
                </li>
              )
            })}
          </ul>
          <div className="mt-3 flex flex-col gap-1.5 text-slate-700 sm:flex-row sm:gap-5">
            <label className="flex items-center gap-2"><input type="radio" name="dim-mode" className="accent-indigo-600" checked={perAttempt} onChange={() => setPerAttempt(true)} />Hər cəhddə yeni təsadüfi suallar</label>
            <label className="flex items-center gap-2"><input type="radio" name="dim-mode" className="accent-indigo-600" checked={!perAttempt} onChange={() => setPerAttempt(false)} />Hamı eyni sualları alsın</label>
          </div>
        </fieldset>
      )} /></div>
      {create.isError && <div className="mt-3"><Alert title="Sınaq yaradılmadı">{create.error.message}</Alert></div>}
      <div className="mt-4 flex gap-2">
        <Button loading={create.isPending} disabled={sections.length === 0 || settings.title.trim().length < 2 || !settings.duration} onClick={submit}>Yarat</Button>
        <Button variant="ghost" onClick={onDone}>Ləğv et</Button>
      </div>
    </section>
  )
}

function TestRow({ test }: { test: DimAdminTest }) {
  const update = useUpdateDimTest()
  const remove = useDeleteDimTest()
  const confirm = useConfirm()
  const [editing, setEditing] = useState<SettingsForm | null>(null)
  const catalog = useDimCatalog()
  const groupName = catalog.data?.groups.find(g => g.code === test.groupCode)?.name

  const save = (form: SettingsForm) => update.mutate({ id: test.id, settings: toSettings(form) }, { onSuccess: () => setEditing(null) })
  const destroy = async () => {
    if (await confirm({ title: 'Sınaq silinsin?', description: test.attempts > 0
      ? `"${test.title}" və ona edilmiş ${test.attempts} cəhd (istifadəçilərin nəticələri) birdəfəlik silinəcək. Bu geri qaytarıla bilməz.`
      : `"${test.title}" silinəcək.`, confirmLabel: 'Sil', danger: true }))
      remove.mutate(test.id)
  }

  return (
    <article className="rounded-xl border border-slate-200 bg-white p-4">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div className="min-w-0">
          <p className="text-xs font-medium tracking-wide text-indigo-700 uppercase">
            {trackLabel[test.track]} · {test.kind === 'Full' ? 'Tam sınaq' : 'Fənn sınağı'}{groupName && ` · ${groupName}`}
          </p>
          <h3 className="mt-0.5 font-semibold">{test.title}</h3>
          <p className="mt-1 text-sm text-slate-600">
            {test.sections.map(s => `${s.name} ${s.questionCount}${s.random ? ' (təsadüfi)' : ''}`).join(' · ')}
          </p>
          <p className="mt-0.5 text-sm text-slate-500">
            {test.durationMinutes} dəq · {test.attemptLimit ? `${test.attemptLimit} cəhd` : 'limitsiz cəhd'} · {test.attempts} cəhd edilib
            {(test.availableFrom || test.availableTo) && ` · ${[test.availableFrom && formatDate(test.availableFrom), test.availableTo && formatDate(test.availableTo)].filter(Boolean).join(' – ')}`}
          </p>
        </div>
        <StatusBadge tone={test.isPublished ? 'success' : 'neutral'}>{test.isPublished ? 'Dərc edilib' : 'Qaralama'}</StatusBadge>
      </div>

      {editing ? (
        <div className="mt-4 border-t border-slate-100 pt-4">
          <SettingsFields value={editing} onChange={setEditing} />
          <div className="mt-3 flex gap-2">
            <Button loading={update.isPending} onClick={() => save(editing)}>Yadda saxla</Button>
            <Button variant="ghost" onClick={() => setEditing(null)}>Ləğv et</Button>
          </div>
        </div>
      ) : (
        <div className="mt-3 flex flex-wrap gap-2">
          <Button variant="secondary" loading={update.isPending} onClick={() => save({ ...fromTest(test), publish: !test.isPublished })}>
            {test.isPublished ? 'Dərcdən çıxar' : 'Dərc et'}
          </Button>
          <Button variant="ghost" onClick={() => setEditing(fromTest(test))}><Pencil className="size-4" aria-hidden />Redaktə</Button>
          <Button variant="ghost" loading={remove.isPending} onClick={() => void destroy()}><Trash2 className="size-4" aria-hidden />Sil</Button>
        </div>
      )}
      {(update.isError || remove.isError) && <div className="mt-3"><Alert title="Əməliyyat alınmadı">{(update.error ?? remove.error)?.message}</Alert></div>}
    </article>
  )
}
