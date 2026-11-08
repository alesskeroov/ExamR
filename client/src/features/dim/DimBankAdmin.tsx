import { CheckCircle2, FileText, KeyRound, Pencil, Trash2 } from 'lucide-react'
import { useState } from 'react'
import { Alert } from '../../components/common/Alert'
import { Button } from '../../components/common/Button'
import { EmptyState } from '../../components/common/EmptyState'
import { FileDropzone } from '../../components/common/FileDropzone'
import { StatusBadge } from '../../components/common/StatusBadge'
import { api, ApiError } from '../../lib/api-client'
import { cn } from '../../lib/utils'
import { MAX_FILE_SIZE_BYTES, useUploadDocument, type DocumentDto } from '../documents/api'
import {
  useApplyAnswerKey, useApproveValid, useDimBankStats, useDimCatalog, useDimSourceDocuments, useDimSourceQuestions,
  useEditDimQuestion, useRemoveFromBank, type DimSourceDocument, type DimSourceQuestion, type DimTrack,
} from './api'
import { inputClass, trackLabel } from './ui'

interface Classification { track: DimTrack; subject: string }

/** Mirrors the server rule (DimAdminEndpoints.BankRejection); null = the question fits DİM tests. */
function problem(q: DimSourceQuestion): string | null {
  if (q.options.length < 2) return 'Açıq sual (variant yoxdur)'
  if (q.options.length > 5) return '5-dən çox variant'
  if (q.options.filter(o => o.isCorrect).length !== 1) return 'Düzgün cavab yoxdur'
  if (!q.text.trim() || q.options.some(o => !o.text.trim())) return 'Boş mətn'
  return null
}

/** Only answer and text problems can be fixed here; the option count comes from the PDF. */
const fixable = (q: DimSourceQuestion) => q.options.length >= 2 && q.options.length <= 5

export function DimBankAdmin() {
  const documents = useDimSourceDocuments()
  const stats = useDimBankStats()
  const [documentId, setDocumentId] = useState('')
  const [upload, setUpload] = useState<Classification>({ track: 'Buraxilis', subject: '' })
  // Subject chosen at upload time, remembered per document for the one-click "add to bank".
  const [chosen, setChosen] = useState<Record<string, Classification>>({})
  const selected = documents.data?.find(d => d.id === documentId)

  return (
    <div className="flex flex-col gap-5">
      <section className="rounded-xl border border-slate-200 bg-white p-4 sm:p-5">
        <h2 className="font-semibold">PDF yüklə</h2>
        <p className="mt-1 text-sm text-slate-600">Əvvəlcə imtahanı və fənni seçin, sonra faylı yükləyin. Suallar avtomatik çıxarılır və yoxlanılır.</p>
        <SubjectFields value={upload} onChange={setUpload} />
        <DimUpload disabled={!upload.subject} onReady={id => {
          setChosen(c => ({ ...c, [id]: upload }))
          setDocumentId(id)
          void documents.refetch()
        }} />
      </section>

      <section className="rounded-xl border border-slate-200 bg-white p-4 sm:p-5">
        <div className="flex flex-wrap items-baseline justify-between gap-2">
          <h2 className="font-semibold">Sənədlərim</h2>
          {stats.data && stats.data.length > 0 && (
            <p className="text-sm text-slate-500">Bankda cəmi: {stats.data.map(s => `${trackLabel[s.track]} ${s.name} ${s.count}`).join(' · ')}</p>
          )}
        </div>
        {documents.isPending ? <div className="mt-3 h-20 animate-pulse rounded-lg bg-slate-200/60" aria-label="Yüklənir" />
          : documents.isError ? <div className="mt-3"><Alert title="Sənədlər yüklənmədi">{documents.error.message}</Alert></div>
          : documents.data.length === 0 ? <p className="mt-2 text-sm text-slate-500">Hələ sənəd yoxdur — yuxarıdan PDF yükləyin.</p>
          : (
            <ul className="mt-3 grid gap-2 sm:grid-cols-2">
              {documents.data.map(d => (
                <li key={d.id}>
                  <button type="button" onClick={() => setDocumentId(d.id)} aria-pressed={d.id === documentId}
                    className={cn('w-full rounded-lg border p-3 text-left transition-colors', d.id === documentId ? 'border-indigo-400 bg-indigo-50/60 ring-1 ring-indigo-400' : 'border-slate-200 hover:bg-slate-50')}>
                    <span className="flex items-center gap-2 font-medium break-all"><FileText className="size-4 shrink-0 text-indigo-600" aria-hidden />{d.originalFileName}</span>
                    <DocumentCounts document={d} />
                  </button>
                </li>
              ))}
            </ul>
          )}
      </section>

      {selected && <DocumentPanel key={selected.id} document={selected} initial={chosen[selected.id]} />}
    </div>
  )
}

