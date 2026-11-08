import { CheckCircle2, KeyRound, Trash2 } from 'lucide-react'
import { useState } from 'react'
import { Alert } from '../../components/common/Alert'
import { FileDropzone } from '../../components/common/FileDropzone'
import { Button } from '../../components/common/Button'
import { EmptyState } from '../../components/common/EmptyState'
import { StatusBadge } from '../../components/common/StatusBadge'
import { api, ApiError } from '../../lib/api-client'
import { cn } from '../../lib/utils'
import { MAX_FILE_SIZE_BYTES, useUploadDocument, type DocumentDto } from '../documents/api'
import {
  useApplyAnswerKey, useApproveToBank, useDimBankStats, useDimCatalog, useDimSourceDocuments, useDimSourceQuestions,
  useRemoveFromBank, type DimSourceQuestion, type DimTrack,
} from './api'
import { inputClass, trackLabel } from './ui'


type Filter = 'all' | 'outside' | 'noAnswer'

/** Mirrors the server rule: 2–5 options with exactly one correct answer. */
const eligible = (q: DimSourceQuestion) =>
  q.options.length >= 2 && q.options.length <= 5 && q.options.filter(o => o.isCorrect).length === 1

export function DimBankAdmin() {
  const documents = useDimSourceDocuments()
  const stats = useDimBankStats()
  const [documentId, setDocumentId] = useState('')

  return (
    <div className="flex flex-col gap-5">
      <section className="rounded-xl border border-slate-200 bg-white p-4 sm:p-5">
        <h2 className="font-semibold">Bankdakı suallar</h2>
        {stats.data && stats.data.length > 0 ? (
          <ul className="mt-3 flex flex-wrap gap-2">
            {stats.data.map(s => (
              <li key={`${s.track}-${s.subject}`} className="rounded-lg bg-slate-100 px-3 py-1.5 text-sm">
                <span className="text-slate-500">{trackLabel[s.track]} · </span>
                <span className="font-medium">{s.name}</span>
                <span className="text-slate-600 tabular-nums"> · {s.count} sual{s.topics > 0 && `, ${s.topics} mövzu`}</span>
              </li>
            ))}
          </ul>
        ) : (
          <p className="mt-2 text-sm text-slate-500">{stats.isPending ? 'Yüklənir…' : 'Bank hələ boşdur.'}</p>
        )}
      </section>

      <section className="rounded-xl border border-slate-200 bg-white p-4 sm:p-5">
        <h2 className="font-semibold">1. PDF yüklə</h2>
        <p className="mt-1 text-sm text-slate-600">Sınaq PDF-ini seçin — suallar avtomatik çıxarılır. Sonra fənni seçib sualları banka əlavə edin.</p>
        <DimUpload onReady={id => { void documents.refetch(); setDocumentId(id) }} />
        {(documents.data?.length ?? 0) > 0 && (
          <label className="mt-4 flex flex-col gap-1.5 text-sm font-medium text-slate-700">
            və ya əvvəl yüklənmiş sənədi seçin
            <select className={inputClass} value={documentId} onChange={e => setDocumentId(e.target.value)}>
              <option value="">Sənəd seçin</option>
              {documents.data?.map(d => <option key={d.id} value={d.id}>{d.originalFileName} ({d.questionCount} sual)</option>)}
            </select>
          </label>
        )}
      </section>

      {documentId && <DocumentQuestions key={documentId} documentId={documentId} />}
    </div>
  )
}

