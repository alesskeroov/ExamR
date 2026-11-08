import { Pencil, Plus, Trash2 } from 'lucide-react'
import { useState } from 'react'
import { Alert } from '../../components/common/Alert'
import { Button } from '../../components/common/Button'
import { EmptyState } from '../../components/common/EmptyState'
import { StatusBadge } from '../../components/common/StatusBadge'
import { useConfirm } from '../../lib/use-confirm'
import { cn } from '../../lib/utils'
import {
  useCreateDimTest, useDeleteDimTest, useDimAdminTests, useDimBankStats, useDimCatalog, useDimSourceDocuments, useUpdateDimTest,
  type DimAdminTest, type DimCatalog, type DimTestKind, type DimTrack,
} from './api'
import { inputClass, trackLabel } from './ui'

/** The three settings an admin edits; time window and publish state are kept as they are. */
interface SettingsForm { title: string; duration: string; limit: string }

const fromTest = (t: DimAdminTest): SettingsForm => ({ title: t.title, duration: String(t.durationMinutes), limit: t.attemptLimit ? String(t.attemptLimit) : '' })

const settingsValid = (f: SettingsForm) => f.title.trim().length >= 2 && Number(f.duration) >= 1

/** Subjects a full test must contain, with the official closed-question count as the default size. */
function sectionSubjects(catalog: DimCatalog, track: DimTrack, kind: DimTestKind, group: string, subject: string) {
  const specs = track === 'Buraxilis' ? catalog.buraxilis : catalog.blok
  const codes = kind === 'Subject' ? (subject ? [subject] : [])
    : track === 'Buraxilis' ? specs.map(s => s.code)
    : catalog.groups.find(g => g.code === group)?.subjects.map(s => s.code) ?? []
  return codes.map(code => specs.find(s => s.code === code)!).filter(Boolean)
}

export function DimTestsAdmin() {
  const tests = useDimAdminTests()
  const [creating, setCreating] = useState<'document' | 'full' | null>(null)

  return (
    <div className="flex flex-col gap-5">
      {creating === 'document' ? <CreateFromDocument onDone={() => setCreating(null)} onFull={() => setCreating('full')} />
        : creating === 'full' ? <CreateFullTest onDone={() => setCreating(null)} />
        : <div><Button onClick={() => setCreating('document')}><Plus className="size-4" aria-hidden />Yeni sınaq</Button></div>}
      {tests.isPending ? <div className="h-40 animate-pulse rounded-xl bg-slate-200/60" aria-label="Yüklənir" />
        : tests.isError ? <Alert title="Sınaqlar yüklənmədi">{tests.error.message}</Alert>
        : tests.data.length === 0 ? <EmptyState title="Hələ sınaq yoxdur" description="Sual bankına sənəd əlavə edin, sonra ondan sınaq yaradın." />
        : <ul className="flex flex-col gap-3">{tests.data.map(t => <li key={t.id}><TestRow test={t} /></li>)}</ul>}
    </div>
  )
}

function SettingsFields({ value, onChange, count }: {
  value: SettingsForm; onChange: (next: SettingsForm) => void; count?: { value: string; max: number; onChange: (value: string) => void }
}) {
  const set = (patch: Partial<SettingsForm>) => onChange({ ...value, ...patch })
  return (
    <div className="flex flex-col gap-3">
      <label className="flex flex-col gap-1.5 text-sm font-medium text-slate-700">Ad
        <input className={inputClass} value={value.title} maxLength={200} onChange={e => set({ title: e.target.value })} placeholder="məs. Riyaziyyat sınağı №1" />
      </label>
      <div className={cn('grid gap-3', count ? 'sm:grid-cols-3' : 'sm:grid-cols-2')}>
        <label className="flex flex-col gap-1.5 text-sm font-medium text-slate-700">Müddət (dəq)
          <input className={inputClass} type="number" min={1} max={600} value={value.duration} onChange={e => set({ duration: e.target.value })} />
        </label>
        {count && (
          <label className="flex flex-col gap-1.5 text-sm font-medium text-slate-700">Sual sayı (bankda {count.max})
            <input className={inputClass} type="number" min={1} max={count.max} value={count.value} onChange={e => count.onChange(e.target.value)} />
          </label>
        )}
        <label className="flex flex-col gap-1.5 text-sm font-medium text-slate-700">Cəhd limiti
          <input className={inputClass} type="number" min={1} max={100} value={value.limit} onChange={e => set({ limit: e.target.value })} placeholder="limitsiz" />
        </label>
      </div>
    </div>
  )
}

