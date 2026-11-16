import { BookOpen, CheckCircle2, FileText, Headphones, KeyRound, Pencil, Plus, Trash2 } from 'lucide-react'
import { useState } from 'react'
import { Alert } from '../../components/common/Alert'
import { Button } from '../../components/common/Button'
import { EmptyState } from '../../components/common/EmptyState'
import { FileDropzone } from '../../components/common/FileDropzone'
import { StatusBadge } from '../../components/common/StatusBadge'
import { api, ApiError } from '../../lib/api-client'
import { useConfirm } from '../../lib/use-confirm'
import { cn } from '../../lib/utils'
import { MAX_FILE_SIZE_BYTES, useUploadDocument, type DocumentDto } from '../documents/api'
import {
  useApplyAnswerKey, useApproveValid, useDeleteDimDocument, useDeleteDimPassage, useDeleteDimQuestion, useDimAdminTests, useDimAudios,
  useDimBankStats, useDimCatalog, useDimPassages, useDimSourceDocuments, useDimSourceQuestions, useEditDimQuestion, useRemoveFromBank,
  useSaveDimPassage, type DimPassage, type DimSourceDocument, type DimSourceQuestion, type DimTrack,
} from './api'
import { DimAudioPanel, DimTopicRanges } from './DimDocumentTools'
import { DimManualQuestion } from './DimManualQuestion'
import { DimTextImport } from './DimTextImport'
import { inputClass, trackLabel } from './ui'
import { Trans, useTranslation } from 'react-i18next'
import i18n from '../../lib/i18n'

interface Classification { track: DimTrack; subject: string }

/** Mirrors the server rule (DimAdminEndpoints.BankRejection); null = the question fits DİM tests. */
function problem(q: DimSourceQuestion): string | null {
  if (!q.text.trim()) return i18n.t('dim.bank.problem.empty')
  if (q.options.length === 0) return q.codedAnswer ? null : i18n.t('dim.bank.problem.openNoAnswer')
  if (q.options.length < 2) return i18n.t('dim.bank.problem.fewOptions')
  if (q.options.length > 5) return i18n.t('dim.bank.problem.manyOptions')
  if (q.options.filter(o => o.isCorrect).length !== 1) return i18n.t('dim.bank.problem.noCorrect')
  if (!q.text.trim() || q.options.some(o => !o.text.trim())) return i18n.t('dim.bank.problem.empty')
  return null
}

/**
 * Only answer and text problems can be fixed here; the option count comes from the PDF. A question without options
 * becomes an open coded question once its answer is typed in.
 */
const fixable = (q: DimSourceQuestion) => q.options.length === 0 || (q.options.length >= 2 && q.options.length <= 5)

