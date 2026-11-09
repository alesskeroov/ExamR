import { ChevronLeft, ChevronRight, ListChecks, Plus, Search } from 'lucide-react'
import { useMemo, useRef, useState, type ReactNode } from 'react'
import { EmptyState } from '../../components/common/EmptyState'
import { cn } from '../../lib/utils'
import { topicStats, type CardDeck, type CardKind, type StudyCard } from './api'
import { CardEditor } from './CardEditor'
import { CardViewer } from './CardViewer'
import { KindAccent, KindBadge, KindPrompt, ReviewBadge } from './FlipCard'
import { TopicPicker } from './TopicPicker'
import { Frame } from './TutorShared'

type Status = 'all' | 'new' | 'learning' | 'weak' | 'mastered'
const statuses: { id: Status; label: string }[] = [
  { id: 'all', label: 'Hamısı' },
  { id: 'new', label: 'Yeni' },
  { id: 'learning', label: 'Öyrənilir' },
  { id: 'weak', label: 'Zəif' },
  { id: 'mastered', label: 'Mənimsənilib' },
]
/** Views next to the study status: cards to check, and cards the student hid (shown only here). */
type View = 'study' | 'review' | 'hidden'
const kinds: { id: CardKind | 'all'; label: string }[] = [
  { id: 'all', label: 'Bütün növlər' },
  { id: 'Question', label: 'Suallar' },
  { id: 'Term', label: 'Anlayışlar' },
  { id: 'List', label: 'Siyahılar' },
  { id: 'Table', label: 'Cədvəllər' },
  { id: 'Test', label: 'Testlər' },
  { id: 'Custom', label: 'Mənim kartlarım' },
]
const statusDot: Record<Exclude<Status, 'all'>, string> = {
  new: 'bg-slate-300', learning: 'bg-amber-400', weak: 'bg-rose-500', mastered: 'bg-emerald-500',
}
const pageSize = 12

function statusOf(card: StudyCard): Exclude<Status, 'all'> {
  if (card.mastered) return 'mastered'
  if (card.weak) return 'weak'
  return card.seen ? 'learning' : 'new'
}

const fold = (text: string) => text.toLocaleLowerCase('az')

interface Props {
  chatId: string
  deck: CardDeck
  onSource: (card: StudyCard) => void
}

