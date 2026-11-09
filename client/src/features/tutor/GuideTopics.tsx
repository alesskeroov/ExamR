import { Check, ChevronRight, Flame, Play, Search, Shuffle } from 'lucide-react'
import { useMemo, useState } from 'react'
import { Alert } from '../../components/common/Alert'
import { cn } from '../../lib/utils'
import { topicStats, type CardDeck, type LearnScope } from './api'
import { Frame } from './TutorShared'

interface Props {
  deck: CardDeck
  onLearn: (scope: LearnScope) => void
}

const fold = (text: string) => text.toLocaleLowerCase('az')

/** "Mövzular": overall progress, the three ways to start, and a searchable topic list; a tap on a topic studies it. */
export function GuideTopics({ deck, onLearn }: Props) {
  const [term, setTerm] = useState('')
  const stats = useMemo(() => topicStats(deck), [deck])
  const mastered = deck.cards.filter(c => c.mastered).length
  const weak = deck.cards.filter(c => c.weak).length
  const seen = deck.cards.filter(c => c.seen).length
  const total = deck.cards.length
  const percent = total ? Math.round((mastered / total) * 100) : 0
  const query = fold(term.trim())
  const numbered = deck.topics.map((topic, i) => ({ topic, number: i + 1 }))
  const shown = query ? numbered.filter(t => fold(t.topic.title).includes(query)) : numbered

  return (
    <Frame>
      {total === 0 && <Alert tone="info" title="Bu sənəd konspekt kimi saxlanıldı">Etibarlı sual–cavab əlaqəsi tapılmadığı üçün avtomatik kart yaradılmadı. Mövzuların altından konspekti oxuya bilərsiniz.</Alert>}
      {deck.warning && <Alert tone="info" title="Qeyd">{deck.warning}</Alert>}

      {total > 0 && <section aria-label="Ümumi gedişat" className="tutor-border rounded-3xl border bg-white p-5 sm:p-6">
        <div className="flex items-end justify-between gap-4">
          <div>
            <p className="text-sm text-slate-500">Mənimsənilib</p>
            <p className="mt-1 text-4xl font-bold tracking-tight text-slate-900 tabular-nums">{percent}%</p>
          </div>
          <p className="pb-1 text-right text-sm text-slate-500 tabular-nums">
            <span className="font-semibold text-slate-900">{mastered}</span> / {total} kart
            <span className="block text-xs">{deck.topics.length} mövzu</span>
          </p>
        </div>
        <div className="mt-3 h-2.5 overflow-hidden rounded-full bg-slate-100" role="progressbar" aria-valuemin={0} aria-valuemax={total} aria-valuenow={mastered} aria-label="Mənimsənilmiş kartlar">
          <div className="h-full rounded-full bg-emerald-500 transition-[width]" style={{ width: `${percent}%` }} />
        </div>

        <button type="button" disabled={total === 0} onClick={() => onLearn({ kind: 'all' })}
          className="btn-primary mt-5 inline-flex h-12 w-full items-center justify-center gap-2 rounded-2xl text-[15px] font-semibold">
          <Play className="size-4" aria-hidden />{seen === 0 ? 'Öyrənməyə başla' : mastered === total ? 'Hamısını təkrarla' : 'Davam et'}
        </button>
        <div className="mt-2 grid grid-cols-2 gap-2">
          <button type="button" onClick={() => onLearn({ kind: 'weak' })} disabled={weak === 0} className="tutor-shared-action">
            <Flame className="size-4 text-rose-600" aria-hidden />Zəif kartlar{weak > 0 ? ` · ${weak}` : ''}
          </button>
          <button type="button" disabled={total === 0} onClick={() => onLearn({ kind: 'mixed' })} className="tutor-shared-action">
            <Shuffle className="size-4" aria-hidden />Qarışıq
          </button>
        </div>
      </section>}

      <section aria-labelledby="topics-title" className="flex flex-col gap-3">
        <div className="flex items-center justify-between gap-3">
          <h2 id="topics-title" className="text-base font-semibold">Mövzular</h2>
          <span className="text-xs text-slate-500">Kartlarla məşq et və ya konspekti oxu</span>
        </div>
        {deck.topics.length > 8 && (
          <label className="tutor-border flex h-11 items-center gap-2 rounded-xl border bg-white px-3">
            <Search className="size-4 shrink-0 text-slate-400" aria-hidden />
            <span className="sr-only">Mövzu axtar</span>
            <input type="search" value={term} onChange={e => setTerm(e.target.value)} placeholder="Mövzu axtar…"
              className="min-w-0 flex-1 bg-transparent text-base text-slate-900 outline-none placeholder:text-slate-400 sm:text-sm" />
          </label>
        )}
        <ul className="tutor-border overflow-hidden rounded-2xl border bg-white">
          {shown.map(({ topic, number }, i) => {
            const s = stats.get(topic.key) ?? { total: 0, mastered: 0, weak: 0, seen: 0 }
            const complete = s.total > 0 && s.mastered === s.total
            return (
              <li key={topic.key} className={cn(i > 0 && 'tutor-border border-t')}>
                <button type="button" disabled={s.total === 0} onClick={() => onLearn({ kind: 'topic', key: topic.key })}
                  className="flex w-full items-center gap-3 px-4 py-3.5 text-left hover:bg-slate-50">
                  <span className={cn('grid size-8 shrink-0 place-items-center rounded-full text-xs font-semibold tabular-nums',
                    complete ? 'bg-emerald-50 text-emerald-700' : 'bg-slate-100 text-slate-600')}>
                    {complete ? <Check className="size-4" aria-label="Mənimsənilib" /> : number}
                  </span>
                  <span className="min-w-0 flex-1">
                    <span className="line-clamp-2 text-[15px] leading-snug font-medium text-slate-900">{topic.title}</span>
                    {s.total > 0 ? <span className="mt-1.5 flex items-center gap-2">
                      <span className="h-1.5 flex-1 overflow-hidden rounded-full bg-slate-100">
                        <span className="block h-full rounded-full bg-emerald-500" style={{ width: `${s.total ? (s.mastered / s.total) * 100 : 0}%` }} />
                      </span>
                      <span className="shrink-0 text-xs text-slate-500 tabular-nums">
                        {s.mastered}/{s.total}{s.weak > 0 && <span className="text-rose-600"> · {s.weak} zəif</span>}
                      </span>
                    </span> : <span className="mt-1 block text-xs text-slate-500">Oxu materialı</span>}
                  </span>
                  <ChevronRight className="size-4 shrink-0 text-slate-400" aria-hidden />
                </button>
                {topic.notes && (
                  <details className="px-4 pb-4">
                    <summary className="cursor-pointer py-2 text-sm font-semibold text-indigo-700">Konspekti oxu</summary>
                    <div className="guide-scroll mt-2 max-h-[60dvh] overflow-y-auto whitespace-pre-wrap break-words text-sm leading-relaxed text-slate-700">{topic.notes}</div>
                  </details>
                )}
              </li>
            )
          })}
          {shown.length === 0 && <li className="px-4 py-8 text-center text-sm text-slate-500">Mövzu tapılmadı.</li>}
        </ul>
      </section>
    </Frame>
  )
}