export function DimBankAdmin() {
  const { t } = useTranslation()
  const documents = useDimSourceDocuments()
  const stats = useDimBankStats()
  const [documentId, setDocumentId] = useState('')
  const [upload, setUpload] = useState<Classification>({ track: 'Buraxilis', subject: '' })
  // Subject chosen at upload time, remembered per document for the one-click "add to bank".
  const [chosen, setChosen] = useState<Record<string, Classification>>({})
  const [source, setSource] = useState<'upload' | 'manual' | 'import'>('upload')
  const selected = documents.data?.find(d => d.id === documentId)
  const tests = useDimAdminTests()
  const removeDocument = useDeleteDimDocument()
  const confirm = useConfirm()

  async function handleDelete(d: DimSourceDocument) {
    const banked = d.bank.reduce((sum, b) => sum + b.count, 0)
    const usedBy = (tests.data ?? []).filter(t => t.sections.some(s => s.sourceDocumentId === d.id)).map(t => `"${t.title}"`)
    const description = t('dim.bank.deleteText', { name: d.originalFileName, n: d.questionCount })
      + (banked > 0 ? t('dim.bank.deleteBanked', { n: banked }) : '')
      + t('dim.bank.deleteTests')
      + (usedBy.length > 0 ? t('dim.bank.deleteUsedBy', { list: usedBy.join(', ') }) : '')
      + t('dim.bank.deleteEnd')
    if (await confirm({ title: t('documents.deleteTitle'), description, confirmLabel: t('common.delete'), danger: true }))
      removeDocument.mutate(d.id, { onSuccess: () => setDocumentId(current => (current === d.id ? '' : current)) })
  }

  return (
    <div className="flex flex-col gap-5">
      <section className="rounded-xl border border-slate-200 bg-white p-4 sm:p-5">
        <div role="tablist" aria-label={t('dim.bank.sourceAria')} className="inline-flex rounded-lg border border-slate-200 bg-white p-1">
          {([['upload', t('dim.bank.source.upload')], ['import', t('dim.bank.source.import')], ['manual', t('dim.bank.source.manual')]] as const).map(([value, label]) => (
            <button key={value} type="button" role="tab" aria-selected={source === value} onClick={() => setSource(value)}
              className={cn('rounded-md px-3 py-1.5 text-sm font-medium whitespace-nowrap', source === value ? 'bg-indigo-600 text-white' : 'text-slate-600 hover:bg-slate-100')}>
              {label}
            </button>
          ))}
        </div>
        {source === 'upload' ? (
          <>
            <p className="mt-3 text-sm text-slate-600">{t('dim.bank.uploadText')}</p>
            <SubjectFields value={upload} onChange={setUpload} />
            <DimUpload disabled={!upload.subject} onReady={id => {
              setChosen(c => ({ ...c, [id]: upload }))
              setDocumentId(id)
              void documents.refetch()
            }} />
          </>
        ) : source === 'import' ? (
          <>
            <p className="mt-3 text-sm text-slate-600">
              {t('dim.bank.importText')}
            </p>
            <DimTextImport documents={documents.data ?? []} documentId={documentId} onDocument={setDocumentId}
              onImported={(id, subject) => setChosen(c => ({ ...c, [id]: { track: 'Buraxilis', subject } }))} />
          </>
        ) : (
          <>
            <p className="mt-3 text-sm text-slate-600">
              {t('dim.bank.manualText')}
            </p>
            <DimManualQuestion documents={documents.data ?? []} documentId={documentId} onDocument={setDocumentId} />
          </>
        )}
      </section>

      <section className="rounded-xl border border-slate-200 bg-white p-4 sm:p-5">
        <div className="flex flex-wrap items-baseline justify-between gap-2">
          <h2 className="font-semibold">{t('dim.bank.myDocuments')}</h2>
          {stats.data && stats.data.length > 0 && (
            <p className="text-sm text-slate-500">{t('dim.bank.totalInBank')} {stats.data.map(s => `${trackLabel[s.track]} ${s.name} ${s.count}`).join(' · ')}</p>
          )}
        </div>
        {documents.isPending ? <div className="mt-3 h-20 animate-pulse rounded-lg bg-slate-200/60" aria-label={t('common.loading')} />
          : documents.isError ? <div className="mt-3"><Alert title={t('documents.listFailed')}>{documents.error.message}</Alert></div>
          : documents.data.length === 0 ? <p className="mt-2 text-sm text-slate-500">{t('dim.bank.noDocs')}</p>
          : (
            <ul className="mt-3 grid gap-2 sm:grid-cols-2">
              {documents.data.map(d => (
                <li key={d.id} className="relative">
                  <button type="button" onClick={() => setDocumentId(d.id)} aria-pressed={d.id === documentId}
                    className={cn('w-full rounded-lg border p-3 pr-12 text-left transition-colors', d.id === documentId ? 'border-indigo-400 bg-indigo-50/60 ring-1 ring-indigo-400' : 'border-slate-200 hover:bg-slate-50')}>
                    <span className="flex items-center gap-2 font-medium break-all"><FileText className="size-4 shrink-0 text-indigo-600" aria-hidden />{d.originalFileName}</span>
                    <DocumentCounts document={d} />
                  </button>
                  <button type="button" onClick={() => void handleDelete(d)} disabled={removeDocument.isPending}
                    aria-label={t('documents.deleteAria', { name: d.originalFileName })} title={t('documents.delete')}
                    className="absolute top-2 right-2 inline-flex size-9 items-center justify-center rounded-md text-slate-500 hover:bg-rose-50 hover:text-rose-700 disabled:opacity-50">
                    <Trash2 className="size-4" aria-hidden />
                  </button>
                </li>
              ))}
            </ul>
          )}
        {removeDocument.isError && <div className="mt-3"><Alert title={t('dim.bank.deleteFailed')}>{removeDocument.error.message}</Alert></div>}
      </section>

      {/* Re-mounted when a subject is chosen for the document (upload or import), so "add to bank" starts with it. */}
      {selected && <DocumentPanel key={`${selected.id}:${chosen[selected.id]?.subject ?? ''}`} document={selected} initial={chosen[selected.id]} />}
    </div>
  )
}