function DocumentCounts({ document: d }: { document: DimSourceDocument }) {
  const banked = d.bank.reduce((sum, b) => sum + b.count, 0)
  return (
    <span className="mt-1 block text-sm text-slate-600 tabular-nums">
      <span className="text-emerald-700">{d.valid} düzgün</span>
      {d.questionCount - d.valid > 0 && <> · <span className="text-amber-700">{d.questionCount - d.valid} səhv</span></>}
      {' · '}{banked > 0 ? `${banked} bankda (${d.bank.map(b => `${trackLabel[b.track]} ${b.name}`).join(', ')})` : 'bankda yoxdur'}
    </span>
  )
}

function SubjectFields({ value, onChange }: { value: Classification; onChange: (next: Classification) => void }) {
  const catalog = useDimCatalog()
  const subjects = value.track === 'Buraxilis' ? catalog.data?.buraxilis ?? [] : catalog.data?.blok ?? []
  return (
    <div className="mt-3 grid gap-3 sm:grid-cols-2">
      <label className="flex flex-col gap-1.5 text-sm font-medium text-slate-700">İmtahan
        <select className={inputClass} value={value.track} onChange={e => onChange({ track: e.target.value as DimTrack, subject: '' })}>
          <option value="Buraxilis">Buraxılış (XI sinif)</option>
          <option value="Blok">Blok</option>
        </select>
      </label>
      <label className="flex flex-col gap-1.5 text-sm font-medium text-slate-700">Fənn
        <select className={inputClass} value={value.subject} onChange={e => onChange({ ...value, subject: e.target.value })}>
          <option value="">Fənn seçin</option>
          {subjects.map(s => <option key={s.code} value={s.code}>{s.name}</option>)}
        </select>
      </label>
    </div>
  )
}

