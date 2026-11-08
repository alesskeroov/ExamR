import { ListChecks, Search } from 'lucide-react'
import { useState } from 'react'
import { EmptyState } from '../../components/common/EmptyState'
import { cn } from '../../lib/utils'
import type { CardDeck, CardKind, StudyCard } from './api'
import { FlipCard } from './FlipCard'
import { Frame } from './TutorShared'

type Status = 'all' | 'new' | 'learning' | 'weak' | 'mastered'
const statuses: { id: Status; label: string }[] = [
  { id: 'all', label: 'Hamısı' },
  { id: 'new', label: 'Yeni' },
  { id: 'learning', label: 'Öyrənilir' },
  { id: 'weak', label: 'Zəif' },
  { id: 'mastered', label: 'Mənimsənilib' },
]
const kinds: { id: CardKind | 'all'; label: string }[] = [
  { id: 'all', label: 'Bütün növlər' },
  { id: 'Question', label: 'Suallar' },
  { id: 'Term', label: 'Anlayışlar' },
  { id: 'List', label: 'Siyahılar' },
  { id: 'Test', label: 'Testlər' },
]
/** Cards shown at once; "Daha çox" shows the next ones (large test documents have hundreds). */
const pageSize = 24

function statusOf(card: StudyCard): Exclude<Status, 'all'> {
  if (card.mastered) return 'mastered'
  if (card.weak) return 'weak'
  return card.seen ? 'learning' : 'new'
}

const fold = (text: string) => text.toLocaleLowerCase('az')

interface Props {
  deck: CardDeck
  onSource: (card: StudyCard) => void
}

/** "Kartlarım": every card as a flip card with its topic; search and filters. Cards are private to the student. */
export function GuideCardList({ deck, onSource }: Props) {
  const [term, setTerm] = useState('')
  const [topic, setTopic] = useState('')
  const [kind, setKind] = useState<CardKind | 'all'>('all')
  const [status, setStatus] = useState<Status>('all')
  const [flipped, setFlipped] = useState<string | null>(null)
  const [shownCount, setShownCount] = useState(pageSize)

  const query = fold(term.trim())
  const shown = deck.cards.filter(c =>
    (!topic || c.topicKey === topic)
    && (kind === 'all' || c.kind === kind)
    && (status === 'all' || statusOf(c) === status)
    && (!query || fold(c.front).includes(query) || fold(c.back).includes(query) || c.items.some(i => fold(i).includes(query))))
  const topicTitle = new Map(deck.topics.map(t => [t.key, t.title]))
  const presentKinds = kinds.filter(k => k.id === 'all' || deck.cards.some(c => c.kind === k.id))
  const filter = <T,>(set: (value: T) => void) => (value: T) => { set(value); setShownCount(pageSize); setFlipped(null) }

  return (
    <Frame wide>
      <header className="flex items-start gap-3">
        <span className="grid size-10 shrink-0 place-items-center rounded-xl bg-indigo-50 text-indigo-700" aria-hidden><ListChecks className="size-5" /></span>
        <div className="min-w-0">
          <h2 className="text-base font-semibold">Kartlarım</h2>
          <p className="mt-0.5 text-sm text-slate-600">Karta toxun — çevrilsin və cavabı görünsün. Kartlar yalnız sənə görünür.</p>
        </div>
      </header>

      <div className="flex flex-col gap-2 sm:flex-row">
        <label className="tutor-border flex h-10 min-w-0 flex-1 items-center gap-2 rounded-xl border px-3">
          <Search className="size-4 shrink-0 text-slate-400" aria-hidden />
          <span className="sr-only">Kartlarda axtar</span>
          <input type="search" value={term} onChange={e => filter(setTerm)(e.target.value)} placeholder="Kartlarda axtar…"
            className="min-w-0 flex-1 bg-transparent text-sm text-slate-900 outline-none placeholder:text-slate-400" />
        </label>
        <label className="sr-only" htmlFor="cards-topic">Mövzu</label>
        <select id="cards-topic" value={topic} onChange={e => filter(setTopic)(e.target.value)}
          className="tutor-border h-10 min-w-0 rounded-xl border bg-transparent px-3 text-sm text-slate-700 sm:max-w-64">
          <option value="">Bütün mövzular</option>
          {deck.topics.map(t => <option key={t.key} value={t.key}>{t.title}</option>)}
        </select>
        {presentKinds.length > 2 && (
          <>
            <label className="sr-only" htmlFor="cards-kind">Kart növü</label>
            <select id="cards-kind" value={kind} onChange={e => filter(setKind)(e.target.value as CardKind | 'all')}
              className="tutor-border h-10 min-w-0 rounded-xl border bg-transparent px-3 text-sm text-slate-700">
              {presentKinds.map(k => <option key={k.id} value={k.id}>{k.label}</option>)}
            </select>
          </>
        )}
      </div>

      <div role="radiogroup" aria-label="Vəziyyət" className="flex flex-wrap gap-1.5">
        {statuses.map(s => {
          const count = s.id === 'all' ? deck.cards.length : deck.cards.filter(c => statusOf(c) === s.id).length
          return (
            <button key={s.id} type="button" role="radio" aria-checked={status === s.id} onClick={() => filter(setStatus)(s.id)}
              className={cn('tutor-border h-8 rounded-full border px-3 text-xs', status === s.id ? 'border-indigo-400 bg-indigo-50 font-semibold text-indigo-700' : 'text-slate-600 hover:bg-slate-50')}>
              {s.label} ({count})
            </button>
          )
        })}
      </div>

      {shown.length === 0 ? (
        <EmptyState icon={<ListChecks className="size-8" aria-hidden />} title="Kart tapılmadı" description="Axtarışı və ya filtrləri dəyişin." />
      ) : (
        <>
          <p className="text-xs text-slate-500">{shown.length} kart</p>
          <ul className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
            {shown.slice(0, shownCount).map(card => (
              <li key={card.id}>
                <FlipCard card={card} topic={topicTitle.get(card.topicKey) ?? ''} size="small" required={deck.required}
                  flipped={flipped === card.id} onFlip={() => setFlipped(f => (f === card.id ? null : card.id))} onSource={onSource} />
              </li>
            ))}
          </ul>
          {shown.length > shownCount && (
            <button type="button" onClick={() => setShownCount(n => n + pageSize)} className="tutor-shared-action">
              Daha çox ({shown.length - shownCount})
            </button>
          )}
        </>
      )}
    </Frame>
  )
}
