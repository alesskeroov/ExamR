import { useQueryClient } from '@tanstack/react-query'
import { CalendarClock, Eye, Layers, Play, RotateCcw } from 'lucide-react'
import { useEffect, useState, type ReactNode } from 'react'
import { Alert } from '../../components/common/Alert'
import { queryKeys } from '../../lib/query-keys'
import { cn } from '../../lib/utils'
import { useCards, useReviewCard, type CardGrade, type StudyCard } from './api'
import { Skeleton } from './TutorShared'

interface Props { chatId: string }

type Summary = Record<CardGrade, number>

const grades: { grade: CardGrade; label: string; key: string; tone: string }[] = [
  { grade: 'Again', label: 'Bilmirdim', key: '1', tone: 'border-rose-200 bg-rose-50 text-rose-800 hover:bg-rose-100' },
  { grade: 'Hard', label: 'Qismən', key: '2', tone: 'border-amber-200 bg-amber-50 text-amber-900 hover:bg-amber-100' },
  { grade: 'Good', label: 'Bilirdim', key: '3', tone: 'border-emerald-200 bg-emerald-50 text-emerald-800 hover:bg-emerald-100' },
]

// Mirrors CardScheduler on the server, only to label the buttons.
const days = [0, 1, 3, 7, 14, 30, 60]
function nextLabel(box: number, grade: CardGrade) {
  if (grade === 'Again') return '10 dəq'
  if (grade === 'Hard') {
    const half = days[Math.max(1, Math.min(6, box))] / 2
    return half < 1 ? '12 saat' : `${Math.round(half)} gün`
  }
  return `${days[Math.min(6, box + 1)]} gün`
}

// Browsers often lack Azerbaijani month names in Intl ("M11"), so the date is spelled out here.
const months = ['yanvar', 'fevral', 'mart', 'aprel', 'may', 'iyun', 'iyul', 'avqust', 'sentyabr', 'oktyabr', 'noyabr', 'dekabr']
const formatDate = (value: string) => {
  const d = new Date(value)
  return `${d.getDate()} ${months[d.getMonth()]}, ${String(d.getHours()).padStart(2, '0')}:${String(d.getMinutes()).padStart(2, '0')}`
}

/** "Kartlar": the document's own questions as flashcards with spaced repetition. The student grades themself, so there is no AI cost. */
export function TutorCards({ chatId }: Props) {
  const client = useQueryClient()
  const [ahead, setAhead] = useState(false)
  const deck = useCards(chatId, ahead)
  const [session, setSession] = useState<StudyCard[] | null>(null)
  const [summary, setSummary] = useState<Summary | null>(null)

  if (deck.isLoading) return <Frame><Skeleton label="Kartlar hazırlanır…" /></Frame>
  if (deck.isError) {
    return (
      <Frame>
        <Alert title="Kartlar açılmadı" action={<button type="button" onClick={() => deck.refetch()} className="text-sm font-semibold underline">Yenidən cəhd et</button>}>
          {deck.error.message}
        </Alert>
      </Frame>
    )
  }

  const data = deck.data!
  if (session) {
    return (
      <Frame>
        <CardSession chatId={chatId} cards={session} onFinish={result => {
          setSession(null)
          setSummary(result)
          setAhead(false)
          void client.invalidateQueries({ queryKey: queryKeys.tutor.cardsAll(chatId) })
        }} />
      </Frame>
    )
  }

  const seen = data.learning + data.learned
  return (
    <Frame>
      <header className="flex items-start gap-3">
        <span className="grid size-10 shrink-0 place-items-center rounded-xl bg-indigo-50 text-indigo-700" aria-hidden><Layers className="size-5" /></span>
        <div className="min-w-0">
          <h2 className="text-base font-semibold">Kartlar</h2>
          <p className="mt-0.5 text-sm text-slate-600">Sualı oxu, cavabı yadına sal, sonra aç və özünü qiymətləndir. Bildiyin kartlar daha gec, bilmədiklərin tez qayıdır.</p>
        </div>
      </header>

      {data.warning && <Alert tone="info" title="Qeyd">{data.warning}</Alert>}
      {summary && <SummaryNote summary={summary} />}

      <section aria-label="Gedişat" className="tutor-border rounded-2xl border bg-white p-4 sm:p-5">
        <div className="flex items-baseline justify-between gap-2">
          <p className="text-sm font-semibold text-slate-900">Öyrənilib: {data.learned} / {data.total}</p>
          <p className="text-xs text-slate-500">{seen} kart görülüb</p>
        </div>
        <div className="mt-2 h-2 overflow-hidden rounded-full bg-slate-100" role="progressbar" aria-valuemin={0} aria-valuemax={data.total} aria-valuenow={data.learned}
          aria-label="Öyrənilmiş kartlar">
          <div className="h-full rounded-full bg-emerald-500 transition-[width]" style={{ width: `${data.total ? (data.learned / data.total) * 100 : 0}%` }} />
        </div>
        <dl className="mt-4 grid grid-cols-2 gap-2 sm:grid-cols-4">
          <Stat label="Təkrar vaxtıdır" value={data.due} tone="text-indigo-700" />
          <Stat label="Yeni" value={data.new} tone="text-slate-900" />
          <Stat label="Öyrənilir" value={data.learning} tone="text-amber-700" />
          <Stat label="Öyrənilib" value={data.learned} tone="text-emerald-700" />
        </dl>
      </section>

      {data.cards.length > 0 ? (
        <button type="button" onClick={() => { setSummary(null); setSession(data.cards) }}
          className="btn-primary inline-flex h-11 w-full items-center justify-center gap-2 rounded-xl text-sm font-semibold">
          <Play className="size-4" aria-hidden />
          {ahead ? `Əlavə məşq (${data.cards.length} kart)` : `Başla — ${data.cards.length} kart`}
        </button>
      ) : (
        <div className="tutor-border rounded-2xl border border-dashed px-4 py-6 text-center">
          <CalendarClock className="mx-auto size-6 text-slate-400" aria-hidden />
          <p className="mt-2 text-sm font-semibold text-slate-800">Bu gün üçün kartlar bitdi</p>
          {data.nextDueAt && <p className="mt-1 text-sm text-slate-500">Növbəti təkrar: {formatDate(data.nextDueAt)}</p>}
          {!ahead && seen > 0 && (
            <button type="button" onClick={() => setAhead(true)} className="tutor-shared-action mt-4">
              <RotateCcw className="size-4" aria-hidden />Yenə də məşq et
            </button>
          )}
        </div>
      )}
    </Frame>
  )
}

