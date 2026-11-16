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
import { useTranslation } from 'react-i18next'

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
  const { t } = useTranslation()
  const tests = useDimAdminTests()
  const [creating, setCreating] = useState<'document' | 'full' | null>(null)

  return (
    <div className="flex flex-col gap-5">
      {creating === 'document' ? <CreateFromDocument onDone={() => setCreating(null)} onFull={() => setCreating('full')} />
        : creating === 'full' ? <CreateFullTest onDone={() => setCreating(null)} />
        : <div><Button onClick={() => setCreating('document')}><Plus className="size-4" aria-hidden />{t('dim.admin.newTest')}</Button></div>}
      {tests.isPending ? <div className="h-40 animate-pulse rounded-xl bg-slate-200/60" aria-label={t('common.loading')} />
        : tests.isError ? <Alert title={t('dim.page.loadFailed')}>{tests.error.message}</Alert>
        : tests.data.length === 0 ? <EmptyState title={t('dim.page.empty')} description={t('dim.admin.emptyText')} />
        : <ul className="flex flex-col gap-3">{tests.data.map(test => <li key={test.id}><TestRow test={test} /></li>)}</ul>}
    </div>
  )
}

function SettingsFields({ value, onChange, count }: {
  value: SettingsForm; onChange: (next: SettingsForm) => void; count?: { value: string; max: number; onChange: (value: string) => void }
}) {
  const { t } = useTranslation()
  const set = (patch: Partial<SettingsForm>) => onChange({ ...value, ...patch })
  return (
    <div className="flex flex-col gap-3">
      <label className="flex flex-col gap-1.5 text-sm font-medium text-slate-700">{t('dim.admin.name')}
        <input className={inputClass} value={value.title} maxLength={200} onChange={e => set({ title: e.target.value })} placeholder={t('dim.admin.namePlaceholder')} />
      </label>
      <div className={cn('grid gap-3', count ? 'sm:grid-cols-3' : 'sm:grid-cols-2')}>
        <label className="flex flex-col gap-1.5 text-sm font-medium text-slate-700">{t('dim.admin.duration')}
          <input className={inputClass} type="number" min={1} max={600} value={value.duration} onChange={e => set({ duration: e.target.value })} />
        </label>
        {count && (
          <label className="flex flex-col gap-1.5 text-sm font-medium text-slate-700">{t('dim.admin.countInBank', { n: count.max })}
            <input className={inputClass} type="number" min={1} max={count.max} value={count.value} onChange={e => count.onChange(e.target.value)} />
          </label>
        )}
        <label className="flex flex-col gap-1.5 text-sm font-medium text-slate-700">{t('dim.admin.attemptLimit')}
          <input className={inputClass} type="number" min={1} max={100} value={value.limit} onChange={e => set({ limit: e.target.value })} placeholder={t('dim.admin.unlimited')} />
        </label>
      </div>
    </div>
  )
}