function DocumentQuestions({ documentId }: { documentId: string }) {
  const questions = useDimSourceQuestions(documentId)
  const catalog = useDimCatalog()
  const applyKey = useApplyAnswerKey()
  const approve = useApproveToBank()
  const remove = useRemoveFromBank()
  const [keyText, setKeyText] = useState('')
  const [track, setTrack] = useState<DimTrack>('Buraxilis')
  const [subject, setSubject] = useState('')
  const [topic, setTopic] = useState('')
  const [filter, setFilter] = useState<Filter>('all')
  const [selected, setSelected] = useState<Set<string>>(new Set())

  const subjects = track === 'Buraxilis' ? catalog.data?.buraxilis ?? [] : catalog.data?.blok ?? []
  const all = questions.data ?? []
  const visible = all.filter(q => filter === 'all' || (filter === 'outside' ? q.bank === null : !eligible(q)))
  const selectable = visible.filter(eligible)
  const allSelected = selectable.length > 0 && selectable.every(q => selected.has(q.id))
  const missingAnswers = all.filter(q => q.options.length >= 2 && q.options.length <= 5 && !eligible(q)).length

  const toggle = (id: string) => setSelected(current => {
    const next = new Set(current)
    if (next.has(id)) next.delete(id)
    else next.add(id)
    return next
  })

  const toggleAll = () => setSelected(allSelected ? new Set() : new Set(selectable.map(q => q.id)))

  if (questions.isPending) return <div className="h-64 animate-pulse rounded-xl bg-slate-200/60" aria-label="Yüklənir" />
  if (questions.isError) return <Alert title="Suallar yüklənmədi">{questions.error.message}</Alert>

  return (
    <>
      <section className="rounded-xl border border-slate-200 bg-white p-4 sm:p-5">
        <h2 className="font-semibold">2. Fənni seç və banka əlavə et</h2>
        <p className="mt-1 text-sm text-slate-600">
          {all.length} sual tapıldı, {all.filter(eligible).length} yararlıdır. Aşağıda sualları seçin (və ya “hamısını seç”) və təsdiqləyin.
        </p>
        <div className="mt-3 grid gap-3 sm:grid-cols-2">
          <label className="flex flex-col gap-1.5 text-sm font-medium text-slate-700">İmtahan
            <select className={inputClass} value={track} onChange={e => { setTrack(e.target.value as DimTrack); setSubject('') }}>
              <option value="Buraxilis">Buraxılış (XI sinif)</option>
              <option value="Blok">Blok</option>
            </select>
          </label>
          <label className="flex flex-col gap-1.5 text-sm font-medium text-slate-700">Fənn
            <select className={inputClass} value={subject} onChange={e => setSubject(e.target.value)}>
              <option value="">Fənn seçin</option>
              {subjects.map(s => <option key={s.code} value={s.code}>{s.name}</option>)}
            </select>
          </label>
        </div>
        <details className="mt-3 text-sm">
          <summary className="cursor-pointer font-medium text-slate-600">Mövzu əlavə et (istəyə bağlı)</summary>
          <label className="mt-2 flex flex-col gap-1.5 text-slate-700">Mövzu — nəticədə zəif mövzuları göstərmək üçün
            <input className={inputClass} value={topic} maxLength={200} onChange={e => setTopic(e.target.value)} placeholder="məs. Kvadrat tənliklər" />
          </label>
        </details>
        <div className="mt-4 flex flex-wrap items-center gap-3">
          <Button loading={approve.isPending} disabled={!subject || selected.size === 0}
            onClick={() => approve.mutate({ track, subject, topic: topic.trim() || null, questionIds: [...selected] }, { onSuccess: () => setSelected(new Set()) })}>
            <CheckCircle2 className="size-4" aria-hidden />Seçilmiş {selected.size} sualı banka əlavə et
          </Button>
          {approve.data && (
            <p className="text-sm text-slate-600" role="status">
              {approve.data.approved} sual banka əlavə edildi{approve.data.rejected.length > 0 && `, ${approve.data.rejected.length} rədd edildi (${[...new Set(approve.data.rejected.map(r => r.reason))].join(' ')})`}.
            </p>
          )}
        </div>
        {approve.isError && <div className="mt-2"><Alert title="Əlavə alınmadı">{approve.error.message}</Alert></div>}

        <details className="mt-4 border-t border-slate-100 pt-3 text-sm" open={missingAnswers > 0}>
          <summary className="flex cursor-pointer items-center gap-2 font-medium text-slate-700">
            <KeyRound className="size-4 text-indigo-600" aria-hidden />Cavab açarı
            {missingAnswers > 0 && <span className="font-normal text-amber-700">— {missingAnswers} sualın cavabı tapılmadı</span>}
          </summary>
          <p className="mt-2 text-slate-600">PDF-in cavab açarını yapışdırın: <code>1-A 2-C 3-B</code> və ya hər sətirdə bir cüt.</p>
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
                {applyKey.data.conflicts.length > 0 && ` Təkrarlanan (sonuncu götürüldü): ${applyKey.data.conflicts.join(', ')}.`}
              </p>
            )}
          </div>
          {applyKey.isError && <div className="mt-2"><Alert title="Açar tətbiq edilmədi">{applyKey.error.message}</Alert></div>}
        </details>
      </section>

      <section aria-label="Sənədin sualları" className="flex flex-col gap-3">
        <div className="flex flex-wrap items-center justify-between gap-3">
          <div role="tablist" aria-label="Filtr" className="inline-flex rounded-lg border border-slate-200 bg-white p-1">
            {([['all', `Hamısı (${all.length})`], ['outside', 'Bankda olmayan'], ['noAnswer', 'Cavabsız / yararsız']] as const).map(([value, label]) => (
              <button key={value} type="button" role="tab" aria-selected={filter === value} onClick={() => setFilter(value)}
                className={cn('rounded-md px-3 py-1.5 text-sm font-medium whitespace-nowrap', filter === value ? 'bg-indigo-600 text-white' : 'text-slate-600 hover:bg-slate-100')}>
                {label}
              </button>
            ))}
          </div>
          <label className="flex items-center gap-2 text-sm text-slate-700">
            <input type="checkbox" className="size-4 accent-indigo-600" checked={allSelected} onChange={toggleAll} disabled={selectable.length === 0} />
            Yararlı olanların hamısını seç ({selectable.length})
          </label>
        </div>

        {visible.length === 0 ? (
          <EmptyState title="Sual yoxdur" description="Bu filtrə uyğun sual tapılmadı." />
        ) : (
          <ul className="flex flex-col gap-2">
            {visible.map(q => (
              <li key={q.id} className={cn('rounded-xl border bg-white p-3 sm:p-4', selected.has(q.id) ? 'border-indigo-400 ring-1 ring-indigo-400' : 'border-slate-200')}>
                <div className="flex items-start gap-3">
                  <input type="checkbox" className="mt-1 size-4 shrink-0 accent-indigo-600" aria-label={`Sual ${q.sourceNumber}`}
                    checked={selected.has(q.id)} disabled={!eligible(q)} onChange={() => toggle(q.id)} />
                  <div className="min-w-0 flex-1">
                    <div className="flex flex-wrap items-center gap-2">
                      <span className="text-sm font-semibold text-slate-500 tabular-nums">#{q.sourceNumber}</span>
                      {q.bank ? <StatusBadge tone="success">{`${trackLabel[q.bank.track]} · ${subjects.find(s => s.code === q.bank?.subject)?.name ?? q.bank.subject}${q.bank.topic ? ` · ${q.bank.topic}` : ''}`}</StatusBadge>
                        : !eligible(q) ? <StatusBadge tone="warning">{q.options.length < 2 ? 'Açıq sual' : q.options.length > 5 ? '5-dən çox variant' : 'Cavab yoxdur'}</StatusBadge>
                        : q.verificationStatus === 'AutoDetected' ? <StatusBadge tone="info">Cavab avtomatik tapılıb</StatusBadge> : null}
                      {q.bank && (
                        <button type="button" onClick={() => remove.mutate(q.id)} disabled={remove.isPending}
                          className="ml-auto inline-flex items-center gap-1 rounded-md px-2 py-1 text-xs font-medium text-rose-700 hover:bg-rose-50">
                          <Trash2 className="size-3.5" aria-hidden />Bankdan çıxar
                        </button>
                      )}
                    </div>
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
                    {q.parseWarning && <p className="mt-1 text-xs text-amber-700">{q.parseWarning}</p>}
                  </div>
                </div>
              </li>
            ))}
          </ul>
        )}
      </section>
    </>
  )
}

/** Uploads a PDF/DOCX and waits until the parser has finished, then hands the document id back. */
function DimUpload({ onReady }: { onReady: (documentId: string) => void }) {
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
        disabled={upload.isPending || processing} hint="Maksimum 20 MB · mətn əsaslı PDF və DOCX" onFile={handleFile} />
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