function CardSession({ chatId, cards, onFinish }: { chatId: string; cards: StudyCard[]; onFinish: (summary: Summary) => void }) {
  const review = useReviewCard(chatId)
  const [queue, setQueue] = useState(cards)
  const [done, setDone] = useState(0)
  const [revealed, setRevealed] = useState(false)
  const [typed, setTyped] = useState('')
  const [summary, setSummary] = useState<Summary>({ Again: 0, Hard: 0, Good: 0 })
  const card = queue[0]

  const grade = (value: CardGrade) => {
    if (!card || review.isPending) return
    review.mutate({ number: card.number, grade: value }, {
      onSuccess: state => {
        const nextSummary = { ...summary, [value]: summary[value] + 1 }
        // A forgotten card comes back at the end of this round, starting from the first box.
        const rest = value === 'Again' ? [...queue.slice(1), { ...card, box: state.box, isNew: false }] : queue.slice(1)
        setSummary(nextSummary)
        setTyped('')
        setRevealed(false)
        if (value !== 'Again') setDone(d => d + 1)
        if (rest.length === 0) onFinish(nextSummary)
        else setQueue(rest)
      },
    })
  }

  // Space/Enter reveals, 1–3 grade; ignored while typing an answer.
  useEffect(() => {
    const onKey = (event: KeyboardEvent) => {
      const target = event.target as HTMLElement | null
      if (target?.closest('textarea, input')) return
      if (!revealed && (event.key === ' ' || event.key === 'Enter')) {
        if (target?.closest('button, a')) return // the focused control handles its own key
        event.preventDefault()
        setRevealed(true)
      }
      else if (revealed) {
        const match = grades.find(g => g.key === event.key)
        if (match) grade(match.grade)
      }
    }
    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
  })

  if (!card) return null
  const total = cards.length
  return (
    <>
      <div className="flex items-center justify-between gap-3 text-sm">
        <button type="button" onClick={() => onFinish(summary)} className="font-semibold text-slate-600 hover:text-slate-900">Dayandır</button>
        <span className="text-slate-500" aria-live="polite">{done} / {total}</span>
      </div>
      <div className="h-1.5 overflow-hidden rounded-full bg-slate-100" aria-hidden>
        <div className="h-full rounded-full bg-indigo-500 transition-[width]" style={{ width: `${(done / total) * 100}%` }} />
      </div>

      <article key={`${card.number}-${queue.length}`} className="tutor-enter tutor-border rounded-2xl border bg-white p-4 sm:p-6">
        <div className="flex flex-wrap items-center gap-2 text-xs text-slate-500">
          <span className="font-semibold text-slate-700">Sual {card.number}</span>
          {card.page != null && <span>· Səh. {card.page}</span>}
          {card.isNew && <span className="rounded-full bg-indigo-50 px-2 py-0.5 font-semibold text-indigo-700">Yeni</span>}
        </div>
        <h3 className="mt-2 text-[17px] font-semibold leading-snug text-slate-900">{card.question}</h3>

        {!revealed ? (
          <>
            <label className="mt-4 block text-xs font-medium text-slate-500" htmlFor="card-answer">Cavabını yaz (istəyə görə)</label>
            <textarea id="card-answer" value={typed} onChange={e => setTyped(e.target.value)} rows={3} maxLength={4000}
              className="tutor-border mt-1 w-full resize-y rounded-xl border bg-transparent px-3 py-2 text-sm text-slate-900 placeholder:text-slate-400"
              placeholder="Yadına düşənləri qısa yaz…" />
            <button type="button" onClick={() => setRevealed(true)}
              className="btn-primary mt-3 inline-flex h-11 w-full items-center justify-center gap-2 rounded-xl text-sm font-semibold">
              <Eye className="size-4" aria-hidden />Cavabı göstər
            </button>
            <p className="mt-2 hidden text-center text-[11px] text-slate-400 sm:block">Boşluq — cavabı göstər · 1 / 2 / 3 — qiymət</p>
          </>
        ) : (
          <>
            {typed.trim() && (
              <div className="tutor-sunken mt-4 rounded-xl px-3 py-2">
                <p className="text-xs font-semibold text-slate-500">Sənin cavabın</p>
                <p className="mt-1 text-sm whitespace-pre-line text-slate-800">{typed}</p>
              </div>
            )}
            <div className="mt-4 border-l-4 border-l-emerald-400 pl-3">
              <p className="text-xs font-semibold text-emerald-700">Sənəddəki cavab</p>
              <p className="mt-1 text-sm leading-relaxed whitespace-pre-line text-slate-800">{card.answer}</p>
              {card.sharedAnswer && <p className="mt-2 text-xs text-slate-500">Sənəddə bu cavab növbəti sual(lar)la birlikdə verilib.</p>}
            </div>
            <p className="mt-5 text-sm font-medium text-slate-700">Nə qədər bildin?</p>
            <div className="mt-2 grid grid-cols-3 gap-2">
              {grades.map(g => (
                <button key={g.grade} type="button" onClick={() => grade(g.grade)} disabled={review.isPending}
                  className={cn('flex min-h-14 flex-col items-center justify-center rounded-xl border px-2 py-1.5 text-sm font-semibold disabled:opacity-60', g.tone)}>
                  {g.label}
                  <span className="text-[11px] font-normal opacity-80">{nextLabel(card.box, g.grade)}</span>
                </button>
              ))}
            </div>
          </>
        )}
      </article>
      {review.isError && <Alert title="Qiymət saxlanmadı">{review.error.message}</Alert>}
    </>
  )
}

function SummaryNote({ summary }: { summary: Summary }) {
  const total = summary.Again + summary.Hard + summary.Good
  if (total === 0) return null
  return (
    <div className="tutor-enter rounded-2xl bg-emerald-50 px-4 py-3 text-sm text-emerald-900" role="status">
      <p className="font-semibold">Raund bitdi</p>
      <p className="mt-0.5">Bilirdim: {summary.Good} · Qismən: {summary.Hard} · Bilmirdim: {summary.Again}</p>
    </div>
  )
}

function Stat({ label, value, tone }: { label: string; value: number; tone: string }) {
  return (
    <div className="tutor-sunken rounded-xl px-3 py-2">
      <dt className="text-xs text-slate-500">{label}</dt>
      <dd className={cn('text-lg font-semibold', tone)}>{value}</dd>
    </div>
  )
}

function Frame({ children }: { children: ReactNode }) {
  return <div className="mx-auto flex w-full max-w-2xl min-w-0 flex-col gap-4 px-4 py-5 sm:py-8" style={{ overflowWrap: 'anywhere' }}>{children}</div>
}
