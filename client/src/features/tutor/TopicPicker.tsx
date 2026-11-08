import { Check, ChevronDown, Search } from 'lucide-react'
import { useState } from 'react'
import { cn } from '../../lib/utils'
import type { CardTopic, TopicStats } from './api'
import { GuideDialog } from './GuideDialog'

interface Props {
  topics: CardTopic[]
  stats: Map<string, TopicStats>
  /** Selected topic key; '' = all topics. */
  value: string
  onChange: (key: string) => void
  allLabel?: string
  className?: string
}

const fold = (text: string) => text.toLocaleLowerCase('az')

/**
 * Topic chooser that stays readable with long names and in the dark theme (the native select popup is neither):
 * a button that opens a searchable list with each topic's progress.
 */
export function TopicPicker({ topics, stats, value, onChange, allLabel = 'Bütün mövzular', className }: Props) {
  const [open, setOpen] = useState(false)
  const [term, setTerm] = useState('')
  const current = topics.find(t => t.key === value)
  const query = fold(term.trim())
  const numbered = topics.map((topic, i) => ({ topic, number: i + 1 }))
  const shown = query ? numbered.filter(t => fold(t.topic.title).includes(query)) : numbered
  const close = () => { setOpen(false); setTerm('') }
  const pick = (key: string) => { onChange(key); close() }

  return (
    <>
      <button type="button" onClick={() => setOpen(true)} aria-haspopup="dialog"
        className={cn('tutor-border flex h-10 min-w-0 items-center gap-2 rounded-xl border bg-white px-3 text-left text-sm hover:bg-slate-50', className)}>
        <span className={cn('min-w-0 flex-1 truncate', current ? 'font-semibold text-slate-900' : 'text-slate-600')}>{current?.title ?? allLabel}</span>
        <ChevronDown className="size-4 shrink-0 text-slate-400" aria-hidden />
      </button>
      {open && (
        <GuideDialog title="Mövzu seç" onClose={close} tall={topics.length > 6}>
          {topics.length > 6 && (
            <div className="shrink-0 px-4 pt-3 sm:px-5">
              <label className="tutor-border flex h-11 items-center gap-2 rounded-xl border px-3">
                <Search className="size-4 shrink-0 text-slate-400" aria-hidden />
                <span className="sr-only">Mövzu axtar</span>
                {/* 16px text on phones so iOS does not zoom into the field. */}
                <input type="search" value={term} onChange={e => setTerm(e.target.value)} placeholder="Mövzu axtar…"
                  className="min-w-0 flex-1 bg-transparent text-base text-slate-900 outline-none placeholder:text-slate-400 sm:text-sm" />
              </label>
            </div>
          )}
          <ul role="listbox" aria-label="Mövzular" className="min-h-0 flex-1 overflow-y-auto overscroll-contain px-2 py-2 pb-[max(.5rem,env(safe-area-inset-bottom))] sm:px-3">
            {!query && <Option label={allLabel} selected={!value} onPick={() => pick('')} />}
            {shown.map(({ topic, number }) => {
              const s = stats.get(topic.key)
              return <Option key={topic.key} number={number} label={topic.title} selected={topic.key === value}
                done={s?.mastered ?? 0} total={s?.total ?? 0} onPick={() => pick(topic.key)} />
            })}
            {shown.length === 0 && <li className="px-3 py-8 text-center text-sm text-slate-500">Mövzu tapılmadı.</li>}
          </ul>
        </GuideDialog>
      )}
    </>
  )
}

function Option({ label, selected, onPick, number, done = 0, total = 0 }: {
  label: string; selected: boolean; onPick: () => void; number?: number; done?: number; total?: number
}) {
  return (
    <li role="option" aria-selected={selected}>
      <button type="button" onClick={onPick}
        className={cn('flex w-full items-start gap-3 rounded-xl px-3 py-3 text-left', selected ? 'bg-indigo-50' : 'hover:bg-slate-50')}>
        {number != null && <span className="mt-0.5 w-6 shrink-0 text-right text-xs font-semibold text-slate-400 tabular-nums">{number}</span>}
        <span className="min-w-0 flex-1">
          <span className={cn('block text-[15px] leading-snug', selected ? 'font-semibold text-indigo-700' : 'text-slate-800')}>{label}</span>
          {total > 0 && (
            <span className="mt-1.5 flex items-center gap-2">
              <span className="h-1.5 flex-1 overflow-hidden rounded-full bg-slate-100">
                <span className="block h-full rounded-full bg-emerald-500" style={{ width: `${(done / total) * 100}%` }} />
              </span>
              <span className="shrink-0 text-xs text-slate-500 tabular-nums">{done}/{total}</span>
            </span>
          )}
        </span>
        {selected && <Check className="mt-0.5 size-4 shrink-0 text-indigo-700" aria-hidden />}
      </button>
    </li>
  )
}
