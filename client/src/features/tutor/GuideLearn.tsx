import { Check, Eye, Layers, Play, RotateCcw } from 'lucide-react'
import { useEffect, useState, type ReactNode } from 'react'
import { Alert } from '../../components/common/Alert'
import { cn } from '../../lib/utils'
import { useReviewCard, type CardDeck, type CardGrade, type LearnScope, type StudyCard } from './api'
import { FlipCard } from './FlipCard'
import { Frame } from './TutorShared'

interface Props {
  chatId: string
  deck: CardDeck
  scope: LearnScope
  onScope: (scope: LearnScope) => void
  onSource: (card: StudyCard) => void
}

/** Cards in one session; a forgotten card comes back after a few others until it is mastered. */
const sessionSize = 10
const comeBackAfter: Record<CardGrade, number> = { Again: 3, Hard: 4, Good: 6 }

const grades: { grade: CardGrade; label: string; key: string; tone: string }[] = [
  { grade: 'Again', label: 'Bilmirdim', key: '1', tone: 'border-rose-200 bg-rose-50 text-rose-800 hover:bg-rose-100' },
  { grade: 'Hard', label: 'Qismən', key: '2', tone: 'border-amber-200 bg-amber-50 text-amber-900 hover:bg-amber-100' },
  { grade: 'Good', label: 'Bilirdim', key: '3', tone: 'border-emerald-200 bg-emerald-50 text-emerald-800 hover:bg-emerald-100' },
]

function shuffle<T>(items: T[]) {
  const copy = [...items]
  for (let i = copy.length - 1; i > 0; i--) {
    const j = Math.floor(Math.random() * (i + 1));
    [copy[i], copy[j]] = [copy[j], copy[i]]
  }
  return copy
}

function inScope(deck: CardDeck, scope: LearnScope) {
  if (scope.kind === 'weak') return deck.cards.filter(c => c.weak)
  if (scope.kind === 'topic') return deck.cards.filter(c => c.topicKey === scope.key)
  return deck.cards
}

/** "Öyrən": flashcards the student grades themself. No dates: a card is done once it is mastered ("Bilirdim" in a row). */
export function GuideLearn({ chatId, deck, scope, onScope, onSource }: Props) {
  const [session, setSession] = useState<StudyCard[] | null>(null)
  const [result, setResult] = useState<{ mastered: number; total: number } | null>(null)
  const pool = inScope(deck, scope)
  const open = pool.filter(c => !c.mastered)
  const scopeKey = scope.kind === 'topic' ? `topic:${scope.key}` : scope.kind

  const start = (includeMastered: boolean) => {
    const source = includeMastered ? pool : open
    // Not seen and weak cards first in document order; Qarışıq mixes everything.
    const ordered = scope.kind === 'mixed' ? shuffle(source) : [...source.filter(c => c.weak || !c.seen), ...source.filter(c => !c.weak && c.seen)]
    setResult(null)
    setSession(ordered.slice(0, sessionSize))
  }

  if (session) {
    return (
      <Frame>
        <Session key={scopeKey} chatId={chatId} cards={session} required={deck.required} topics={new Map(deck.topics.map(t => [t.key, t.title]))} onSource={onSource}
          onFinish={summary => { setSession(null); setResult(summary) }} />
      </Frame>
    )
  }

  const topicTitle = scope.kind === 'topic' ? deck.topics.find(t => t.key === scope.key)?.title : undefined
  return (
    <Frame>
      <header className="flex items-start gap-3">
        <span className="grid size-10 shrink-0 place-items-center rounded-xl bg-indigo-50 text-indigo-700" aria-hidden><Layers className="size-5" /></span>
        <div className="min-w-0">
          <h2 className="text-base font-semibold">Öyrən</h2>
          <p className="mt-0.5 text-sm text-slate-600">Sualı oxu, cavabı yadına sal, sonra aç və özünü qiymətləndir. Bilmədiyin kart bir neçə kartdan sonra yenə gəlir.</p>
        </div>
      </header>

      <div role="radiogroup" aria-label="Nəyi öyrənək?" className="flex flex-wrap gap-2">
        <ScopeChip active={scope.kind === 'all'} onClick={() => onScope({ kind: 'all' })}>Hamısı</ScopeChip>
        <ScopeChip active={scope.kind === 'weak'} onClick={() => onScope({ kind: 'weak' })}>Zəif kartlar ({deck.cards.filter(c => c.weak).length})</ScopeChip>
        <ScopeChip active={scope.kind === 'mixed'} onClick={() => onScope({ kind: 'mixed' })}>Qarışıq</ScopeChip>
        <label className="sr-only" htmlFor="learn-topic">Mövzu</label>
        <select id="learn-topic" value={scope.kind === 'topic' ? scope.key : ''} onChange={e => e.target.value && onScope({ kind: 'topic', key: e.target.value })}
          className={cn('tutor-border h-9 max-w-full min-w-0 rounded-full border bg-transparent px-3 text-sm', scope.kind === 'topic' ? 'border-indigo-400 font-semibold text-indigo-700' : 'text-slate-700')}>
          <option value="">Mövzu seç…</option>
          {deck.topics.map(t => <option key={t.key} value={t.key}>{t.title}</option>)}
        </select>
      </div>

      {result && result.total > 0 && (
        <div className="tutor-enter rounded-2xl bg-emerald-50 px-4 py-3 text-sm text-emerald-900" role="status">
          <p className="font-semibold">Sessiya bitdi</p>
          <p className="mt-0.5">Bu sessiyada {result.mastered} / {result.total} kart mənimsənildi.</p>
        </div>
      )}

      <section className="tutor-border rounded-2xl border bg-white p-4 sm:p-5">
        <p className="text-sm font-semibold text-slate-900">{topicTitle ?? (scope.kind === 'weak' ? 'Zəif kartlar' : scope.kind === 'mixed' ? 'Qarışıq' : 'Bütün kartlar')}</p>
        <p className="mt-0.5 text-sm text-slate-600">
          {pool.length === 0 ? 'Burada kart yoxdur.' : `${pool.length - open.length} / ${pool.length} mənimsənilib · ${open.length} kart qalıb`}
        </p>
        {open.length > 0 ? (
          <button type="button" onClick={() => start(false)}
            className="btn-primary mt-4 inline-flex h-11 w-full items-center justify-center gap-2 rounded-xl text-sm font-semibold">
            <Play className="size-4" aria-hidden />Başla — {Math.min(sessionSize, open.length)} kart
          </button>
        ) : pool.length > 0 && (
          <div className="mt-4">
            <p className="flex items-center gap-2 text-sm font-semibold text-emerald-700"><Check className="size-4" aria-hidden />Hamısı mənimsənilib</p>
            <button type="button" onClick={() => start(true)} className="tutor-shared-action mt-3 w-full">
              <RotateCcw className="size-4" aria-hidden />Yenə də təkrarla
            </button>
          </div>
        )}
      </section>
    </Frame>
  )
}