function DocumentPanel({ document, initial }: { document: DimSourceDocument; initial?: Classification }) {
  const questions = useDimSourceQuestions(document.id)
  const approve = useApproveValid()
  const [target, setTarget] = useState<Classification>(initial ?? (document.bank[0] ? { track: document.bank[0].track, subject: document.bank[0].subject } : { track: 'Buraxilis', subject: '' }))
  const [topic, setTopic] = useState('')
  const [view, setView] = useState<'wrong' | 'all'>('wrong')

  if (questions.isPending) return <div className="h-64 animate-pulse rounded-xl bg-slate-200/60" aria-label="Yüklənir" />
  if (questions.isError) return <Alert title="Suallar yüklənmədi">{questions.error.message}</Alert>

  const all = questions.data
  const wrong = all.filter(q => problem(q) !== null)
  const valid = all.length - wrong.length
  const missingAnswers = wrong.filter(q => fixable(q) && q.options.filter(o => o.isCorrect).length !== 1).length
  const visible = view === 'wrong' ? wrong : all

  return (
    <>
      <section className="rounded-xl border border-indigo-200 bg-white p-4 sm:p-5">
        <h2 className="font-semibold break-all">{document.originalFileName}</h2>
        <p className="mt-1 text-sm text-slate-600">
          {all.length} sual tapıldı: <span className="font-medium text-emerald-700">{valid} düzgün formatda</span>
          {wrong.length > 0 && <>, <span className="font-medium text-amber-700">{wrong.length} səhv</span> (aşağıda düzəldə bilərsiniz)</>}.
        </p>
        <SubjectFields value={target} onChange={setTarget} />
        <details className="mt-3 text-sm">
          <summary className="cursor-pointer font-medium text-slate-600">Mövzu əlavə et (istəyə bağlı)</summary>
          <label className="mt-2 flex flex-col gap-1.5 text-slate-700">Mövzu — nəticədə zəif mövzuları göstərmək üçün
            <input className={inputClass} value={topic} maxLength={200} onChange={e => setTopic(e.target.value)} placeholder="məs. Kvadrat tənliklər" />
          </label>
        </details>
        <div className="mt-4 flex flex-wrap items-center gap-3">
          <Button loading={approve.isPending} disabled={!target.subject || valid === 0}
            onClick={() => approve.mutate({ documentId: document.id, ...target, topic: topic.trim() || null })}>
            <CheckCircle2 className="size-4" aria-hidden />Düzgün {valid} sualı banka əlavə et
          </Button>
          {approve.data && (
            <p className="text-sm text-slate-600" role="status">
              {approve.data.approved} sual banka əlavə edildi.{approve.data.rejected.length > 0 && ` ${approve.data.rejected.length} səhv sual əlavə edilmədi.`}
            </p>
          )}
        </div>
        {approve.isError && <div className="mt-2"><Alert title="Əlavə alınmadı">{approve.error.message}</Alert></div>}
        {missingAnswers > 0 && <AnswerKey documentId={document.id} missing={missingAnswers} />}
      </section>

      <section aria-label="Sənədin sualları" className="flex flex-col gap-3">
        <div role="tablist" aria-label="Suallar" className="inline-flex self-start rounded-lg border border-slate-200 bg-white p-1">
          {([['wrong', `Səhv suallar (${wrong.length})`], ['all', `Bütün suallar (${all.length})`]] as const).map(([value, label]) => (
            <button key={value} type="button" role="tab" aria-selected={view === value} onClick={() => setView(value)}
              className={cn('rounded-md px-3 py-1.5 text-sm font-medium whitespace-nowrap', view === value ? 'bg-indigo-600 text-white' : 'text-slate-600 hover:bg-slate-100')}>
              {label}
            </button>
          ))}
        </div>
        {visible.length === 0 ? (
          <EmptyState title={view === 'wrong' ? 'Səhv sual yoxdur' : 'Sual yoxdur'} description={view === 'wrong' ? 'Bütün suallar düzgün formatdadır.' : 'Bu sənəddə sual tapılmadı.'} />
        ) : (
          <ul className="flex flex-col gap-2">{visible.map(q => <li key={q.id}><QuestionItem question={q} /></li>)}</ul>
        )}
      </section>
    </>
  )
}

function AnswerKey({ documentId, missing }: { documentId: string; missing: number }) {
  const applyKey = useApplyAnswerKey()
  const [keyText, setKeyText] = useState('')
  return (
    <details className="mt-4 border-t border-slate-100 pt-3 text-sm">
      <summary className="flex cursor-pointer items-center gap-2 font-medium text-slate-700">
        <KeyRound className="size-4 text-indigo-600" aria-hidden />Cavab açarını yapışdır
        <span className="font-normal text-amber-700">— {missing} sualın cavabı tapılmadı</span>
      </summary>
      <p className="mt-2 text-slate-600">Format: <code>1-A 2-C 3-B</code> və ya hər sətirdə bir cüt. Nömrələr PDF-dəki nömrələrdir.</p>
      <textarea value={keyText} onChange={e => setKeyText(e.target.value)} rows={3} aria-label="Cavab açarı"
        className="mt-2 w-full rounded-lg border border-slate-300 bg-white p-3 font-mono text-sm focus:border-indigo-500 focus:ring-2 focus:ring-indigo-500/20 focus:outline-none" />
      <div className="mt-2 flex flex-wrap items-center gap-3">
        <Button variant="secondary" loading={applyKey.isPending} disabled={keyText.trim().length < 2}
          onClick={() => applyKey.mutate({ documentId, text: keyText })}>Açarı tətbiq et</Button>
        {applyKey.data && (
          <p className="text-slate-600" role="status">
            {applyKey.data.applied} suala tətbiq edildi.
            {applyKey.data.notFound.length > 0 && ` Tapılmayan nömrələr: ${applyKey.data.notFound.join(', ')}.`}
            {applyKey.data.invalid.length > 0 && ` Variant sayı uyğun deyil: ${applyKey.data.invalid.join(', ')}.`}
          </p>
        )}
      </div>
      {applyKey.isError && <div className="mt-2"><Alert title="Açar tətbiq edilmədi">{applyKey.error.message}</Alert></div>}
    </details>
  )
}

