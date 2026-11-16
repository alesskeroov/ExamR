import { Check, Eye, Layers, Play, RotateCcw } from 'lucide-react'
import { useEffect, useState, type ReactNode } from 'react'
import { Alert } from '../../components/common/Alert'
import { cn } from '../../lib/utils'
import { studyCards, topicStats, useReviewCard, type CardDeck, type CardGrade, type LearnScope, type StudyCard } from './api'
import { FlipCard } from './FlipCard'
import { TopicPicker } from './TopicPicker'
import { Frame } from './TutorShared'
import { useTranslation } from 'react-i18next'

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
  { grade: 'Again', label: 'tutor.grade.again', key: '1', tone: 'border-rose-200 bg-rose-50 text-rose-800 hover:bg-rose-100' },
  { grade: 'Hard', label: 'tutor.grade.hard', key: '2', tone: 'border-amber-200 bg-amber-50 text-amber-900 hover:bg-amber-100' },
  { grade: 'Good', label: 'tutor.grade.good', key: '3', tone: 'border-emerald-200 bg-emerald-50 text-emerald-800 hover:bg-emerald-100' },
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
  const cards = studyCards(deck)
  if (scope.kind === 'due') return cards.filter(c => c.due)
  if (scope.kind === 'weak') return cards.filter(c => c.weak)
  if (scope.kind === 'topic') return cards.filter(c => c.topicKey === scope.key)
  return cards
}