/** "Kartlarım": every card as a tile; a tap opens it large to read and flip. Filters, search and pages. Cards are private. */
export function GuideCardList({ chatId, deck, onSource }: Props) {
  const [term, setTerm] = useState('')
  const [topic, setTopic] = useState('')
  const [kind, setKind] = useState<CardKind | 'all'>('all')
  const [status, setStatus] = useState<Status>('all')
  const [view, setView] = useState<View>('study')
  const [creating, setCreating] = useState(false)
  const [page, setPage] = useState(0)
  const [viewing, setViewing] = useState<number | null>(null)
  const top = useRef<HTMLDivElement>(null)
  const stats = useMemo(() => topicStats(deck), [deck])
  const topicTitle = useMemo(() => new Map(deck.topics.map(t => [t.key, t.title])), [deck.topics])

  const query = fold(term.trim())
  const inView = (c: StudyCard) => view === 'hidden' ? c.hidden : !c.hidden && (view === 'study' || c.needsReview)
  const visible = deck.cards.filter(inView)
  const shown = visible.filter(c =>
    (!topic || c.topicKey === topic)
    && (kind === 'all' || c.kind === kind)
    && (status === 'all' || statusOf(c) === status)
    && (!query || fold(c.front).includes(query) || fold(c.back).includes(query) || c.items.some(i => fold(i).includes(query))))
  const reviewCount = deck.cards.filter(c => !c.hidden && c.needsReview).length
  const hiddenCount = deck.cards.filter(c => c.hidden).length
  const pages = Math.max(1, Math.ceil(shown.length / pageSize))
  const current = Math.min(page, pages - 1)
  const pageCards = shown.slice(current * pageSize, current * pageSize + pageSize)
  const presentKinds = kinds.filter(k => k.id === 'all' || deck.cards.some(c => c.kind === k.id))
  const filter = <T,>(set: (value: T) => void) => (value: T) => { set(value); setPage(0) }
  const goPage = (next: number) => { setPage(next); top.current?.scrollIntoView({ block: 'start', behavior: 'smooth' }) }

  return (
    <Frame wide>
      <div ref={top} className="scroll-mt-4" />
      <header className="flex items-start gap-3">
        <span className="grid size-10 shrink-0 place-items-center rounded-xl bg-indigo-50 text-indigo-700" aria-hidden><ListChecks className="size-5" /></span>
        <div className="min-w-0">
          <h2 className="text-base font-semibold">Kartlarım</h2>
          <p className="mt-0.5 text-sm text-slate-600">Karta toxun — böyük açılsın, sonra çevir. Kartlar yalnız sənə görünür.</p>
        </div>
        <button type="button" onClick={() => setCreating(true)}
          className="btn-primary ml-auto inline-flex h-10 shrink-0 items-center gap-1.5 rounded-xl px-3 text-sm font-semibold sm:px-4">
          <Plus className="size-4" aria-hidden /><span className="sr-only sm:not-sr-only">Kart əlavə et</span>
        </button>
      </header>

      {(reviewCount > 0 || hiddenCount > 0 || view !== 'study') && (
        <div className="flex flex-wrap gap-1.5" role="group" aria-label="Görünüş">
          <Chip active={view === 'study'} onClick={() => filter(setView)('study')}>Öyrənilən kartlar</Chip>
          {(reviewCount > 0 || view === 'review') && <Chip active={view === 'review'} onClick={() => filter(setView)('review')}>Yoxlanmalı · {reviewCount}</Chip>}
          {(hiddenCount > 0 || view === 'hidden') && <Chip active={view === 'hidden'} onClick={() => filter(setView)('hidden')}>Gizlədilən · {hiddenCount}</Chip>}
        </div>
      )}
      {view === 'review' && (
        <p className="text-sm text-slate-600">Bu kartlar avtomatik çıxarılıb və səhv ola bilər. Kartı açın: düzəldin, gizlədin və ya silin.</p>
      )}

      <div className="grid gap-2 sm:grid-cols-[1fr_minmax(0,18rem)]">
        <label className="tutor-border flex h-10 min-w-0 items-center gap-2 rounded-xl border bg-white px-3">
          <Search className="size-4 shrink-0 text-slate-400" aria-hidden />
          <span className="sr-only">Kartlarda axtar</span>
          <input type="search" value={term} onChange={e => filter(setTerm)(e.target.value)} placeholder="Kartlarda axtar…"
            className="min-w-0 flex-1 bg-transparent text-base text-slate-900 outline-none placeholder:text-slate-400 sm:text-sm" />
        </label>
        <TopicPicker topics={deck.topics} stats={stats} value={topic} onChange={filter(setTopic)} />
      </div>

      <div className="-mx-4 flex gap-1.5 overflow-x-auto px-4 pb-1 sm:mx-0 sm:flex-wrap sm:px-0" role="group" aria-label="Filtrlər">
        {presentKinds.length > 2 && presentKinds.map(k => (
          <Chip key={k.id} active={kind === k.id} onClick={() => filter(setKind)(k.id)}>{k.label}</Chip>
        ))}
        {presentKinds.length > 2 && <span className="tutor-border mx-1 w-px shrink-0 self-stretch border-l" aria-hidden />}
        {statuses.map(s => {
          const count = s.id === 'all' ? visible.length : visible.filter(c => statusOf(c) === s.id).length
          return <Chip key={s.id} active={status === s.id} onClick={() => filter(setStatus)(s.id)}>{s.label} · {count}</Chip>
        })}
      </div>

      {shown.length === 0 ? (
        <EmptyState icon={<ListChecks className="size-8" aria-hidden />} title="Kart tapılmadı" description="Axtarışı və ya filtrləri dəyişin." />
      ) : (
        <>
          <p className="text-xs text-slate-500">{shown.length} kart{pages > 1 ? ` · səhifə ${current + 1} / ${pages}` : ''}</p>
          <ul className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
            {pageCards.map((card, i) => (
              <li key={card.id}>
                <CardTile card={card} topic={topicTitle.get(card.topicKey) ?? ''} onOpen={() => setViewing(current * pageSize + i)} />
              </li>
            ))}
          </ul>
          {pages > 1 && <Pager page={current} pages={pages} onPage={goPage} />}
        </>
      )}

      {viewing != null && (
        <CardViewer chatId={chatId} deck={deck} cards={shown} index={Math.min(viewing, shown.length - 1)} required={deck.required} topicTitle={key => topicTitle.get(key) ?? ''}
          onIndex={next => { setViewing(next); setPage(Math.floor(next / pageSize)) }} onClose={() => setViewing(null)} onSource={onSource} />
      )}
      {creating && <CardEditor chatId={chatId} deck={deck} topicKey={topic} onClose={() => setCreating(false)} />}
    </Frame>
  )
}

