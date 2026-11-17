import { UsageMeter } from '../../components/common/UsageMeter'
import { useUsageLimit } from '../usage/api'
import { CheckCircle2, FileText, Layers, Loader2, RotateCcw, Search, Trash2, TriangleAlert, Upload } from 'lucide-react'
import { useMemo, useRef, useState } from 'react'
import { Alert } from '../../components/common/Alert'
import { cn } from '../../lib/utils'
import { useDeleteTutorDocument, useIndexDocument, useTutorDocuments, type TutorDocument } from './api'
import { useConfirm } from '../../lib/use-confirm'
import { useTranslation } from 'react-i18next'

interface Props {
  onStart: (documentId: string) => void
  starting: boolean
  /** Why the server refused the chosen document (not a PDF, a test, no open questions). */
  startError?: string | null
  /** Documents that already have a chat: "Davam et" reopens it instead of starting a new one. */
  resumable?: string[]
}

// PDF, Word (.docx) and PowerPoint (.pptx); the server checks the type and the content again.
// How the guide works, shown once above the upload so a first-time user knows what to expect.
const steps = [
  { key: 'upload', icon: Upload },
  { key: 'cards', icon: Layers },
  { key: 'review', icon: RotateCcw },
] as const

const accept = '.pdf,.docx,.pptx,application/pdf,application/vnd.openxmlformats-officedocument.wordprocessingml.document,application/vnd.openxmlformats-officedocument.presentationml.presentation'

/** Start of a new chat: pick (or upload) the one document this conversation will be about. */
export function TutorDocumentPicker({ onStart, starting, startError, resumable = [] }: Props) {
  const { t } = useTranslation()
  const { data, isLoading, isError, refetch } = useTutorDocuments()
  const monthly = useUsageLimit('Upload')
  const index = useIndexDocument()
  const deletion = useDeleteTutorDocument()
  const confirm = useConfirm()
  const [term, setTerm] = useState('')
  const [progress, setProgress] = useState<number | null>(null)
  const [typeError, setTypeError] = useState<string | null>(null)
  const input = useRef<HTMLInputElement>(null)

  const documents = useMemo(() => {
    const needle = term.trim().toLocaleLowerCase('az')
    return (data ?? []).filter(d => !needle || d.fileName.toLocaleLowerCase('az').includes(needle))
  }, [data, term])

  const upload = (file: File | undefined) => {
    if (!file || monthly.blocked) return
    const name = file.name.toLowerCase()
    const error = name.endsWith('.pdf') || name.endsWith('.docx') || name.endsWith('.pptx') ? null
      : name.endsWith('.doc') ? t('tutor.picker.oldDoc')
      : name.endsWith('.ppt') ? t('tutor.picker.oldPpt')
      : t('tutor.picker.types')
    setTypeError(error)
    if (error) return
    setProgress(0)
    index.mutate({ file, onProgress: setProgress }, { onSettled: () => setProgress(null) })
  }

  const remove = async (doc: TutorDocument) => {
    const ok = await confirm({
      title: t('tutor.picker.deleteTitle'), confirmLabel: t('tutor.picker.delete'), danger: true,
      description: t('tutor.picker.deleteText', { name: doc.fileName }),
    })
    if (ok) deletion.mutate(doc.documentId)
  }

  return (
    <div className="mx-auto w-full max-w-2xl px-4 py-8 sm:py-12">
      <h1 className="text-2xl font-bold tracking-tight sm:text-[28px]">{t('tutor.picker.title')}</h1>
      <p className="mt-2 text-slate-600">{t('tutor.picker.text')}</p>
      {/* Container query: the guide's side panels can leave this column narrow even on a wide screen. */}
      <div className="@container mt-4">
        <ol className="grid gap-2 @lg:grid-cols-3">
          {steps.map(({ key, icon: Icon }, i) => (
            <li key={key} className="tutor-border flex gap-3 rounded-xl border bg-white p-3 @lg:flex-col @lg:gap-2">
              <span className="grid size-8 shrink-0 place-items-center rounded-lg bg-indigo-50 text-indigo-700" aria-hidden><Icon className="size-4" /></span>
              <div className="min-w-0">
                <p className="text-sm font-semibold">{i + 1}. {t(`tutor.picker.steps.${key}.title`)}</p>
                <p className="mt-0.5 text-xs text-slate-600">{t(`tutor.picker.steps.${key}.text`)}</p>
              </div>
            </li>
          ))}
        </ol>
      </div>

      <UsageMeter kind="Upload" />
      <div className="mt-6 flex flex-col gap-2 sm:flex-row">
        <label className="relative flex-1">
          <span className="sr-only">{t('tutor.picker.search')}</span>
          <Search className="pointer-events-none absolute top-1/2 left-3 size-4 -translate-y-1/2 text-slate-400" aria-hidden />
          <input value={term} onChange={e => setTerm(e.target.value)} placeholder={t('tutor.picker.searchPlaceholder')} type="search"
            className="h-11 w-full border border-slate-300 bg-white pr-3 pl-9 text-sm outline-none focus:border-indigo-500 focus:ring-2 focus:ring-indigo-100" />
        </label>
        <input ref={input} type="file" accept={accept} disabled={monthly.blocked || progress !== null} className="sr-only" tabIndex={-1} aria-hidden
          onChange={e => { upload(e.target.files?.[0]); e.target.value = '' }} />
        <button type="button" onClick={() => input.current?.click()} disabled={monthly.blocked || progress !== null}
          className="btn-primary inline-flex h-11 items-center justify-center gap-2 rounded-[10px] px-4 text-sm font-semibold disabled:opacity-70">
          {progress !== null ? <Loader2 className="size-4 animate-spin" aria-hidden /> : <Upload className="size-4" aria-hidden />}
          {progress !== null ? t('tutor.picker.uploading', { n: progress }) : t('documents.uploadNew')}
        </button>
      </div>
      <p className="mt-2 text-xs text-slate-500">{t('tutor.picker.hint')}</p>

      {typeError && <div className="mt-4"><Alert title={t('tutor.picker.unsupported')}>{typeError}</Alert></div>}
      {index.isError && <div className="mt-4"><Alert title={t('tutor.picker.indexFailed')}>{index.error.message}</Alert></div>}
      {deletion.isError && <div className="mt-4"><Alert title={t('tutor.picker.deleteFailed')}>{deletion.error.message}</Alert></div>}
      {startError && <div className="mt-4"><Alert title={t('tutor.picker.startFailed')}>{startError}</Alert></div>}

      <div className="mt-6">
        {isLoading ? (
          <ul className="space-y-2" aria-label={t('common.loading')}>{[0, 1, 2].map(i => <li key={i} className="h-16 animate-pulse rounded-xl bg-slate-100" />)}</ul>
        ) : isError ? (
          <Alert title={t('documents.listFailed')} action={<button type="button" onClick={() => refetch()} className="text-sm font-semibold underline">{t('common.retry')}</button>} />
        ) : documents.length === 0 ? (
          <p className="tutor-border rounded-xl border border-dashed px-4 py-10 text-center text-sm text-slate-500">
            {term ? t('tutor.picker.noMatch') : t('tutor.picker.empty')}
          </p>
        ) : (
          <ul className="space-y-2">
            {documents.map(doc => (
              <DocumentRow key={doc.documentId} doc={doc} resumable={resumable.includes(doc.documentId)} busy={starting || (index.isPending && index.variables && 'documentId' in index.variables && index.variables.documentId === doc.documentId) === true}
                deleting={deletion.isPending && deletion.variables === doc.documentId}
                onStart={() => onStart(doc.documentId)} onIndex={() => index.mutate({ documentId: doc.documentId })} onDelete={() => void remove(doc)} />
            ))}
          </ul>
        )}
      </div>
    </div>
  )
}

