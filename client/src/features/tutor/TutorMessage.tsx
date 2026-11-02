import { Check, Copy, FileText, RotateCcw, TriangleAlert } from 'lucide-react'
import { useState, type ReactNode } from 'react'
import { cn } from '../../lib/utils'
import type { ChatSource } from './api'

export interface ViewMessage {
  key: string
  role: 'User' | 'Assistant'
  content: string
  status: 'Completed' | 'Cancelled' | 'Failed' | 'Streaming'
  grounded: boolean | null
  sources: ChatSource[]
  error?: string
}

interface Props {
  message: ViewMessage
  onOpenSource: (source: ChatSource) => void
  onRetry?: () => void
}

export function TutorMessage({ message, onOpenSource, onRetry }: Props) {
  if (message.role === 'User') {
    return (
      <div className="tutor-enter flex justify-end">
        <p className="max-w-[85%] whitespace-pre-wrap rounded-2xl rounded-br-md bg-indigo-50 px-4 py-2.5 text-[15px] leading-relaxed text-slate-900 sm:max-w-[75%]">
          {message.content}
        </p>
      </div>
    )
  }

  const streaming = message.status === 'Streaming'
  const sources = message.sources
  const byNumber = new Map(sources.map(s => [s.number, s]))
  return (
    <article className="tutor-enter group flex gap-3" aria-busy={streaming}>
      <span className="app-brand-mark mt-0.5 grid size-7 shrink-0 place-items-center rounded-lg" aria-hidden>
        <svg viewBox="0 0 16 16" className="size-3.5"><path d="M4 4h8M4 8h5.5M4 12h8" stroke="currentColor" strokeWidth="2" strokeLinecap="round" /></svg>
      </span>
      <div className="min-w-0 flex-1">
        {streaming && !message.content ? (
          <span className="tutor-dots inline-flex h-7 items-center gap-1 text-slate-400" role="status" aria-label="Cavab hazırlanır"><span /><span /><span /></span>
        ) : (
          <div className={cn('tutor-prose text-[15px] leading-relaxed text-slate-800', streaming && 'tutor-caret')}>
            {renderBlocks(message.content, number => { const s = byNumber.get(number); if (s) onOpenSource(s) })}
          </div>
        )}

        {message.status === 'Cancelled' && <p className="mt-2 text-xs text-slate-500">Cavab dayandırıldı.</p>}
        {(message.status === 'Failed' || message.error) && (
          <p className="mt-2 flex items-center gap-1.5 text-sm text-rose-700" role="alert">
            <TriangleAlert className="size-4 shrink-0" aria-hidden />{message.error ?? 'Cavab hazırlana bilmədi.'}
          </p>
        )}
        {message.grounded === false && !streaming && (
          <p className="mt-2 text-xs text-slate-500">Sənəddə uyğun mənbə tapılmadı — AI çağırılmadı.</p>
        )}

        {sources.length > 0 && !streaming && (
          <div className="mt-3 flex flex-wrap gap-1.5" aria-label="Mənbələr">
            {sources.map(source => (
              <button key={source.number} type="button" onClick={() => onOpenSource(source)}
                title={source.excerpt}
                className={cn('inline-flex max-w-full items-center gap-1.5 rounded-full border px-2.5 py-1 text-xs transition-colors focus-visible:outline-2 focus-visible:outline-indigo-600',
                  source.cited ? 'border-indigo-200 bg-indigo-50 text-indigo-800 hover:bg-indigo-100' : 'border-slate-200 text-slate-500 hover:bg-slate-50')}>
                <FileText className="size-3.5 shrink-0" aria-hidden />
                <span className="font-semibold">{source.number}</span>
                <span className="truncate">{pageLabel(source) ?? source.section ?? `Hissə ${source.chunkOrdinal}`}</span>
              </button>
            ))}
          </div>
        )}

        {!streaming && message.content && (
          <div className="mt-2 flex gap-1 opacity-100 transition-opacity sm:opacity-0 sm:group-hover:opacity-100 sm:group-focus-within:opacity-100">
            <CopyButton text={message.content} />
            {onRetry && (
              <button type="button" onClick={onRetry} className="inline-flex h-8 items-center gap-1.5 rounded-lg px-2 text-xs text-slate-500 hover:bg-slate-100 hover:text-slate-900">
                <RotateCcw className="size-3.5" aria-hidden />Yenidən
              </button>
            )}
          </div>
        )}
        {!streaming && !message.content && onRetry && (
          <button type="button" onClick={onRetry} className="mt-2 inline-flex h-8 items-center gap-1.5 rounded-lg border border-slate-200 px-3 text-xs font-medium text-slate-700 hover:bg-slate-50">
            <RotateCcw className="size-3.5" aria-hidden />Yenidən cəhd et
          </button>
        )}
      </div>
    </article>
  )
}