function DocumentCounts({ document: d }: { document: DimSourceDocument }) {
  const { t } = useTranslation()
  const banked = d.bank.reduce((sum, b) => sum + b.count, 0)
  return (
    <span className="mt-1 block text-sm text-slate-600 tabular-nums">
      <span className="text-emerald-700">{d.valid} {t('dashboard.correctWord')}</span>
      {d.questionCount - d.valid > 0 && <> · <span className="text-amber-700">{d.questionCount - d.valid} {t('dashboard.wrongWord')}</span></>}
      {' · '}{banked > 0 ? `${t('dim.admin.inBank', { n: banked })} (${d.bank.map(b => `${trackLabel[b.track]} ${b.name}`).join(', ')})` : t('dim.bank.notInBank')}
    </span>
  )
}

/** Only Buraxılış is offered for now; Blok is paused (the API still supports it). */
function SubjectFields({ value, onChange }: { value: Classification; onChange: (next: Classification) => void }) {
  const { t } = useTranslation()
  const catalog = useDimCatalog()
  const subjects = catalog.data?.buraxilis ?? []
  return (
    <div className="mt-3 grid gap-3 sm:max-w-sm">
      <label className="flex flex-col gap-1.5 text-sm font-medium text-slate-700">{t('dim.report.subject')}
        <select className={inputClass} value={value.subject} onChange={e => onChange({ ...value, subject: e.target.value })}>
          <option value="">{t('dim.form.pickSubject')}</option>
          {subjects.map(s => <option key={s.code} value={s.code}>{s.name}</option>)}
        </select>
      </label>
    </div>
  )
}

