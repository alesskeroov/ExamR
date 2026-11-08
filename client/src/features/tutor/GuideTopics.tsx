import { Flame, LibraryBig, Play, Shuffle } from 'lucide-react'
import { Alert } from '../../components/common/Alert'
import { cn } from '../../lib/utils'
import type { CardDeck, LearnScope } from './api'
import { Frame } from './TutorShared'

interface Props {
  deck: CardDeck
  onLearn: (scope: LearnScope) => void
}

/** "Mövzular": the document's topics with the student's mastery per topic; the starting point of the guide. */
export function GuideTopics({ deck, onLearn }: Props) {
  const mastered = deck.cards.filter(c => c.mastered).length
  const weak = deck.cards.filter(c => c.weak).length
  const seen = deck.cards.filter(c => c.seen).length
  const byNumber = new Map(deck.cards.map(c => [c.number, c]))

  return (
    <Frame>
      <header className="flex items-start gap-3">
        <span className="grid size-10 shrink-0 place-items-center rounded-xl bg-indigo-50 text-indigo-700" aria-hidden><LibraryBig className="size-5" /></span>
        <div className="min-w-0">
          <h2 className="text-base font-semibold">Mövzular</h2>
          <p className="mt-0.5 text-sm text-slate-600">
            Sənəddəki {deck.cards.length} sual kartlara çevrildi. Kart ardıcıl {deck.required} dəfə "Bilirdim" deyəndə mənimsənilmiş sayılır.
          </p>
        </div>
      </header>

      {deck.warning && <Alert tone="info" title="Qeyd">{deck.warning}</Alert>}

      <section aria-label="Ümumi gedişat" className="tutor-border rounded-2xl border bg-white p-4 sm:p-5">
        <div className="flex items-baseline justify-between gap-2">
          <p className="text-sm font-semibold text-slate-900">Mənimsənilib: {mastered} / {deck.cards.length}</p>
          <p className="text-xs text-slate-500">{seen} kart görülüb</p>
        </div>
        <Bar value={mastered} total={deck.cards.length} label="Mənimsənilmiş kartlar" />
        <div className="mt-4 grid gap-2 sm:grid-cols-3">
          <button type="button" onClick={() => onLearn({ kind: 'all' })}
            className="btn-primary inline-flex h-11 items-center justify-center gap-2 rounded-xl text-sm font-semibold">
            <Play className="size-4" aria-hidden />{seen === 0 ? 'Öyrənməyə başla' : mastered === deck.cards.length ? 'Təkrarla' : 'Davam et'}
          </button>
          <button type="button" onClick={() => onLearn({ kind: 'weak' })} disabled={weak === 0} className="tutor-shared-action">
            <Flame className="size-4 text-rose-600" aria-hidden />Zəif kartlar ({weak})
          </button>
          <button type="button" onClick={() => onLearn({ kind: 'mixed' })} className="tutor-shared-action">
            <Shuffle className="size-4" aria-hidden />Qarışıq
          </button>
        </div>
      </section>

      <ul className="flex flex-col gap-2" aria-label="Mövzu siyahısı">
        {deck.topics.map(topic => {
          const cards = topic.numbers.map(n => byNumber.get(n)).filter(c => c !== undefined)
          const done = cards.filter(c => c.mastered).length
          const weakHere = cards.filter(c => c.weak).length
          const complete = done === cards.length
          return (
            <li key={topic.key} className="tutor-border rounded-2xl border bg-white p-4">
              <div className="flex items-start gap-3">
                <div className="min-w-0 flex-1">
                  <h3 className="text-[15px] font-semibold leading-snug text-slate-900">{topic.title}</h3>
                  <p className="mt-0.5 text-xs text-slate-500">
                    {done}/{cards.length} mənimsənilib{weakHere > 0 && <span className="text-rose-600"> · {weakHere} zəif</span>}
                  </p>
                </div>
                <button type="button" onClick={() => onLearn({ kind: 'topic', key: topic.key })}
                  className={cn('inline-flex h-9 shrink-0 items-center gap-1.5 rounded-lg px-3 text-sm font-semibold',
                    complete ? 'text-slate-600 hover:bg-slate-100' : 'bg-indigo-50 text-indigo-700 hover:bg-indigo-100')}>
                  {complete ? 'Təkrarla' : 'Öyrən'}
                </button>
              </div>
              <Bar value={done} total={cards.length} label={`${topic.title}: mənimsənilmiş kartlar`} />
            </li>
          )
        })}
      </ul>
    </Frame>
  )
}

function Bar({ value, total, label }: { value: number; total: number; label: string }) {
  return (
    <div className="mt-2 h-2 overflow-hidden rounded-full bg-slate-100" role="progressbar" aria-valuemin={0} aria-valuemax={total} aria-valuenow={value} aria-label={label}>
      <div className="h-full rounded-full bg-emerald-500 transition-[width]" style={{ width: `${total ? (value / total) * 100 : 0}%` }} />
    </div>
  )
}
