import { CheckCircle2, FileText, Loader2, Search, TriangleAlert, Upload } from 'lucide-react'
import { useMemo, useRef, useState } from 'react'
import { Alert } from '../../components/common/Alert'
import { cn } from '../../lib/utils'
import { useIndexDocument, useTutorDocuments, type TutorDocument } from './api'

interface Props {
  onStart: (documentId: string) => void
  starting: boolean
}

const accept = '.pdf,.docx,.txt'

/** Start of a new chat: pick (or upload) the one document this conversation will be about. */
export function TutorDocumentPicker({ onStart, starting }: Props) {
  const { data, isLoading, isError, refetch } = useTutorDocuments()
  const index = useIndexDocument()
  const [term, setTerm] = useState('')
  const [progress, setProgress] = useState<number | null>(null)
  const input = useRef<HTMLInputElement>(null)

  const documents = useMemo(() => {
    const needle = term.trim().toLocaleLowerCase('az')
    return (data ?? []).filter(d => !needle || d.fileName.toLocaleLowerCase('az').includes(needle))
  }, [data, term])

  const upload = (file: File | undefined) => {
    if (!file) return
    setProgress(0)
    index.mutate({ file, onProgress: setProgress }, { onSettled: () => setProgress(null) })
  }

  return (
    <div className="mx-auto w-full max-w-2xl px-4 py-8 sm:py-12">
      <h1 className="text-2xl font-bold tracking-tight sm:text-[28px]">Hansı sənəd üzərində işləyək?</h1>
      <p className="mt-2 text-slate-600">Söhbət yalnız seçdiyiniz sənədə əsaslanır. Başqa sənəd üçün yeni söhbət açın.</p>

      <div className="mt-6 flex flex-col gap-2 sm:flex-row">
        <label className="relative flex-1">
          <span className="sr-only">Sənəd axtar</span>
          <Search className="pointer-events-none absolute top-1/2 left-3 size-4 -translate-y-1/2 text-slate-400" aria-hidden />
          <input value={term} onChange={e => setTerm(e.target.value)} placeholder="Sənəd axtar…" type="search"
            className="h-11 w-full border border-slate-300 bg-white pr-3 pl-9 text-sm outline-none focus:border-indigo-500 focus:ring-2 focus:ring-indigo-100" />
        </label>
        <input ref={input} type="file" accept={accept} className="sr-only" tabIndex={-1} aria-hidden
          onChange={e => { upload(e.target.files?.[0]); e.target.value = '' }} />
        <button type="button" onClick={() => input.current?.click()} disabled={progress !== null}
          className="btn-primary inline-flex h-11 items-center justify-center gap-2 rounded-[10px] px-4 text-sm font-semibold disabled:opacity-70">
          {progress !== null ? <Loader2 className="size-4 animate-spin" aria-hidden /> : <Upload className="size-4" aria-hidden />}
          {progress !== null ? `Yüklənir ${progress}%` : 'Yeni sənəd yüklə'}
        </button>
      </div>
      <p className="mt-2 text-xs text-slate-500">PDF, DOCX və ya TXT · ən çox 50 MB. Skan edilmiş PDF-lər hələ dəstəklənmir.</p>

      {index.isError && <div className="mt-4"><Alert title="Sənəd hazırlana bilmədi">{index.error.message}</Alert></div>}

      <div className="mt-6">
        {isLoading ? (
          <ul className="space-y-2" aria-label="Yüklənir">{[0, 1, 2].map(i => <li key={i} className="h-16 animate-pulse rounded-xl bg-slate-100" />)}</ul>
        ) : isError ? (
          <Alert title="Sənədlər yüklənmədi" action={<button type="button" onClick={() => refetch()} className="text-sm font-semibold underline">Yenidən cəhd et</button>} />
        ) : documents.length === 0 ? (
          <p className="tutor-border rounded-xl border border-dashed px-4 py-10 text-center text-sm text-slate-500">
            {term ? 'Axtarışa uyğun sənəd yoxdur.' : 'Hələ sənəd yoxdur. Yuxarıdan ilk sənədinizi yükləyin.'}
          </p>
        ) : (
          <ul className="space-y-2">
            {documents.map(doc => (
              <DocumentRow key={doc.documentId} doc={doc} busy={starting || (index.isPending && index.variables && 'documentId' in index.variables && index.variables.documentId === doc.documentId) === true}
                onStart={() => onStart(doc.documentId)} onIndex={() => index.mutate({ documentId: doc.documentId })} />
            ))}
          </ul>
        )}
      </div>
    </div>
  )
}

function DocumentRow({ doc, busy, onStart, onIndex }: { doc: TutorDocument; busy: boolean; onStart: () => void; onIndex: () => void }) {
  const ready = doc.status === 'Completed'
  const working = doc.status === 'Pending' || doc.status === 'Processing'
  return (
    <li className="tutor-enter tutor-border flex items-center gap-3 rounded-xl border bg-white px-3 py-3 sm:px-4">
      <span className={cn('grid size-10 shrink-0 place-items-center rounded-lg text-[11px] font-bold uppercase', ready ? 'bg-indigo-50 text-indigo-700' : 'bg-slate-100 text-slate-500')} aria-hidden>
        {doc.format === 'pdf' ? 'PDF' : doc.format === 'docx' ? 'DOC' : <FileText className="size-4" />}
      </span>
      <div className="min-w-0 flex-1">
        <p className="truncate text-sm font-semibold">{doc.fileName}</p>
        <p className="mt-0.5 flex items-center gap-1.5 text-xs text-slate-500">
          {ready && <><CheckCircle2 className="size-3.5 text-emerald-600" aria-hidden />Hazırdır{doc.pageCount ? ` · ${doc.pageCount} səhifə` : ''}</>}
          {working && <><Loader2 className="size-3.5 animate-spin" aria-hidden />Hazırlanır…</>}
          {doc.status === 'Failed' && <><TriangleAlert className="size-3.5 text-rose-600" aria-hidden />Emal alınmadı</>}
          {doc.status === null && 'Söhbət üçün hələ hazırlanmayıb'}
        </p>
      </div>
      {ready ? (
        <button type="button" onClick={onStart} disabled={busy}
          className="btn-primary inline-flex h-9 shrink-0 items-center gap-1.5 rounded-lg px-3 text-sm font-semibold disabled:opacity-70">
          {busy && <Loader2 className="size-4 animate-spin" aria-hidden />}Söhbətə başla
        </button>
      ) : !working && (
        <button type="button" onClick={onIndex} disabled={busy}
          className="inline-flex h-9 shrink-0 items-center gap-1.5 rounded-lg border border-slate-300 px-3 text-sm font-medium text-slate-700 hover:bg-slate-50 disabled:opacity-70">
          {busy && <Loader2 className="size-4 animate-spin" aria-hidden />}{doc.status === 'Failed' ? 'Yenidən cəhd et' : 'Hazırla'}
        </button>
      )}
    </li>
  )
}