function DocumentPanel({ document, initial }: { document: DimSourceDocument; initial?: Classification }) {
  const { t } = useTranslation()
  const questions = useDimSourceQuestions(document.id)
  const audios = useDimAudios(document.id)
  const approve = useApproveValid()
  const [target, setTarget] = useState<Classification>(initial ?? (document.bank[0] ? { track: document.bank[0].track, subject: document.bank[0].subject } : { track: 'Buraxilis', subject: '' }))
  const [topic, setTopic] = useState('')
  const [view, setView] = useState<'wrong' | 'all'>('wrong')

  if (questions.isPending) return <div className="h-64 animate-pulse rounded-xl bg-slate-200/60" aria-label={t('common.loading')} />
  if (questions.isError) return <Alert title={t('documents.questionsFailed')}>{questions.error.message}</Alert>

  const all = questions.data
  const wrong = all.filter(q => problem(q) !== null)
  const valid = all.length - wrong.length
  // Closed questions without a single correct option, and open ones without a coded answer: the answer key fixes both.
  const missingAnswers = wrong.filter(q => (q.options.length >= 2 && q.options.length <= 5 && q.options.filter(o => o.isCorrect).length !== 1)
    || (q.options.length === 0 && !q.codedAnswer)).length
  const unbanked = all.filter(q => problem(q) === null && !q.bank).length
  const audioFor = (n: number) => audios.data?.find(a => n >= a.questionFrom && n <= a.questionTo)?.title ?? null
  const visible = view === 'wrong' ? wrong : all

  return (
    <>
      <section className="rounded-xl border border-indigo-200 bg-white p-4 sm:p-5">
        <h2 className="font-semibold break-all">{document.originalFileName}</h2>
        <p className="mt-1 text-sm text-slate-600">
          {t('dim.bank.found', { n: all.length })} <span className="font-medium text-emerald-700">{t('dim.bank.validFormat', { n: valid })}</span>
          {wrong.length > 0 && <>, <span className="font-medium text-amber-700">{wrong.length} {t('dashboard.wrongWord')}</span> {t('dim.bank.fixBelow')}</>}.
        </p>
        {document.isManual && unbanked === 0 ? (
          <p className="mt-2 text-sm text-slate-500">{t('dim.bank.manualSet')}</p>
        ) : (
          <>
        <SubjectFields value={target} onChange={setTarget} />
        <details className="mt-3 text-sm">
          <summary className="cursor-pointer font-medium text-slate-600">{t('dim.bank.addTopic')}</summary>
          <label className="mt-2 flex flex-col gap-1.5 text-slate-700">{t('dim.bank.topicLabel')}
            <input className={inputClass} value={topic} maxLength={200} onChange={e => setTopic(e.target.value)} placeholder={t('dim.form.topicPlaceholder')} />
          </label>
        </details>
        <div className="mt-4 flex flex-wrap items-center gap-3">
          <Button loading={approve.isPending} disabled={!target.subject || valid === 0}
            onClick={() => approve.mutate({ documentId: document.id, ...target, topic: topic.trim() || null })}>
            <CheckCircle2 className="size-4" aria-hidden />{t('dim.bank.approve', { n: valid })}
          </Button>
          {approve.data && (
            <p className="text-sm text-slate-600" role="status">
              {t('dim.bank.approved', { n: approve.data.approved })}{approve.data.rejected.length > 0 && ` ${t('dim.bank.rejected', { n: approve.data.rejected.length })}`}
            </p>
          )}
        </div>
        {approve.isError && <div className="mt-2"><Alert title={t('dim.bank.approveFailed')}>{approve.error.message}</Alert></div>}
          </>
        )}
        {missingAnswers > 0 && <AnswerKey documentId={document.id} missing={missingAnswers} />}
        <Passages documentId={document.id} />
        <DimTopicRanges documentId={document.id} />
        <DimAudioPanel documentId={document.id} />
      </section>

      <section aria-label={t('dim.bank.questionsAria')} className="flex flex-col gap-3">
        <div role="tablist" aria-label={t('exam.session.questions')} className="inline-flex self-start rounded-lg border border-slate-200 bg-white p-1">
          {([['wrong', t('dim.bank.wrongTab', { n: wrong.length })], ['all', t('dim.bank.allTab', { n: all.length })]] as const).map(([value, label]) => (
            <button key={value} type="button" role="tab" aria-selected={view === value} onClick={() => setView(value)}
              className={cn('rounded-md px-3 py-1.5 text-sm font-medium whitespace-nowrap', view === value ? 'bg-indigo-600 text-white' : 'text-slate-600 hover:bg-slate-100')}>
              {label}
            </button>
          ))}
        </div>
        {visible.length === 0 ? (
          <EmptyState title={view === 'wrong' ? t('dim.bank.noWrong') : t('dim.bank.noQuestions')} description={view === 'wrong' ? t('dim.bank.allValid') : t('dim.bank.noneInDoc')} />
        ) : (
          <ul className="flex flex-col gap-2">{visible.map(q => <li key={q.id}><QuestionItem question={q} manual={document.isManual} audio={audioFor(q.sourceNumber)} /></li>)}</ul>
        )}
      </section>
    </>
  )
}

function AnswerKey({ documentId, missing }: { documentId: string; missing: number }) {
  const { t } = useTranslation()
  const applyKey = useApplyAnswerKey()
  const [keyText, setKeyText] = useState('')
  return (
    <details className="mt-4 border-t border-slate-100 pt-3 text-sm">
      <summary className="flex cursor-pointer items-center gap-2 font-medium text-slate-700">
        <KeyRound className="size-4 text-indigo-600" aria-hidden />{t('dim.key.title')}
        <span className="font-normal text-amber-700">— {t('dim.key.missing', { n: missing })}</span>
      </summary>
      <p className="mt-2 text-slate-600">
        <Trans i18nKey="dim.key.hint" components={{ c: <code /> }} />
      </p>
      <textarea value={keyText} onChange={e => setKeyText(e.target.value)} rows={3} aria-label={t('dim.key.aria')}
        className="mt-2 w-full rounded-lg border border-slate-300 bg-white p-3 font-mono text-sm focus:border-indigo-500 focus:ring-2 focus:ring-indigo-500/20 focus:outline-none" />
      <div className="mt-2 flex flex-wrap items-center gap-3">
        <Button variant="secondary" loading={applyKey.isPending} disabled={keyText.trim().length < 2}
          onClick={() => applyKey.mutate({ documentId, text: keyText })}>{t('dim.key.apply')}</Button>
        {applyKey.data && (
          <p className="text-slate-600" role="status">
            {t('dim.key.applied', { n: applyKey.data.applied })}
            {applyKey.data.notFound.length > 0 && ` ${t('dim.key.notFound', { list: applyKey.data.notFound.join(', ') })}`}
            {applyKey.data.invalid.length > 0 && ` ${t('dim.key.invalid', { list: applyKey.data.invalid.join(', ') })}`}
          </p>
        )}
      </div>
      {applyKey.isError && <div className="mt-2"><Alert title={t('dim.key.failed')}>{applyKey.error.message}</Alert></div>}
    </details>
  )
}