/** Main flow: pick a document already added to the bank; every attempt draws random questions from it. */
function CreateFromDocument({ onDone, onFull }: { onDone: () => void; onFull: () => void }) {
  const { t } = useTranslation()
  const documents = useDimSourceDocuments()
  const catalog = useDimCatalog()
  const create = useCreateDimTest()
  const [source, setSource] = useState('')
  const [count, setCount] = useState('')
  const [settings, setSettings] = useState<SettingsForm>({ title: '', duration: '60', limit: '' })

  // One option per document and subject it was added to the bank under.
  const sources = (documents.data ?? []).flatMap(d => d.bank.filter(b => b.track === 'Buraxilis')
    .map(b => ({ key: `${d.id}|${b.track}|${b.subject}`, document: d, ...b })))
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
      <h2 className="font-semibold">{t('dim.admin.newTest')}</h2>
      <p className="mt-1 text-sm text-slate-600">{t('dim.admin.newTestText')}</p>
      {documents.isPending ? <div className="mt-3 h-10 animate-pulse rounded-lg bg-slate-200/60" aria-label={t('common.loading')} />
        : sources.length === 0 ? <p className="mt-3 text-sm text-amber-700">{t('dim.admin.noBankDocs')}</p>
        : (
          <label className="mt-3 flex flex-col gap-1.5 text-sm font-medium text-slate-700">{t('dim.admin.bankDocument')}
            <select className={inputClass} value={source} onChange={e => choose(e.target.value)}>
              <option value="">{t('questions.pickDocument')}</option>
              {sources.map(s => <option key={s.key} value={s.key}>{s.document.originalFileName} — {trackLabel[s.track]} · {s.name} ({t('common.questions', { count: s.count })})</option>)}
            </select>
          </label>
        )}
      {picked && (
        <div className="mt-4">
          <SettingsFields value={settings} onChange={setSettings} count={{ value: count, max: picked.count, onChange: setCount }} />
        </div>
      )}
      {create.isError && <div className="mt-3"><Alert title={t('dim.admin.createFailed')}>{create.error.message}</Alert></div>}
      <div className="mt-4 flex flex-wrap items-center gap-2">
        <Button loading={create.isPending} disabled={!picked || !settingsValid(settings) || !(Number(count) >= 1 && Number(count) <= (picked?.count ?? 0))} onClick={submit}>{t('dim.set.create')}</Button>
        <Button variant="ghost" onClick={onDone}>{t('common.cancel')}</Button>
        <button type="button" onClick={onFull} className="ml-auto text-sm font-medium text-indigo-700 hover:underline">{t('dim.admin.fullFromBank')}</button>
      </div>
      <p className="mt-2 text-xs text-slate-500">{t('dim.admin.draftHint')}</p>
    </section>
  )
}

/** Secondary flow: an official-shape test (several subjects) drawn from the whole bank. */
function CreateFullTest({ onDone }: { onDone: () => void }) {
  const { t } = useTranslation()
  const catalog = useDimCatalog()
  const stats = useDimBankStats()
  const create = useCreateDimTest()
  const track: DimTrack = 'Buraxilis'
  const [settings, setSettings] = useState<SettingsForm>({ title: '', duration: '180', limit: '' })

  if (!catalog.data) return <div className="h-40 animate-pulse rounded-xl bg-slate-200/60" aria-label={t('common.loading')} />
  const sections = sectionSubjects(catalog.data, track, 'Full', '', '')
  const inBank = (code: string) => stats.data?.find(s => s.track === track && s.subject === code)?.count ?? 0
  const short = sections.filter(s => s.closed > inBank(s.code))

  const submit = () => create.mutate({
    track, kind: 'Full', groupCode: null,
    title: settings.title.trim(), durationMinutes: Number(settings.duration), attemptLimit: settings.limit ? Number(settings.limit) : null,
    availableFrom: null, availableTo: null, isPublished: false,
    sections: sections.map(s => ({ subject: s.code, randomCount: s.closed, drawPerAttempt: true })),
  }, { onSuccess: onDone })

  return (
    <section className="rounded-xl border border-indigo-200 bg-white p-4 sm:p-5">
      <h2 className="font-semibold">{t('dim.admin.fullTitle')}</h2>
      <p className="mt-1 text-sm text-slate-600">{t('dim.admin.fullText')}</p>
      {sections.length > 0 && (
        <p className={cn('mt-3 text-sm', short.length > 0 ? 'text-rose-700' : 'text-slate-600')}>
          {sections.map(s => `${s.name} ${s.closed} (${t('dim.admin.inBank', { n: inBank(s.code) })})`).join(' · ')}
          {short.length > 0 && ` — ${t('dim.admin.notEnough', { names: short.map(s => s.name).join(', ') })}`}
        </p>
      )}
      <div className="mt-4"><SettingsFields value={settings} onChange={setSettings} /></div>
      {create.isError && <div className="mt-3"><Alert title={t('dim.admin.createFailed')}>{create.error.message}</Alert></div>}
      <div className="mt-4 flex gap-2">
        <Button loading={create.isPending} disabled={sections.length === 0 || !settingsValid(settings)} onClick={submit}>{t('dim.set.create')}</Button>
        <Button variant="ghost" onClick={onDone}>{t('common.cancel')}</Button>
      </div>
    </section>
  )
}