function QuestionItem({ question: q }: { question: DimSourceQuestion }) {
  const catalog = useDimCatalog()
  const remove = useRemoveFromBank()
  const [editing, setEditing] = useState(false)
  const issue = problem(q)
  const subjectName = (code: string) => [...(catalog.data?.buraxilis ?? []), ...(catalog.data?.blok ?? [])].find(s => s.code === code)?.name ?? code

  return (
    <article className={cn('rounded-xl border bg-white p-3 sm:p-4', issue ? 'border-amber-200' : 'border-slate-200')}>
      <div className="flex flex-wrap items-center gap-2">
        <span className="text-sm font-semibold text-slate-500 tabular-nums">#{q.sourceNumber}</span>
        {issue ? <StatusBadge tone="warning">{issue}</StatusBadge>
          : q.bank ? <StatusBadge tone="success">{`${trackLabel[q.bank.track]} · ${subjectName(q.bank.subject)}${q.bank.topic ? ` · ${q.bank.topic}` : ''}`}</StatusBadge>
          : <StatusBadge tone="info">Düzgün, bankda deyil</StatusBadge>}
        <span className="ml-auto flex gap-1">
          {!editing && fixable(q) && (
            <button type="button" onClick={() => setEditing(true)} className="inline-flex items-center gap-1 rounded-md px-2 py-1 text-xs font-medium text-indigo-700 hover:bg-indigo-50">
              <Pencil className="size-3.5" aria-hidden />Düzəlt
            </button>
          )}
          {q.bank && (
            <button type="button" onClick={() => remove.mutate(q.id)} disabled={remove.isPending}
              className="inline-flex items-center gap-1 rounded-md px-2 py-1 text-xs font-medium text-rose-700 hover:bg-rose-50">
              <Trash2 className="size-3.5" aria-hidden />Bankdan çıxar
            </button>
          )}
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
          {issue && !fixable(q) && <p className="mt-1 text-xs text-slate-500">Bu sual DİM formatına uyğun deyil və sınağa düşməyəcək.</p>}
          {q.parseWarning && <p className="mt-1 text-xs text-amber-700">{q.parseWarning}</p>}
        </>
      )}
    </article>
  )
}