function CardTile({ card, topic, onOpen }: { card: StudyCard; topic: string; onOpen: () => void }) {
  const state = statusOf(card)
  return (
    <button type="button" onClick={onOpen}
      className="tutor-border relative flex h-full min-h-44 w-full flex-col overflow-hidden rounded-2xl border bg-white p-4 pt-5 text-left shadow-sm transition hover:-translate-y-0.5 hover:shadow-md focus-visible:ring-2 focus-visible:ring-indigo-400 focus-visible:outline-none">
      <KindAccent card={card} />
      <div className="flex items-start gap-2">
        <p className="line-clamp-1 min-w-0 flex-1 text-xs font-semibold text-slate-500" title={card.section ? `${topic} › ${card.section}` : topic}>{card.section ?? topic}</p>
        <KindBadge card={card} />
      </div>
      <div className="mt-1.5 flex justify-end empty:hidden"><ReviewBadge card={card} /></div>
      <p className="mt-3 text-[11px] font-semibold tracking-[.1em] text-slate-400 uppercase"><KindPrompt card={card} /></p>
      <p className="mt-1 line-clamp-4 text-[15px] leading-snug font-semibold text-slate-900">{card.front}</p>
      <span className="mt-auto flex items-center gap-1.5 pt-3 text-xs text-slate-500">
        <span className={cn('size-2 rounded-full', statusDot[state])} aria-hidden />
        {statuses.find(s => s.id === state)!.label}
      </span>
    </button>
  )
}

function Chip({ active, onClick, children }: { active: boolean; onClick: () => void; children: ReactNode }) {
  return (
    <button type="button" aria-pressed={active} onClick={onClick}
      className={cn('tutor-border h-9 shrink-0 rounded-full border px-3 text-[13px] whitespace-nowrap',
        active ? 'border-indigo-400 bg-indigo-50 font-semibold text-indigo-700' : 'bg-white text-slate-600 hover:bg-slate-50')}>
      {children}
    </button>
  )
}

/** Page numbers around the current page, with the first and last always reachable. */
function Pager({ page, pages, onPage }: { page: number; pages: number; onPage: (page: number) => void }) {
  const numbers = [...new Set([0, page - 1, page, page + 1, pages - 1])].filter(n => n >= 0 && n < pages).sort((a, b) => a - b)
  return (
    <nav aria-label="Səhifələr" className="flex items-center justify-center gap-1.5">
      <button type="button" onClick={() => onPage(page - 1)} disabled={page === 0} aria-label="Əvvəlki səhifə"
        className="tutor-border grid size-10 place-items-center rounded-xl border bg-white text-slate-600 hover:bg-slate-50 disabled:opacity-40">
        <ChevronLeft className="size-4" />
      </button>
      {numbers.map((n, i) => (
        <span key={n} className="flex items-center gap-1.5">
          {i > 0 && n - numbers[i - 1] > 1 && <span className="px-0.5 text-slate-400" aria-hidden>…</span>}
          <button type="button" onClick={() => onPage(n)} aria-current={n === page ? 'page' : undefined}
            className={cn('tutor-border grid h-10 min-w-10 place-items-center rounded-xl border px-2 text-sm tabular-nums',
              n === page ? 'border-indigo-400 bg-indigo-50 font-semibold text-indigo-700' : 'bg-white text-slate-700 hover:bg-slate-50')}>
            {n + 1}
          </button>
        </span>
      ))}
      <button type="button" onClick={() => onPage(page + 1)} disabled={page === pages - 1} aria-label="Növbəti səhifə"
        className="tutor-border grid size-10 place-items-center rounded-xl border bg-white text-slate-600 hover:bg-slate-50 disabled:opacity-40">
        <ChevronRight className="size-4" />
      </button>
    </nav>
  )
}