function CopyButton({ text }: { text: string }) {
  const [copied, setCopied] = useState(false)
  const copy = async () => {
    try {
      await navigator.clipboard.writeText(text)
      setCopied(true)
      setTimeout(() => setCopied(false), 1600)
    } catch { /* clipboard blocked: nothing useful to show */ }
  }
  return (
    <button type="button" onClick={copy} aria-label={copied ? 'Kopyalandı' : 'Cavabı kopyala'}
      className="inline-flex h-8 items-center gap-1.5 rounded-lg px-2 text-xs text-slate-500 hover:bg-slate-100 hover:text-slate-900">
      {copied ? <Check className="size-3.5 text-emerald-600" aria-hidden /> : <Copy className="size-3.5" aria-hidden />}
      {copied ? 'Kopyalandı' : 'Kopyala'}
    </button>
  )
}

function pageLabel(source: ChatSource) {
  if (source.pageFrom == null) return null
  return source.pageTo != null && source.pageTo !== source.pageFrom ? `səh. ${source.pageFrom}–${source.pageTo}` : `səh. ${source.pageFrom}`
}

// Small, safe subset of Markdown (paragraphs, lists, **bold**, `code`) — rendered as React nodes, never as HTML.
function renderBlocks(text: string, onCite: (n: number) => void): ReactNode[] {
  const blocks: ReactNode[] = []
  let list: { ordered: boolean; items: string[] } | null = null
  const flush = () => {
    if (!list) return
    const items = list.items.map((item, i) => <li key={i}>{renderInline(item, onCite)}</li>)
    blocks.push(list.ordered ? <ol key={blocks.length}>{items}</ol> : <ul key={blocks.length}>{items}</ul>)
    list = null
  }
  for (const raw of text.split('\n')) {
    const line = raw.trim()
    const bullet = /^[-*•]\s+(.*)$/.exec(line)
    const numbered = /^\d+[.)]\s+(.*)$/.exec(line)
    if (bullet || numbered) {
      const ordered = !!numbered
      if (list && list.ordered !== ordered) flush()
      list ??= { ordered, items: [] }
      list.items.push((bullet ?? numbered)![1])
      continue
    }
    flush()
    if (line) blocks.push(<p key={blocks.length}>{renderInline(line.replace(/^#{1,6}\s+/, ''), onCite)}</p>)
  }
  flush()
  return blocks
}

function renderInline(text: string, onCite: (n: number) => void): ReactNode[] {
  return text.split(/(\*\*[^*]+\*\*|`[^`]+`|\[\d{1,2}\])/g).filter(Boolean).map((part, i) => {
    if (part.startsWith('**') && part.endsWith('**') && part.length > 4) return <strong key={i} className="font-semibold text-slate-900">{part.slice(2, -2)}</strong>
    if (part.startsWith('`') && part.endsWith('`') && part.length > 2) return <code key={i}>{part.slice(1, -1)}</code>
    const cite = /^\[(\d{1,2})\]$/.exec(part)
    if (cite) {
      const n = Number(cite[1])
      return (
        <button key={i} type="button" onClick={() => onCite(n)} aria-label={`Mənbə ${n}`}
          className="mx-0.5 inline-grid h-[1.15rem] min-w-[1.15rem] place-items-center rounded-md bg-indigo-50 px-1 align-[0.1em] text-[11px] font-semibold text-indigo-700 hover:bg-indigo-100">
          {n}
        </button>
      )
    }
    return part
  })
}