/** Reading passages / listening transcripts: shown in the exam above the questions of their number range. */
function Passages({ documentId }: { documentId: string }) {
  const { t } = useTranslation()
  const passages = useDimPassages(documentId)
  const remove = useDeleteDimPassage()
  const [editing, setEditing] = useState<DimPassage | 'new' | null>(null)
  const list = passages.data ?? []

  return (
    <details className="mt-4 border-t border-slate-100 pt-3 text-sm">
      <summary className="flex cursor-pointer items-center gap-2 font-medium text-slate-700">
        <BookOpen className="size-4 text-indigo-600" aria-hidden />{t('dim.passages.title')}
        <span className="font-normal text-slate-500">— {passages.isPending ? '…' : list.length}</span>
      </summary>
      <p className="mt-2 text-slate-600">
        {t('dim.passages.text')}
      </p>
      {passages.isError && <div className="mt-2"><Alert title={t('dim.passages.loadFailed')}>{passages.error.message}</Alert></div>}
      {remove.isError && <div className="mt-2"><Alert title={t('dim.passages.deleteFailed')}>{remove.error.message}</Alert></div>}
      <ul className="mt-3 flex flex-col gap-2">
        {list.map(p => (
          <li key={p.id} className="rounded-lg border border-slate-200 p-3">
            {editing !== null && editing !== 'new' && editing.id === p.id ? (
              <PassageEditor documentId={documentId} passage={p} onDone={() => setEditing(null)} />
            ) : (
              <>
                <div className="flex flex-wrap items-center gap-2">
                  <StatusBadge tone="info">{t('history.questionsRange', { range: `${p.fromNumber}–${p.toNumber}` })}</StatusBadge>
                  {p.title && <span className="min-w-0 truncate text-slate-600">{p.title}</span>}
                  <span className="ml-auto flex gap-1">
                    <button type="button" onClick={() => setEditing(p)} className="inline-flex items-center gap-1 rounded-md px-2 py-1 text-xs font-medium text-indigo-700 hover:bg-indigo-50">
                      <Pencil className="size-3.5" aria-hidden />{t('questions.edit')}
                    </button>
                    <button type="button" onClick={() => remove.mutate(p.id)} disabled={remove.isPending}
                      className="inline-flex items-center gap-1 rounded-md px-2 py-1 text-xs font-medium text-rose-700 hover:bg-rose-50">
                      <Trash2 className="size-3.5" aria-hidden />{t('common.delete')}
                    </button>
                  </span>
                </div>
                <p className="mt-1.5 line-clamp-3 whitespace-pre-line text-slate-800">{p.text}</p>
              </>
            )}
          </li>
        ))}
      </ul>
      {editing === 'new' ? (
        <div className="mt-2 rounded-lg border border-indigo-200 p-3"><PassageEditor documentId={documentId} onDone={() => setEditing(null)} /></div>
      ) : (
        <Button variant="secondary" className="mt-2" onClick={() => setEditing('new')}><Plus className="size-4" aria-hidden />{t('dim.passages.add')}</Button>
      )}
    </details>
  )
}