/** Main flow: pick a document already added to the bank; every attempt draws random questions from it. */
function CreateFromDocument({ onDone, onFull }: { onDone: () => void; onFull: () => void }) {
  const documents = useDimSourceDocuments()
  const catalog = useDimCatalog()
  const create = useCreateDimTest()
  const [source, setSource] = useState('')
  const [count, setCount] = useState('')
  const [settings, setSettings] = useState<SettingsForm>({ title: '', duration: '60', limit: '' })

  // One option per document and subject it was added to the bank under.
  const sources = (documents.data ?? []).flatMap(d => d.bank.map(b => ({ key: `${d.id}|${b.track}|${b.subject}`, document: d, ...b })))
  const picked = sources.find(s => s.key === source)

  const choose = (key: string) => {
    setSource(key)
    create.reset()
    const next = sources.find(s => s.key === key)
    if (!next) return
    const closed = [...(catalog.data?.buraxilis ?? []), ...(catalog.data?.blok ?? [])].find(s => s.code === next.subject)?.closed ?? next.count
    setCount(String(Math.min(closed, next.count)))
    setSettings(s => ({ ...s, title: next.document.originalFileName.replace(/\.(pdf|docx)$/i, '') }))
  }

  const submit = () => picked && create.mutate({
    track: picked.track, kind: 'Subject', groupCode: null,
    title: settings.title.trim(), durationMinutes: Number(settings.duration), attemptLimit: settings.limit ? Number(settings.limit) : null,
    availableFrom: null, availableTo: null, isPublished: false,
    sections: [{ subject: picked.subject, randomCount: Number(count), drawPerAttempt: true, sourceDocumentId: picked.document.id }],
  }, { onSuccess: onDone })

  return (
    <section className="rounded-xl border border-indigo-200 bg-white p-4 sm:p-5">
      <h2 className="font-semibold">Yeni sınaq</h2>
      <p className="mt-1 text-sm text-slate-600">Sənədi seçin — hər cəhddə suallar həmin sənədin bankdakı suallarından təsadüfi seçilir.</p>
      {documents.isPending ? <div className="mt-3 h-10 animate-pulse rounded-lg bg-slate-200/60" aria-label="Yüklənir" />
        : sources.length === 0 ? <p className="mt-3 text-sm text-amber-700">Bankda sənəd yoxdur. Əvvəlcə “Sual bankı” bölməsində PDF yükləyib sualları banka əlavə edin.</p>
        : (
          <label className="mt-3 flex flex-col gap-1.5 text-sm font-medium text-slate-700">Sənəd (sual bankı)
            <select className={inputClass} value={source} onChange={e => choose(e.target.value)}>
              <option value="">Sənəd seçin</option>
              {sources.map(s => <option key={s.key} value={s.key}>{s.document.originalFileName} — {trackLabel[s.track]} · {s.name} ({s.count} sual)</option>)}
            </select>
          </label>
        )}
      {picked && (
        <div className="mt-4">
          <SettingsFields value={settings} onChange={setSettings} count={{ value: count, max: picked.count, onChange: setCount }} />
        </div>
      )}
      {create.isError && <div className="mt-3"><Alert title="Sınaq yaradılmadı">{create.error.message}</Alert></div>}
      <div className="mt-4 flex flex-wrap items-center gap-2">
        <Button loading={create.isPending} disabled={!picked || !settingsValid(settings) || !(Number(count) >= 1 && Number(count) <= (picked?.count ?? 0))} onClick={submit}>Yarat</Button>
        <Button variant="ghost" onClick={onDone}>Ləğv et</Button>
        <button type="button" onClick={onFull} className="ml-auto text-sm font-medium text-indigo-700 hover:underline">Tam DİM sınağı (bütün bankdan)</button>
      </div>
      <p className="mt-2 text-xs text-slate-500">Sınaq qaralama kimi yaranır — siyahıda “Dərc et” düyməsi ilə istifadəçilərə açın.</p>
    </section>
  )
}

