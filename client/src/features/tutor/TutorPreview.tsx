import { ExternalLink, Loader2, X } from 'lucide-react'
import { useEffect, useRef, useState } from 'react'
import { cn } from '../../lib/utils'
import { useDocumentChunks, type ChatSource } from './api'
import { placeLabel } from './place-label'

interface Props {
  documentId: string
  fileName: string
  format: string
  source: ChatSource | null
  onClose: () => void
}

/** PDF: the browser's own viewer at the cited page. DOCX/TXT have no pages, so the indexed text parts are shown instead. */
export function TutorPreview({ documentId, fileName, format, source, onClose }: Props) {
  const isPdf = format === 'pdf'
  return (
    <section aria-label="Sənəd önizləməsi" className="tutor-surface flex h-full min-h-0 flex-col">
      <header className="tutor-border flex h-14 shrink-0 items-center gap-2 border-b px-4">
        <div className="min-w-0 flex-1">
          <p className="truncate text-sm font-semibold">{fileName}</p>
          <p className="text-xs text-slate-500">
            {source
              // A search hit has no source number: only its place is shown.
              ? [source.number > 0 ? `Mənbə ${source.number}` : null, placeLabel(format, source.pageFrom, source.pageTo)].filter(Boolean).join(' · ') || 'Önizləmə'
              : 'Önizləmə'}
          </p>
        </div>
        <button type="button" onClick={onClose} aria-label="Önizləməni bağla" className="grid size-9 place-items-center rounded-lg text-slate-500 hover:bg-slate-100 hover:text-slate-900">
          <X className="size-4" />
        </button>
      </header>
      {source?.excerpt && (
        <blockquote className="tutor-border mx-4 mt-3 shrink-0 rounded-lg border-l-4 border-indigo-400 bg-indigo-50/50 px-3 py-2 text-xs leading-relaxed text-slate-700">
          {source.excerpt}
        </blockquote>
      )}
      <div className="min-h-0 flex-1 p-3">
        {isPdf ? <PdfFrame key={documentId} documentId={documentId} page={source?.pageFrom ?? 1} /> : <ChunkList documentId={documentId} ordinal={source?.chunkOrdinal ?? null} />}
      </div>
    </section>
  )
}

function PdfFrame({ documentId, page }: { documentId: string; page: number }) {
  const [url, setUrl] = useState<string | null>(null)
  const [failed, setFailed] = useState(false)
  useEffect(() => {
    let revoked = false
    let objectUrl: string | null = null
    fetch(`/api/documents/${documentId}/download`, { credentials: 'same-origin' })
      .then(r => (r.ok ? r.blob() : Promise.reject(new Error(String(r.status)))))
      .then(blob => {
        if (revoked) return
        objectUrl = URL.createObjectURL(new Blob([blob], { type: 'application/pdf' }))
        setUrl(objectUrl)
      })
      .catch(() => { if (!revoked) setFailed(true) })
    return () => { revoked = true; if (objectUrl) URL.revokeObjectURL(objectUrl) }
  }, [documentId])

  if (failed) return <p className="p-4 text-sm text-rose-700" role="alert">Sənəd açıla bilmədi.</p>
  if (!url) return <div className="grid h-full place-items-center text-slate-400"><Loader2 className="size-5 animate-spin" aria-label="Yüklənir" /></div>
  return (
    <div className="flex h-full flex-col gap-2">
      {/* Remounted per page: built-in PDF viewers only honour #page on load. */}
      <iframe key={page} title="PDF önizləmə" src={`${url}#page=${page}`} className="tutor-border min-h-0 w-full flex-1 rounded-lg border bg-white" />
      <a href={`${url}#page=${page}`} target="_blank" rel="noreferrer" className="inline-flex items-center gap-1.5 self-end text-xs font-medium text-indigo-700 hover:underline">
        Yeni pəncərədə aç <ExternalLink className="size-3.5" aria-hidden />
      </a>
    </div>
  )
}

function ChunkList({ documentId, ordinal }: { documentId: string; ordinal: number | null }) {
  const { data, isLoading, isError } = useDocumentChunks(documentId, true)
  const active = useRef<HTMLLIElement | null>(null)
  useEffect(() => { active.current?.scrollIntoView({ block: 'start', behavior: 'smooth' }) }, [ordinal, data])

  if (isLoading) return <div className="grid h-full place-items-center text-slate-400"><Loader2 className="size-5 animate-spin" aria-label="Yüklənir" /></div>
  if (isError || !data) return <p className="p-4 text-sm text-rose-700" role="alert">Mətn yüklənə bilmədi.</p>
  return (
    <ol className="h-full space-y-2 overflow-y-auto pr-1">
      {data.items.map(chunk => (
        <li key={chunk.ordinal} ref={chunk.ordinal === ordinal ? active : undefined}
          className={cn('rounded-lg border px-3 py-2 text-[13px] leading-relaxed whitespace-pre-wrap',
            chunk.ordinal === ordinal ? 'border-indigo-300 bg-indigo-50/60 text-slate-900' : 'tutor-border text-slate-600')}>
          {chunk.section && <p className="mb-1 text-xs font-semibold text-slate-500">{chunk.section}</p>}
          {chunk.text}
        </li>
      ))}
    </ol>
  )
}