function PassageEditor({ documentId, passage, onDone }: { documentId: string; passage?: DimPassage; onDone: () => void }) {
  const { t } = useTranslation()
  const save = useSaveDimPassage()
  const [from, setFrom] = useState(passage ? String(passage.fromNumber) : '')
  const [to, setTo] = useState(passage ? String(passage.toNumber) : '')
  const [title, setTitle] = useState(passage?.title ?? '')
  const [text, setText] = useState(passage?.text ?? '')
  const fromNumber = Number(from)
  const toNumber = Number(to)
  const ready = Number.isInteger(fromNumber) && fromNumber >= 1 && Number.isInteger(toNumber) && toNumber >= fromNumber && text.trim() !== ''

  return (
    <div className="flex flex-col gap-3">
      <div className="flex flex-wrap gap-3">
        <label className="flex w-28 flex-col gap-1.5 font-medium text-slate-700">{t('dim.passages.firstQuestion')}
          <input className={inputClass} type="number" min={1} inputMode="numeric" value={from} onChange={e => setFrom(e.target.value)} />
        </label>
        <label className="flex w-28 flex-col gap-1.5 font-medium text-slate-700">{t('exam.setup.toQuestion')}
          <input className={inputClass} type="number" min={1} inputMode="numeric" value={to} onChange={e => setTo(e.target.value)} />
        </label>
        <label className="flex min-w-48 flex-1 flex-col gap-1.5 font-medium text-slate-700">{t('dim.passages.heading')}
          <input className={inputClass} value={title} maxLength={300} onChange={e => setTitle(e.target.value)} placeholder={t('dim.passages.headingPlaceholder')} />
        </label>
      </div>
      <label className="flex flex-col gap-1.5 font-medium text-slate-700">{t('dim.passages.textLabel')}
        <textarea value={text} onChange={e => setText(e.target.value)} rows={8} maxLength={20000}
          className="w-full rounded-lg border border-slate-300 bg-white p-3 text-sm leading-6 focus:border-indigo-500 focus:ring-2 focus:ring-indigo-500/20 focus:outline-none" />
      </label>
      {save.isError && <Alert title={t('questions.notSaved')}>{save.error.message}</Alert>}
      <div className="flex gap-2">
        <Button loading={save.isPending} disabled={!ready}
          onClick={() => save.mutate({ documentId, id: passage?.id, fromNumber, toNumber, title: title.trim() || null, text }, { onSuccess: onDone })}>
          {t('questions.saveButton')}
        </Button>
        <Button variant="ghost" onClick={onDone}>{t('common.cancel')}</Button>
      </div>
    </div>
  )
}

function QuestionItem({ question: q, manual, audio }: { question: DimSourceQuestion; manual: boolean; audio: string | null }) {
  const { t } = useTranslation()
  const catalog = useDimCatalog()
  const remove = useRemoveFromBank()
  const deleteQuestion = useDeleteDimQuestion()
  const [editing, setEditing] = useState(false)
  const issue = problem(q)
  const subjectName = (code: string) => [...(catalog.data?.buraxilis ?? []), ...(catalog.data?.blok ?? [])].find(s => s.code === code)?.name ?? code

  return (
    <article className={cn('rounded-xl border bg-white p-3 sm:p-4', issue ? 'border-amber-200' : 'border-slate-200')}>
      <div className="flex flex-wrap items-center gap-2">
        <span className="text-sm font-semibold text-slate-500 tabular-nums">#{q.sourceNumber}</span>
        {audio && <span className="inline-flex items-center gap-1 text-xs text-indigo-700"><Headphones className="size-3.5" aria-hidden />{audio}</span>}
        {issue ? <StatusBadge tone="warning">{issue}</StatusBadge>
          : q.bank ? <StatusBadge tone="success">{`${trackLabel[q.bank.track]} · ${subjectName(q.bank.subject)}${q.bank.topic ? ` · ${q.bank.topic}` : ''}`}</StatusBadge>
          : <StatusBadge tone="info">{t('dim.bank.validNotBanked')}</StatusBadge>}
        <span className="ml-auto flex gap-1">
          {!editing && fixable(q) && (
            <button type="button" onClick={() => setEditing(true)} className="inline-flex items-center gap-1 rounded-md px-2 py-1 text-xs font-medium text-indigo-700 hover:bg-indigo-50">
              <Pencil className="size-3.5" aria-hidden />{t('dim.audio.edit')}
            </button>
          )}
          {!manual && q.bank && (
            <button type="button" onClick={() => remove.mutate(q.id)} disabled={remove.isPending}
              className="inline-flex items-center gap-1 rounded-md px-2 py-1 text-xs font-medium text-slate-600 hover:bg-slate-100">
              {t('dim.bank.removeFromBank')}
            </button>
          )}
          <button type="button" disabled={deleteQuestion.isPending}
            onClick={() => { if (window.confirm(t('dim.bank.deleteQuestion', { n: q.sourceNumber }))) deleteQuestion.mutate(q.id) }}
            className="inline-flex items-center gap-1 rounded-md px-2 py-1 text-xs font-medium text-rose-700 hover:bg-rose-50">
            <Trash2 className="size-3.5" aria-hidden />{t('common.delete')}
          </button>
        </span>
      </div>
      {editing ? <QuestionEditor question={q} onDone={() => setEditing(false)} /> : (
        <>
          <p className="mt-1 line-clamp-3 text-sm whitespace-pre-line text-slate-800">{q.text}</p>
          {q.options.length > 0 && (
            <ul className="mt-2 grid gap-1 text-sm sm:grid-cols-2">
              {q.options.map(o => (
                <li key={o.id} className={cn('truncate rounded-md px-2 py-1', o.isCorrect ? 'bg-emerald-50 font-medium text-emerald-800' : 'text-slate-600')}>
                  {o.label}) {o.text}
                </li>
              ))}
            </ul>
          )}
          {q.codedAnswer && (
            <p className="mt-2 text-sm"><span className="rounded-md bg-emerald-50 px-2 py-1 font-medium text-emerald-800">{t('dim.bank.codedAnswer')}: {q.codedAnswer}</span></p>
          )}
          {issue && !fixable(q) && <p className="mt-1 text-xs text-slate-500">{t('dim.bank.notDimFormat')}</p>}
          {q.parseWarning && <p className="mt-1 text-xs text-amber-700">{q.parseWarning}</p>}
        </>
      )}
      {deleteQuestion.isError && <p className="mt-1 text-xs text-rose-700" role="alert">{t('dim.bank.questionDeleteFailed', { error: deleteQuestion.error.message })}</p>}
    </article>
  )
}