function TestRow({ test }: { test: DimAdminTest }) {
  const { t } = useTranslation()
  const update = useUpdateDimTest()
  const remove = useDeleteDimTest()
  const confirm = useConfirm()
  const catalog = useDimCatalog()
  const documents = useDimSourceDocuments()
  const [editing, setEditing] = useState<SettingsForm | null>(null)
  const groupName = catalog.data?.groups.find(g => g.code === test.groupCode)?.name
  const documentName = (id?: string | null) => (id ? documents.data?.find(d => d.id === id)?.originalFileName ?? t('dim.admin.deletedDoc') : null)

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
    if (await confirm({ title: t('dim.admin.deleteTitle'), description: test.attempts > 0
      ? t('dim.admin.deleteWithAttempts', { title: test.title, n: test.attempts })
      : t('dim.admin.deleteText', { title: test.title }), confirmLabel: t('common.delete'), danger: true }))
      remove.mutate(test.id)
  }

  return (
    <article className="rounded-xl border border-slate-200 bg-white p-4">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div className="min-w-0">
          <p className="text-xs font-medium tracking-wide text-indigo-700 uppercase">
            {trackLabel[test.track]} · {test.kind === 'Full' ? t('dim.kind.Full') : t('dim.kind.Subject')}{groupName && ` · ${groupName}`}
          </p>
          <h3 className="mt-0.5 font-semibold">{test.title}</h3>
          <p className="mt-1 text-sm text-slate-600 break-words">
            {test.sections.map(s => `${s.name} ${t('common.questions', { count: s.questionCount })}${s.random ? ` (${t('dim.admin.random')})` : ''}${documentName(s.sourceDocumentId) ? ` · ${documentName(s.sourceDocumentId)}` : ''}`).join(' · ')}
          </p>
          <p className="mt-0.5 text-sm text-slate-500">
            {t('dim.page.minutesShort', { n: test.durationMinutes })} · {test.attemptLimit ? t('dim.admin.attemptsN', { n: test.attemptLimit }) : t('dim.admin.attemptsUnlimited')} · {t('dim.admin.attemptsMade', { n: test.attempts })}
          </p>
        </div>
        <StatusBadge tone={test.isPublished ? 'success' : 'neutral'}>{test.isPublished ? t('dim.admin.published') : t('dim.admin.draft')}</StatusBadge>
      </div>

      {editing ? (
        <div className="mt-4 border-t border-slate-100 pt-4">
          <SettingsFields value={editing} onChange={setEditing} />
          <div className="mt-3 flex gap-2">
            <Button loading={update.isPending} disabled={!settingsValid(editing)} onClick={() => save({ form: editing })}>{t('questions.saveButton')}</Button>
            <Button variant="ghost" onClick={() => setEditing(null)}>{t('common.cancel')}</Button>
          </div>
        </div>
      ) : (
        <div className="mt-3 flex flex-wrap gap-2">
          <Button variant="secondary" loading={update.isPending} onClick={() => save({ publish: !test.isPublished })}>
            {test.isPublished ? t('dim.admin.unpublish') : t('dim.admin.publish')}
          </Button>
          <Button variant="ghost" onClick={() => setEditing(fromTest(test))}><Pencil className="size-4" aria-hidden />{t('questions.edit')}</Button>
          <Button variant="ghost" loading={remove.isPending} onClick={() => void destroy()}><Trash2 className="size-4" aria-hidden />{t('common.delete')}</Button>
        </div>
      )}
      {(update.isError || remove.isError) && <div className="mt-3"><Alert title={t('profile.actionFailed')}>{(update.error ?? remove.error)?.message}</Alert></div>}
    </article>
  )
}
