import { ArrowLeftRight, Check, Lightbulb, RotateCcw, Timer } from 'lucide-react'
import { useState, type ReactNode } from 'react'
import { Alert } from '../../components/common/Alert'
import { cn } from '../../lib/utils'
import { useMoreRevision, useRevision, type RevisionTopic } from './api'
import { Skeleton } from './TutorShared'

const batch = 5

interface Props {
  chatId: string
  disabled: boolean
  /** Opens Məni yoxla on these questions (a weak topic's wrong ones, or the whole topic). */
  onQuiz: (title: string, numbers: number[]) => void
}

/** "Son təkrar": short cards per topic for a fast refresh before the exam; weak topics from Məni yoxla come first. */
export function TutorRevision({ chatId, disabled, onQuiz }: Props) {
  const revision = useRevision(chatId)
  const more = useMoreRevision(chatId)
  // Topics already tried in this view: one that could not get a card is not requested again and again.
  const [tried, setTried] = useState<string[]>([])

  if (revision.isLoading) return <Frame><Skeleton label="Təkrar kartları hazırlanır…" /></Frame>
  if (revision.isError) {
    return (
      <Frame>
        <Alert title="Son təkrar hazırlanmadı" action={<button type="button" onClick={() => revision.refetch()} className="text-sm font-semibold underline">Yenidən cəhd et</button>}>
          {revision.error.message}
        </Alert>
      </Frame>
    )
  }

  const data = revision.data!
  const ready = data.topics.filter(t => t.card)
  const waiting = data.topics.filter(t => !t.card && !tried.includes(t.key))
  const loadMore = () => {
    const keys = waiting.slice(0, batch).map(t => t.key)
    setTried(previous => [...previous, ...keys])
    more.mutate(keys)
  }

  return (
    <Frame>
      <header className="flex items-start gap-3">
        <span className="grid size-10 shrink-0 place-items-center rounded-xl bg-indigo-50 text-indigo-700" aria-hidden><Timer className="size-5" /></span>
        <div className="min-w-0">
          <h2 className="text-base font-semibold">Son təkrar</h2>
          <p className="mt-0.5 text-sm text-slate-600">
            {data.hasHistory ? 'Məni yoxla-da səhv etdiyin mövzular əvvəldədir.' : 'Hələ yoxlama nəticən yoxdur — əsas mövzular üzrə ümumi təkrar.'}
          </p>
          <p className="mt-0.5 text-xs text-slate-500">İmtahanda hansı sualın düşəcəyi bilinmir; bu, əsas biliklərin qısa təkrarıdır.</p>
        </div>
      </header>

      {data.warning && <Alert tone="info" title="Qeyd">{data.warning}</Alert>}
      {(more.error?.message ?? data.error) && <Alert tone="info" title="Bəzi kartlar hazırlanmadı">{more.error?.message ?? data.error}</Alert>}

      {ready.length === 0 && !more.isPending ? (
        <p className="tutor-border rounded-2xl border border-dashed px-4 py-8 text-center text-sm text-slate-500">Hələ təkrar kartı yoxdur.</p>
      ) : (
        <ul className="flex flex-col gap-3">{ready.map(topic => <TopicCard key={topic.key} topic={topic} onQuiz={onQuiz} />)}</ul>
      )}

      {more.isPending && <Skeleton label="Növbəti mövzular hazırlanır…" />}
      {waiting.length > 0 && !more.isPending && (
        <button type="button" onClick={loadMore} disabled={disabled} className="tutor-shared-action w-full">
          Daha çox mövzu ({waiting.length})
        </button>
      )}
    </Frame>
  )
}

function Frame({ children }: { children: ReactNode }) {
  return <div className="mx-auto flex w-full max-w-2xl min-w-0 flex-col gap-4 px-4 py-5 sm:py-8" style={{ overflowWrap: 'anywhere' }}>{children}</div>
}

function TopicCard({ topic, onQuiz }: { topic: RevisionTopic; onQuiz: Props['onQuiz'] }) {
  const card = topic.card!
  const weak = topic.wrong > 0
  return (
    <li className={cn('tutor-enter tutor-border rounded-2xl border bg-white p-4 sm:p-5', weak && 'border-l-4 border-l-rose-400')}>
      <div className="flex flex-wrap items-start gap-2">
        <h3 className="min-w-0 flex-1 text-[15px] font-semibold leading-snug text-slate-900">{topic.title}</h3>
        {weak && <span className="shrink-0 rounded-full bg-rose-50 px-2 py-0.5 text-xs font-semibold text-rose-700">{topic.wrong}/{topic.answered} səhv</span>}
      </div>

      <ul className="mt-3 space-y-1.5">
        {card.facts.map(fact => (
          <li key={fact} className="flex gap-2 text-sm leading-relaxed text-slate-800"><Check className="mt-0.5 size-4 shrink-0 text-emerald-600" aria-hidden />{fact}</li>
        ))}
      </ul>

      {card.concepts.length > 0 && (
        <dl className="mt-3 space-y-1 text-sm">
          {card.concepts.map(c => (
            <div key={c.term} className="leading-relaxed"><dt className="inline font-semibold text-slate-900">{c.term}</dt><dd className="inline text-slate-600"> — {c.meaning}</dd></div>
          ))}
        </dl>
      )}

      {card.comparisons.map(c => (
        <div key={`${c.first}|${c.second}`} className="tutor-sunken mt-3 rounded-xl px-3 py-2 text-sm">
          <p className="flex items-center gap-1.5 font-semibold text-slate-900">{c.first}<ArrowLeftRight className="size-3.5 text-slate-500" aria-label="müqayisə" />{c.second}</p>
          <p className="mt-0.5 leading-relaxed text-slate-600">{c.difference}</p>
        </div>
      ))}

      {card.tips.map(tip => (
        <p key={tip} className="mt-3 flex gap-2 rounded-xl bg-amber-50 px-3 py-2 text-sm text-amber-900"><Lightbulb className="mt-0.5 size-4 shrink-0" aria-hidden />{tip}</p>
      ))}

      <div className="mt-3 flex flex-wrap items-center justify-between gap-2">
        <span className="text-xs text-slate-500">{card.sourcePages.length > 0 ? `Səh. ${card.sourcePages.join(', ')}` : ''}</span>
        <button type="button" onClick={() => onQuiz(topic.title, weak ? topic.wrongNumbers : topic.numbers)}
          className="inline-flex min-h-9 items-center gap-1.5 rounded-lg px-2.5 text-sm font-semibold text-indigo-700 hover:bg-indigo-50">
          <RotateCcw className="size-4" aria-hidden />{weak ? 'Səhvləri təkrar et' : 'Bu mövzudan yoxla'}
        </button>
      </div>
    </li>
  )
}