function QuestionEditor({ question, onDone }: { question: DimSourceQuestion; onDone: () => void }) {
  const edit = useEditDimQuestion()
  const [text, setText] = useState(question.text)
  const [options, setOptions] = useState(question.options.map(o => ({ id: o.id, label: o.label, text: o.text })))
  const [correct, setCorrect] = useState(question.options.find(o => o.isCorrect)?.id ?? '')
  const name = `correct-${question.id}`
  const ready = text.trim() !== '' && correct !== '' && options.every(o => o.text.trim() !== '')

  return (
    <div className="mt-3 flex flex-col gap-3">
      <label className="flex flex-col gap-1.5 text-sm font-medium text-slate-700">Sual
        <textarea value={text} onChange={e => setText(e.target.value)} rows={3} maxLength={4000}
          className="w-full rounded-lg border border-slate-300 bg-white p-3 text-sm focus:border-indigo-500 focus:ring-2 focus:ring-indigo-500/20 focus:outline-none" />
      </label>
      <fieldset className="flex flex-col gap-2">
        <legend className="mb-1 text-sm font-medium text-slate-700">Variantlar — düzgün cavabı işarələyin</legend>
        {options.map((o, i) => (
          <div key={o.id} className="flex items-center gap-2">
            <input type="radio" name={name} className="size-4 shrink-0 accent-emerald-600" checked={correct === o.id} onChange={() => setCorrect(o.id)} aria-label={`${o.label} düzgün cavabdır`} />
            <span className="w-5 text-sm font-semibold text-slate-500">{o.label})</span>
            <input className={inputClass} value={o.text} maxLength={1000} aria-label={`Variant ${o.label}`}
              onChange={e => setOptions(list => list.map((x, j) => (j === i ? { ...x, text: e.target.value } : x)))} />
          </div>
        ))}
      </fieldset>
      {edit.isError && <Alert title="Yadda saxlanmadı">{edit.error.message}</Alert>}
      <div className="flex gap-2">
        <Button loading={edit.isPending} disabled={!ready}
          onClick={() => edit.mutate({ id: question.id, text, options: options.map(o => ({ id: o.id, text: o.text })), correctOptionId: correct }, { onSuccess: onDone })}>
          Yadda saxla
        </Button>
        <Button variant="ghost" onClick={onDone}>Ləğv et</Button>
      </div>
    </div>
  )
}

/** Uploads a PDF/DOCX and waits until the parser has finished, then hands the document id back. */
function DimUpload({ disabled, onReady }: { disabled: boolean; onReady: (documentId: string) => void }) {
  const [progress, setProgress] = useState(0)
  const [clientError, setClientError] = useState<string | null>(null)
  const [processing, setProcessing] = useState(false)
  const [failure, setFailure] = useState<string | null>(null)
  const upload = useUploadDocument(setProgress)

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
      else setFailure(current.errorMessage ?? 'Faylı yoxlayıb yenidən yükləyin.')
    } catch (error) {
      setFailure(error instanceof Error ? error.message : 'Faylı yoxlayıb yenidən yükləyin.')
    } finally {
      setProcessing(false)
    }
  }

  function handleFile(file: File) {
    setClientError(null)
    setFailure(null)
    upload.reset()
    if (!/\.(pdf|docx)$/i.test(file.name)) return setClientError('Yalnız PDF və DOCX faylları qəbul edilir.')
    if (file.size > MAX_FILE_SIZE_BYTES) return setClientError('Fayl 20 MB-dan böyük ola bilməz.')
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
        hint={disabled ? 'Əvvəlcə fənni seçin' : 'Maksimum 20 MB · mətn əsaslı PDF və DOCX'} onFile={handleFile} />
      {(upload.isPending || processing) && (
        <div role="status" className="rounded-lg border border-slate-200 p-3 text-sm">
          <div className="mb-2 flex justify-between font-medium">
            <span>{upload.isPending ? 'Yüklənir…' : 'Suallar çıxarılır…'}</span>
            {upload.isPending && <span className="tabular-nums">{progress}%</span>}
          </div>
          <div className="h-2 overflow-hidden rounded-full bg-slate-100">
            <div className={cn('h-full rounded-full bg-indigo-600', !upload.isPending && 'animate-pulse')} style={{ width: upload.isPending ? `${progress}%` : '100%' }} />
          </div>
        </div>
      )}
      {failure && <Alert title="Sənəd emal olunmadı">{failure}</Alert>}
      {(clientError || (upload.isError && !duplicate)) && <Alert title="Yükləmə alınmadı">{clientError ?? upload.error?.message}</Alert>}
      {duplicate && <p role="status" className="text-sm text-slate-600">Bu fayl artıq yüklənib — mövcud sənəd açıldı.</p>}
    </div>
  )
}