function QuestionEditor({ question, onDone }: { question: DimSourceQuestion; onDone: () => void }) {
  const { t } = useTranslation()
  const edit = useEditDimQuestion()
  const [text, setText] = useState(question.text)
  const [codedAnswer, setCodedAnswer] = useState(question.codedAnswer ?? '')
  const coded = question.options.length === 0
  const [options, setOptions] = useState(question.options.map(o => ({ id: o.id, label: o.label, text: o.text })))
  const [correct, setCorrect] = useState(question.options.find(o => o.isCorrect)?.id ?? '')
  const name = `correct-${question.id}`
  const ready = text.trim() !== '' && (coded ? codedAnswer.trim() !== '' : correct !== '' && options.every(o => o.text.trim() !== ''))

  return (
    <div className="mt-3 flex flex-col gap-3">
      <label className="flex flex-col gap-1.5 text-sm font-medium text-slate-700">{t('dim.manual.question')}
        <textarea value={text} onChange={e => setText(e.target.value)} rows={3} maxLength={4000}
          className="w-full rounded-lg border border-slate-300 bg-white p-3 text-sm focus:border-indigo-500 focus:ring-2 focus:ring-indigo-500/20 focus:outline-none" />
      </label>
      {coded ? (
        <label className="flex flex-col gap-1.5 text-sm font-medium text-slate-700 sm:max-w-sm">{t('dim.bank.codedAnswer')}
          <input className={inputClass} value={codedAnswer} maxLength={100} placeholder={t('dim.bank.codedPlaceholder')}
            onChange={e => setCodedAnswer(e.target.value)} />
          <span className="text-xs font-normal text-slate-500">
            <Trans i18nKey="dim.bank.codedHint" components={{ c: <code /> }} />
          </span>
        </label>
      ) : (
      <fieldset className="flex flex-col gap-2">
        <legend className="mb-1 text-sm font-medium text-slate-700">{t('dim.bank.optionsLegend')}</legend>
        {options.map((o, i) => (
          <div key={o.id} className="flex items-center gap-2">
            <input type="radio" name={name} className="size-4 shrink-0 accent-emerald-600" checked={correct === o.id} onChange={() => setCorrect(o.id)} aria-label={t('questions.isCorrect', { label: o.label })} />
            <span className="w-5 text-sm font-semibold text-slate-500">{o.label})</span>
            <input className={inputClass} value={o.text} maxLength={1000} aria-label={t('dim.manual.option', { label: o.label })}
              onChange={e => setOptions(list => list.map((x, j) => (j === i ? { ...x, text: e.target.value } : x)))} />
          </div>
        ))}
      </fieldset>
      )}
      {edit.isError && <Alert title={t('questions.notSaved')}>{edit.error.message}</Alert>}
      <div className="flex gap-2">
        <Button loading={edit.isPending} disabled={!ready}
          onClick={() => edit.mutate(coded
            ? { id: question.id, text, options: [], codedAnswer }
            : { id: question.id, text, options: options.map(o => ({ id: o.id, text: o.text })), correctOptionId: correct }, { onSuccess: onDone })}>
          {t('questions.saveButton')}
        </Button>
        <Button variant="ghost" onClick={onDone}>{t('common.cancel')}</Button>
      </div>
    </div>
  )
}