function DocumentRow({ doc, resumable, busy, deleting, onStart, onIndex, onDelete }: {
  doc: TutorDocument; resumable: boolean; busy: boolean; deleting: boolean; onStart: () => void; onIndex: () => void; onDelete: () => void
}) {
  const { t } = useTranslation()
  const ready = doc.status === 'Completed'
  const working = doc.status === 'Pending' || doc.status === 'Processing'
  return (
    <li className="tutor-enter tutor-border flex items-center gap-3 rounded-xl border bg-white px-3 py-3 sm:px-4">
      <span className={cn('grid size-10 shrink-0 place-items-center rounded-lg text-[11px] font-bold uppercase', ready ? 'bg-indigo-50 text-indigo-700' : 'bg-slate-100 text-slate-500')} aria-hidden>
        {doc.format === 'pdf' ? 'PDF' : doc.format === 'docx' ? 'DOC' : doc.format === 'pptx' ? 'PPT' : <FileText className="size-4" />}
      </span>
      <div className="min-w-0 flex-1">
        <p className="truncate text-sm font-semibold">{doc.fileName}</p>
        <p className="mt-0.5 flex items-center gap-1.5 text-xs text-slate-500">
          {ready && <><CheckCircle2 className="size-3.5 text-emerald-600" aria-hidden />{t('documents.status.Completed')}{doc.pageCount ? ` · ${doc.format === 'pptx' ? t('tutor.picker.slides', { count: doc.pageCount }) : t('documents.pages', { count: doc.pageCount })}` : ''}</>}
          {working && <><Loader2 className="size-3.5 animate-spin" aria-hidden />{t('tutor.picker.preparing')}</>}
          {doc.status === 'Failed' && <><TriangleAlert className="size-3.5 text-rose-600" aria-hidden />{t('tutor.picker.failed')}</>}
          {doc.status === null && t('tutor.picker.notPrepared')}
        </p>
      </div>
      {ready ? (
        <button type="button" onClick={onStart} disabled={busy}
          className="btn-primary inline-flex h-9 shrink-0 items-center gap-1.5 rounded-lg px-3 text-sm font-semibold disabled:opacity-70">
          {busy && <Loader2 className="size-4 animate-spin" aria-hidden />}{resumable ? t('dashboard.continue') : t('dim.page.start')}
        </button>
      ) : !working && (
        <button type="button" onClick={onIndex} disabled={busy}
          className="inline-flex h-9 shrink-0 items-center gap-1.5 rounded-lg border border-slate-300 px-3 text-sm font-medium text-slate-700 hover:bg-slate-50 disabled:opacity-70">
          {busy && <Loader2 className="size-4 animate-spin" aria-hidden />}{doc.status === 'Failed' ? t('common.retry') : t('tutor.picker.prepare')}
        </button>
      )}
      {doc.isOwner && (
        // Shared documents belong to someone else: only the uploader can delete the file.
        <button type="button" onClick={onDelete} disabled={deleting || busy || doc.status === 'Processing'}
          aria-label={t('tutor.picker.deleteLabel', { name: doc.fileName })} title={t('tutor.picker.delete')}
          className="grid size-9 shrink-0 place-items-center rounded-lg text-slate-400 hover:bg-rose-50 hover:text-rose-600 disabled:opacity-50 disabled:hover:bg-transparent disabled:hover:text-slate-400">
          {deleting ? <Loader2 className="size-4 animate-spin" aria-hidden /> : <Trash2 className="size-4" aria-hidden />}
        </button>
      )}
    </li>
  )
}