function ScopeChip({ active, onClick, children }: { active: boolean; onClick: () => void; children: ReactNode }) {
  return (
    <button type="button" role="radio" aria-checked={active} onClick={onClick}
      className={cn('tutor-border h-9 rounded-full border px-3 text-sm', active ? 'border-indigo-400 bg-indigo-50 font-semibold text-indigo-700' : 'text-slate-700 hover:bg-slate-50')}>
      {children}
    </button>
  )
}

interface SessionProps {
  chatId: string; cards: StudyCard[]; required: number; topics: Map<string, string>
  onSource: (card: StudyCard) => void
  onFinish: (summary: { mastered: number; total: number }) => void
}

function Session({ chatId, cards, required, topics, onSource, onFinish }: SessionProps) {
  const review = useReviewCard(chatId)
  const [queue, setQueue] = useState(cards)
  const [masteredCount, setMasteredCount] = useState(0)
  const [flipped, setFlipped] = useState(false)
  const total = cards.length
  const card = queue[0]

  const grade = (value: CardGrade) => {
    if (!card || review.isPending) return
    review.mutate({ id: card.id, grade: value }, {
      onSuccess: state => {
        const rest = queue.slice(1)
        let next = rest
        let done = masteredCount
        if (state.mastered) done += 1
        else {
          // Not mastered yet: back into the queue a few cards later (or at the end of a short queue).
          next = [...rest]
          next.splice(Math.min(comeBackAfter[value], rest.length), 0, { ...card, streak: state.streak, mastered: false, weak: state.weak, seen: true })
        }
        setMasteredCount(done)
        setFlipped(false)
        if (next.length === 0) onFinish({ mastered: done, total })
        else setQueue(next)
      },
    })
  }

  // Space flips (when the card itself is not focused, it handles its own keys); 1–3 grade once the back is shown.
  useEffect(() => {
    const onKey = (event: KeyboardEvent) => {
      const target = event.target as HTMLElement | null
      if (target?.closest('textarea, input, select')) return
      if (event.key === ' ') {
        if (target?.closest('button, a, [role=button]')) return // the focused control handles its own Space
        event.preventDefault()
        setFlipped(f => !f)
      } else if (flipped) {
        const match = grades.find(g => g.key === event.key)
        if (match) grade(match.grade)
      }
    }
    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
  })

  if (!card) return null
  return (
    <>
      <div className="flex items-center justify-between gap-3 text-sm">
        <button type="button" onClick={() => onFinish({ mastered: masteredCount, total })} className="font-semibold text-slate-600 hover:text-slate-900">Dayandır</button>
        <span className="text-slate-500" aria-live="polite">{masteredCount} / {total} mənimsənildi</span>
      </div>
      <div className="h-1.5 overflow-hidden rounded-full bg-slate-100" aria-hidden>
        <div className="h-full rounded-full bg-emerald-500 transition-[width]" style={{ width: `${(masteredCount / total) * 100}%` }} />
      </div>

      <div key={`${card.id}-${queue.length}`} className="tutor-enter">
        <FlipCard card={card} topic={topics.get(card.topicKey) ?? ''} flipped={flipped} onFlip={() => setFlipped(f => !f)}
          onSource={onSource} required={required} />
      </div>

      {flipped ? (
        <div>
          <p className="text-center text-sm font-medium text-slate-700">Nə qədər bildin?</p>
          <div className="mt-2 grid grid-cols-3 gap-2">
            {grades.map(g => (
              <button key={g.grade} type="button" onClick={() => grade(g.grade)} disabled={review.isPending}
                className={cn('min-h-12 rounded-xl border px-2 py-1.5 text-sm font-semibold disabled:opacity-60', g.tone)}>
                {g.label}<span className="ml-1 hidden text-[11px] font-normal opacity-70 sm:inline">({g.key})</span>
              </button>
            ))}
          </div>
        </div>
      ) : (
        <button type="button" onClick={() => setFlipped(true)}
          className="btn-primary inline-flex h-11 w-full items-center justify-center gap-2 rounded-xl text-sm font-semibold">
          <Eye className="size-4" aria-hidden />Cavabı göstər
        </button>
      )}
      <p className="hidden text-center text-[11px] text-slate-400 sm:block">Boşluq — kartı çevir · 1 / 2 / 3 — qiymət</p>
      {review.isError && <Alert title="Qiymət saxlanmadı">{review.error.message}</Alert>}
    </>
  )
}
