import { BookOpen, ChevronDown } from 'lucide-react'
import { cn } from '../../lib/utils'

interface PassageBoxProps {
  passage: { title: string | null; text: string }
  /** Open on first render; the reader can fold it away. */
  defaultOpen?: boolean
  className?: string
}

/** A reading passage shared by several questions, shown above the question. Paragraphs are separated by blank lines. */
export function PassageBox({ passage, defaultOpen = true, className }: PassageBoxProps) {
  const paragraphs = passage.text.split(/\n\s*\n/).map((p) => p.trim()).filter(Boolean)

  return (
    <details open={defaultOpen} className={cn('group rounded-lg border border-slate-200 bg-slate-50', className)}>
      <summary className="flex min-h-10 cursor-pointer list-none items-center gap-2 rounded-lg px-3 py-2 text-sm font-medium text-slate-700 hover:bg-slate-100 [&::-webkit-details-marker]:hidden">
        <BookOpen className="size-4 shrink-0" aria-hidden />
        <span className="min-w-0 flex-1">{passage.title || 'Mətn'}</span>
        <ChevronDown className="size-4 shrink-0 transition-transform group-open:rotate-180" aria-hidden />
      </summary>
      <div className="exam-question-text max-h-[45vh] space-y-3 overflow-y-auto border-t border-slate-200 px-3 py-3 text-[15px] leading-7 text-slate-800 sm:px-4">
        {paragraphs.map((paragraph, i) => (
          <p key={i} className="whitespace-pre-line">{paragraph}</p>
        ))}
      </div>
    </details>
  )
}