/** Uploads a PDF/DOCX and waits until the parser has finished, then hands the document id back. */
function DimUpload({ disabled, onReady }: { disabled: boolean; onReady: (documentId: string) => void }) {
  const { t } = useTranslation()
  const [progress, setProgress] = useState(0)
  const [clientError, setClientError] = useState<string | null>(null)
  const [processing, setProcessing] = useState(false)
  const [failure, setFailure] = useState<string | null>(null)
  const upload = useUploadDocument(setProgress, { dim: true })

  /** Parsing runs in the background on the server; poll until it finishes. */
  async function waitForParsing(document: DocumentDto) {
    setProcessing(true)
    try {
      let current = document
      while (current.status === 'Pending' || current.status === 'Processing') {
        await new Promise(resolve => setTimeout(resolve, 1500))
        current = await api.get<DocumentDto>(`/documents/${current.id}`)
      }
      if (current.status === 'Completed') onReady(current.id)
      else setFailure(current.errorMessage ?? t('dim.bank.checkFile'))
    } catch (error) {
      setFailure(error instanceof Error ? error.message : t('dim.bank.checkFile'))
    } finally {
      setProcessing(false)
    }
  }

  function handleFile(file: File) {
    setClientError(null)
    setFailure(null)
    upload.reset()
    if (!/\.(pdf|docx)$/i.test(file.name)) return setClientError(t('documents.onlyPdfDocx'))
    if (file.size > MAX_FILE_SIZE_BYTES) return setClientError(t('documents.tooLarge'))
    setProgress(0)
    upload.mutate(file, {
      onSuccess: d => void waitForParsing(d),
      onError: error => {
        // Same file uploaded before: open the existing document instead of failing.
        const existing = error instanceof ApiError && error.status === 409 ? error.problem?.documentId as string | undefined : undefined
        if (existing) onReady(existing)
      },
    })
  }

  const duplicate = upload.error instanceof ApiError && upload.error.status === 409

  return (
    <div className="mt-4 flex flex-col gap-3">
      <FileDropzone accept="application/pdf,.pdf,application/vnd.openxmlformats-officedocument.wordprocessingml.document,.docx"
        disabled={disabled || upload.isPending || processing}
        hint={disabled ? t('dim.bank.pickSubjectFirst') : t('documents.dropHint')} onFile={handleFile} />
      {(upload.isPending || processing) && (
        <div role="status" className="rounded-lg border border-slate-200 p-3 text-sm">
          <div className="mb-2 flex justify-between font-medium">
            <span>{upload.isPending ? t('common.loadingDots') : t('documents.extracting')}</span>
            {upload.isPending && <span className="tabular-nums">{progress}%</span>}
          </div>
          <div className="h-2 overflow-hidden rounded-full bg-slate-100">
            <div className={cn('h-full rounded-full bg-indigo-600', !upload.isPending && 'animate-pulse')} style={{ width: upload.isPending ? `${progress}%` : '100%' }} />
          </div>
        </div>
      )}
      {failure && <Alert title={t('dim.bank.processFailed')}>{failure}</Alert>}
      {(clientError || (upload.isError && !duplicate)) && <Alert title={t('dim.bank.uploadFailed')}>{clientError ?? upload.error?.message}</Alert>}
      {duplicate && <p role="status" className="text-sm text-slate-600">{t('dim.bank.duplicate')}</p>}
    </div>
  )
}