/** "Öyrən": flashcards the student grades themself. Graded cards come back on their review day ("Bu gün"); a card is mastered after "Bilirdim" in a row. */
export function GuideLearn({ chatId, deck, scope, onScope, onSource }: Props) {
  const { t } = useTranslation()
  const [session, setSession] = useState<StudyCard[] | null>(null)
  const [result, setResult] = useState<{ mastered: number; total: number } | null>(null)
  const pool = inScope(deck, scope)
  // A due card is reviewed even when it was mastered: that review is what keeps it mastered.
  const open = scope.kind === 'due' ? pool : pool.filter(c => !c.mastered)
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
  const stats = topicStats(deck)
  return (
    <Frame>
      <header className="flex items-start gap-3">
        <span className="grid size-10 shrink-0 place-items-center rounded-xl bg-indigo-50 text-indigo-700" aria-hidden><Layers className="size-5" /></span>
        <div className="min-w-0">
          <h2 className="text-base font-semibold">{t('tutor.learn.title')}</h2>
          <p className="mt-0.5 text-sm text-slate-600">{t('tutor.learn.text')}</p>
        </div>
      </header>

      <div className="flex flex-col gap-2">
        <div role="radiogroup" aria-label={t('tutor.learn.scopeAria')} className="grid grid-cols-2 gap-2 sm:grid-cols-4">
          <ScopeChip active={scope.kind === 'all'} onClick={() => onScope({ kind: 'all' })}>{t('exam.setup.all')}</ScopeChip>
          <ScopeChip active={scope.kind === 'due'} onClick={() => onScope({ kind: 'due' })}>{t('tutor.learn.today')} · {studyCards(deck).filter(c => c.due).length}</ScopeChip>
          <ScopeChip active={scope.kind === 'weak'} onClick={() => onScope({ kind: 'weak' })}>{t('tutor.status.weak')} · {studyCards(deck).filter(c => c.weak).length}</ScopeChip>
          <ScopeChip active={scope.kind === 'mixed'} onClick={() => onScope({ kind: 'mixed' })}>{t('tutor.topics.mixed')}</ScopeChip>
        </div>
        <TopicPicker topics={deck.topics} stats={stats} value={scope.kind === 'topic' ? scope.key : ''} allLabel={t('tutor.learn.pickTopic')}
          onChange={key => onScope(key ? { kind: 'topic', key } : { kind: 'all' })}
          className={cn('h-11', scope.kind === 'topic' && 'border-indigo-400')} />
      </div>

      {result && result.total > 0 && (
        <div className="tutor-enter rounded-2xl bg-emerald-50 px-4 py-3 text-sm text-emerald-900" role="status">
          <p className="font-semibold">{t('tutor.learn.sessionDone')}</p>
          <p className="mt-0.5">{t('tutor.learn.sessionResult', { mastered: result.mastered, total: result.total })}</p>
        </div>
      )}

      <section className="tutor-border rounded-2xl border bg-white p-4 sm:p-5">
        <p className="text-sm font-semibold text-slate-900">{topicTitle ?? (scope.kind === 'due' ? t('tutor.learn.todayReview') : scope.kind === 'weak' ? t('tutor.topics.weakCards') : scope.kind === 'mixed' ? t('tutor.topics.mixed') : t('tutor.learn.allCards'))}</p>
        <p className="mt-0.5 text-sm text-slate-600">
          {scope.kind === 'due'
            ? (pool.length === 0 ? t('tutor.learn.noDue') : t('tutor.learn.due', { n: pool.length }))
            : pool.length === 0 ? t('tutor.learn.empty') : t('tutor.learn.progress', { done: pool.length - open.length, total: pool.length, left: open.length })}
        </p>
        {open.length > 0 ? (
          <button type="button" onClick={() => start(false)}
            className="btn-primary mt-4 inline-flex h-11 w-full items-center justify-center gap-2 rounded-xl text-sm font-semibold">
            <Play className="size-4" aria-hidden />{t('tutor.learn.start', { n: Math.min(sessionSize, open.length) })}
          </button>
        ) : pool.length > 0 && (
          <div className="mt-4">
            <p className="flex items-center gap-2 text-sm font-semibold text-emerald-700"><Check className="size-4" aria-hidden />{t('tutor.learn.allMastered')}</p>
            <button type="button" onClick={() => start(true)} className="tutor-shared-action mt-3 w-full">
              <RotateCcw className="size-4" aria-hidden />{t('tutor.learn.repeatAgain')}
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
      className={cn('tutor-border h-11 min-w-0 truncate rounded-xl border px-2 text-sm', active ? 'border-indigo-400 bg-indigo-50 font-semibold text-indigo-700' : 'bg-white text-slate-700 hover:bg-slate-50')}>
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
  const { t } = useTranslation()
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
        <button type="button" onClick={() => onFinish({ mastered: masteredCount, total })} className="font-semibold text-slate-600 hover:text-slate-900">{t('tutor.learn.stop')}</button>
        <span className="text-slate-500" aria-live="polite">{t('tutor.learn.masteredOf', { done: masteredCount, total })}</span>
      </div>
      <div className="h-1.5 overflow-hidden rounded-full bg-slate-100" aria-hidden>
        <div className="h-full rounded-full bg-emerald-500 transition-[width]" style={{ width: `${(masteredCount / total) * 100}%` }} />
      </div>

      <div key={`${card.id}-${queue.length}`} className="tutor-enter h-[52dvh] min-h-80 sm:h-[min(60dvh,34rem)]">
        <FlipCard card={card} topic={topics.get(card.topicKey) ?? ''} flipped={flipped} onFlip={() => setFlipped(f => !f)} autoFocus
          onSource={onSource} required={required} />
      </div>

      {/* Stays at the bottom of the screen on phones, so a long answer never pushes the grades out of reach. */}
      <div className="tutor-surface sticky bottom-0 z-10 -mx-4 px-4 pt-2 pb-[max(.75rem,env(safe-area-inset-bottom))] sm:static sm:mx-0 sm:p-0">
      {flipped ? (
        <div>
          <p className="text-center text-sm font-medium text-slate-700">{t('tutor.learn.howWell')}</p>
          <div className="mt-2 grid grid-cols-3 gap-2">
            {grades.map(g => (
              <button key={g.grade} type="button" onClick={() => grade(g.grade)} disabled={review.isPending}
                className={cn('min-h-12 rounded-xl border px-2 py-1.5 text-[15px] font-semibold disabled:opacity-60', g.tone)}>
                {t(g.label)}<span className="ml-1 hidden text-[11px] font-normal opacity-70 sm:inline">({g.key})</span>
              </button>
            ))}
          </div>
        </div>
      ) : (
        <button type="button" onClick={() => setFlipped(true)}
          className="btn-primary inline-flex h-12 w-full items-center justify-center gap-2 rounded-xl text-[15px] font-semibold">
          <Eye className="size-4" aria-hidden />{t('exam.session.showAnswer')}
        </button>
      )}
      </div>
      <p className="hidden text-center text-[11px] text-slate-400 sm:block">{t('tutor.learn.keyboard')}</p>
      {review.isError && <Alert title={t('tutor.learn.gradeFailed')}>{review.error.message}</Alert>}
    </>
  )
}
