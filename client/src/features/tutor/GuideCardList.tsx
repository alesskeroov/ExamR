import { ChevronDown, ListChecks, Search } from 'lucide-react'
import { useState } from 'react'
import { EmptyState } from '../../components/common/EmptyState'
import { cn } from '../../lib/utils'
import type { CardDeck, StudyCard } from './api'
import { Frame } from './TutorShared'

type Status = 'all' | 'new' | 'learning' | 'weak' | 'mastered'
const statuses: { id: Status; label: string }[] = [
  { id: 'all', label: 'Hamısı' },
  { id: 'new', label: 'Yeni' },
  { id: 'learning', label: 'Öyrənilir' },
  { id: 'weak', label: 'Zəif' },
  { id: 'mastered', label: 'Mənimsənilib' },
]

function statusOf(card: StudyCard): Exclude<Status, 'all'> {
  if (card.mastered) return 'mastered'
  if (card.weak) return 'weak'
  return card.seen ? 'learning' : 'new'
}

const badge: Record<Exclude<Status, 'all'>, string> = {
  new: 'bg-indigo-50 text-indigo-700',
  learning: 'bg-amber-50 text-amber-800',
  weak: 'bg-rose-50 text-rose-700',
  mastered: 'bg-emerald-50 text-emerald-700',
}

const fold = (text: string) => text.toLocaleLowerCase('az')

interface Props {
  deck: CardDeck
  onSource: (card: StudyCard) => void
}

/** "Kartlarım": every card of the document with its status; search and filters, answer on demand, jump to the source page. */
export function GuideCardList({ deck, onSource }: Props) {
  const [term, setTerm] = useState('')
  const [topic, setTopic] = useState('')
  const [status, setStatus] = useState<Status>('all')
  const [openCard, setOpenCard] = useState<number | null>(null)

  const query = fold(term.trim())
  const shown = deck.cards.filter(c =>
    (!topic || c.topicKey === topic)
    && (status === 'all' || statusOf(c) === status)
    && (!query || fold(c.question).includes(query) || fold(c.answer).includes(query)))
  const topicTitle = new Map(deck.topics.map(t => [t.key, t.title]))

  return (
    <Frame>
      <header className="flex items-start gap-3">
        <span className="grid size-10 shrink-0 place-items-center rounded-xl bg-indigo-50 text-indigo-700" aria-hidden><ListChecks className="size-5" /></span>
        <div className="min-w-0">
          <h2 className="text-base font-semibold">Kartlarım</h2>
          <p className="mt-0.5 text-sm text-slate-600">Bütün kartlar və vəziyyətləri. Kartlar yalnız sənə görünür.</p>
        </div>
      </header>

      <div className="flex flex-col gap-2 sm:flex-row">
        <label className="tutor-border flex h-10 min-w-0 flex-1 items-center gap-2 rounded-xl border px-3">
          <Search className="size-4 shrink-0 text-slate-400" aria-hidden />
          <span className="sr-only">Kartlarda axtar</span>
          <input type="search" value={term} onChange={e => setTerm(e.target.value)} placeholder="Sual və ya cavabda axtar…"
            className="min-w-0 flex-1 bg-transparent text-sm text-slate-900 outline-none placeholder:text-slate-400" />
        </label>
        <label className="sr-only" htmlFor="cards-topic">Mövzu</label>
        <select id="cards-topic" value={topic} onChange={e => setTopic(e.target.value)}
          className="tutor-border h-10 min-w-0 rounded-xl border bg-transparent px-3 text-sm text-slate-700 sm:max-w-56">
          <option value="">Bütün mövzular</option>
          {deck.topics.map(t => <option key={t.key} value={t.key}>{t.title}</option>)}
        </select>
      </div>

      <div role="radiogroup" aria-label="Vəziyyət" className="flex flex-wrap gap-1.5">
        {statuses.map(s => {
          const count = s.id === 'all' ? deck.cards.length : deck.cards.filter(c => statusOf(c) === s.id).length
          return (
            <button key={s.id} type="button" role="radio" aria-checked={status === s.id} onClick={() => setStatus(s.id)}
              className={cn('tutor-border h-8 rounded-full border px-3 text-xs', status === s.id ? 'border-indigo-400 bg-indigo-50 font-semibold text-indigo-700' : 'text-slate-600 hover:bg-slate-50')}>
              {s.label} ({count})
            </button>
          )
        })}
      </div>

      {shown.length === 0 ? (
        <EmptyState icon={<ListChecks className="size-8" aria-hidden />} title="Kart tapılmadı" description="Axtarışı və ya filtrləri dəyişin." />
      ) : (
        <ul className="flex flex-col gap-2">
          {shown.map(card => {
            const state = statusOf(card)
            const expanded = openCard === card.number
            return (
              <li key={card.number} className="tutor-border rounded-2xl border bg-white">
                <button type="button" onClick={() => setOpenCard(expanded ? null : card.number)} aria-expanded={expanded}
                  className="flex w-full items-start gap-3 p-4 text-left">
                  <div className="min-w-0 flex-1">
                    <div className="flex flex-wrap items-center gap-2 text-xs text-slate-500">
                      <span className="font-semibold text-slate-700">Sual {card.number}</span>
                      <span className="truncate">{topicTitle.get(card.topicKey)}</span>
                    </div>
                    <p className="mt-1 text-sm font-medium leading-snug text-slate-900">{card.question}</p>
                  </div>
                  <span className={cn('shrink-0 rounded-full px-2 py-0.5 text-[11px] font-semibold', badge[state])}>
                    {statuses.find(s => s.id === state)!.label}
                  </span>
                  <ChevronDown className={cn('mt-0.5 size-4 shrink-0 text-slate-400 transition-transform', expanded && 'rotate-180')} aria-hidden />
                </button>
                {expanded && (
                  <div className="tutor-border border-t px-4 pt-3 pb-4">
                    <p className="text-sm leading-relaxed whitespace-pre-line text-slate-800">{card.answer}</p>
                    <button type="button" onClick={() => onSource(card)} className="mt-2 text-xs font-semibold text-indigo-700 hover:underline">
                      Sənəddə göstər{card.page != null ? ` · səh. ${card.page}` : ''}
                    </button>
                  </div>
                )}
              </li>
            )
          })}
        </ul>
      )}
    </Frame>
  )
}