/** Secondary flow: an official-shape test (several subjects) drawn from the whole bank. */
function CreateFullTest({ onDone }: { onDone: () => void }) {
  const catalog = useDimCatalog()
  const stats = useDimBankStats()
  const create = useCreateDimTest()
  const [track, setTrack] = useState<DimTrack>('Buraxilis')
  const [group, setGroup] = useState('')
  const [settings, setSettings] = useState<SettingsForm>({ title: '', duration: '180', limit: '' })

  if (!catalog.data) return <div className="h-40 animate-pulse rounded-xl bg-slate-200/60" aria-label="Yüklənir" />
  const sections = sectionSubjects(catalog.data, track, 'Full', group, '')
  const inBank = (code: string) => stats.data?.find(s => s.track === track && s.subject === code)?.count ?? 0
  const short = sections.filter(s => s.closed > inBank(s.code))

  const submit = () => create.mutate({
    track, kind: 'Full', groupCode: track === 'Blok' ? group : null,
    title: settings.title.trim(), durationMinutes: Number(settings.duration), attemptLimit: settings.limit ? Number(settings.limit) : null,
    availableFrom: null, availableTo: null, isPublished: false,
    sections: sections.map(s => ({ subject: s.code, randomCount: s.closed, drawPerAttempt: true })),
  }, { onSuccess: onDone })

  return (
    <section className="rounded-xl border border-indigo-200 bg-white p-4 sm:p-5">
      <h2 className="font-semibold">Tam DİM sınağı</h2>
      <p className="mt-1 text-sm text-slate-600">Bütün bankdan, rəsmi qapalı sual sayı ilə; hər cəhddə yeni suallar.</p>
      <div className="mt-3 grid gap-3 sm:grid-cols-2">
        <label className="flex flex-col gap-1.5 text-sm font-medium text-slate-700">İmtahan
          <select className={inputClass} value={track} onChange={e => { setTrack(e.target.value as DimTrack); setGroup(''); create.reset() }}>
            <option value="Buraxilis">Buraxılış (XI sinif, 300 bal)</option>
            <option value="Blok">Blok (400 bal)</option>
          </select>
        </label>
        {track === 'Blok' && (
          <label className="flex flex-col gap-1.5 text-sm font-medium text-slate-700">İxtisas qrupu
            <select className={inputClass} value={group} onChange={e => setGroup(e.target.value)}>
              <option value="">Qrup seçin</option>
              {catalog.data.groups.map(g => <option key={g.code} value={g.code}>{g.name}</option>)}
            </select>
          </label>
        )}
      </div>
      {sections.length > 0 && (
        <p className={cn('mt-3 text-sm', short.length > 0 ? 'text-rose-700' : 'text-slate-600')}>
          {sections.map(s => `${s.name} ${s.closed} (bankda ${inBank(s.code)})`).join(' · ')}
          {short.length > 0 && ` — bankda kifayət qədər sual yoxdur: ${short.map(s => s.name).join(', ')}`}
        </p>
      )}
      <div className="mt-4"><SettingsFields value={settings} onChange={setSettings} /></div>
      {create.isError && <div className="mt-3"><Alert title="Sınaq yaradılmadı">{create.error.message}</Alert></div>}
      <div className="mt-4 flex gap-2">
        <Button loading={create.isPending} disabled={sections.length === 0 || !settingsValid(settings)} onClick={submit}>Yarat</Button>
        <Button variant="ghost" onClick={onDone}>Ləğv et</Button>
      </div>
    </section>
  )
}

function TestRow({ test }: { test: DimAdminTest }) {
  const update = useUpdateDimTest()
  const remove = useDeleteDimTest()
  const confirm = useConfirm()
  const catalog = useDimCatalog()
  const documents = useDimSourceDocuments()
  const [editing, setEditing] = useState<SettingsForm | null>(null)
  const groupName = catalog.data?.groups.find(g => g.code === test.groupCode)?.name
  const documentName = (id?: string | null) => (id ? documents.data?.find(d => d.id === id)?.originalFileName ?? 'silinmiş sənəd' : null)

  const save = (patch: Partial<{ form: SettingsForm; publish: boolean }>) => {
    const form = patch.form ?? fromTest(test)
    update.mutate({
      id: test.id,
      settings: {
        title: form.title.trim(), durationMinutes: Number(form.duration), attemptLimit: form.limit ? Number(form.limit) : null,
        availableFrom: test.availableFrom, availableTo: test.availableTo, isPublished: patch.publish ?? test.isPublished,
      },
    }, { onSuccess: () => setEditing(null) })
  }
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
          <p className="mt-1 text-sm text-slate-600 break-words">
            {test.sections.map(s => `${s.name} ${s.questionCount} sual${s.random ? ' (təsadüfi)' : ''}${documentName(s.sourceDocumentId) ? ` · ${documentName(s.sourceDocumentId)}` : ''}`).join(' · ')}
          </p>
          <p className="mt-0.5 text-sm text-slate-500">
            {test.durationMinutes} dəq · {test.attemptLimit ? `${test.attemptLimit} cəhd` : 'limitsiz cəhd'} · {test.attempts} cəhd edilib
          </p>
        </div>
        <StatusBadge tone={test.isPublished ? 'success' : 'neutral'}>{test.isPublished ? 'Dərc edilib' : 'Qaralama'}</StatusBadge>
      </div>

      {editing ? (
        <div className="mt-4 border-t border-slate-100 pt-4">
          <SettingsFields value={editing} onChange={setEditing} />
          <div className="mt-3 flex gap-2">
            <Button loading={update.isPending} disabled={!settingsValid(editing)} onClick={() => save({ form: editing })}>Yadda saxla</Button>
            <Button variant="ghost" onClick={() => setEditing(null)}>Ləğv et</Button>
          </div>
        </div>
      ) : (
        <div className="mt-3 flex flex-wrap gap-2">
          <Button variant="secondary" loading={update.isPending} onClick={() => save({ publish: !test.isPublished })}>
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
