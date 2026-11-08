import { Check, FileText, RotateCw, X } from 'lucide-react'
import type { KeyboardEvent, MouseEvent } from 'react'
import { cn } from '../../lib/utils'
import type { CardKind, StudyCard } from './api'

const kindLabel: Record<CardKind, string> = { Question: 'Sual', Term: 'Anlayış', List: 'Siyahı', Test: 'Test' }
const prompt: Record<CardKind, string> = {
  Question: 'Cavabı yadına sal',
  Term: 'Bu anlayış nədir?',
  List: 'Bəndləri sadala',
  Test: 'Düzgün cavab hansıdır?',
}
const kindTone: Record<CardKind, string> = {
  Question: 'bg-indigo-50 text-indigo-700',
  Term: 'bg-emerald-50 text-emerald-700',
  List: 'bg-amber-50 text-amber-800',
  Test: 'bg-slate-100 text-slate-700',
}

interface Props {
  card: StudyCard
  topic: string
  flipped: boolean
  onFlip: () => void
  /** Opens the document at the card's source. */
  onSource?: (card: StudyCard) => void
  /** Mastery dots ("Bilirdim" in a row out of required). */
  required?: number
  size?: 'large' | 'small'
}

/** A flashcard that turns over on click (or Enter/Space): topic and question in front, the document's answer and details behind. */
export function FlipCard({ card, topic, flipped, onFlip, onSource, required, size = 'large' }: Props) {
  const large = size === 'large'
  const onKey = (event: KeyboardEvent) => {
    if (event.target !== event.currentTarget) return
    if (event.key === 'Enter' || event.key === ' ') { event.preventDefault(); event.stopPropagation(); onFlip() }
  }
  const source = (event: MouseEvent) => { event.stopPropagation(); onSource?.(card) }

  return (
    <div role="button" tabIndex={0} aria-pressed={flipped} onClick={onFlip} onKeyDown={onKey}
      aria-label={flipped ? 'Kartı ön üzə çevir' : 'Kartı çevir və cavabı gör'}
      className={cn('flip-card group block w-full cursor-pointer text-left outline-none', flipped && 'is-flipped')}>
      <div className="flip-card-inner">
        {/* Front */}
        <div inert={flipped} aria-hidden={flipped}
          className={cn('flip-card-face tutor-border flex flex-col rounded-2xl border bg-white shadow-sm group-focus-visible:ring-2 group-focus-visible:ring-indigo-400',
            large ? 'min-h-[19rem] p-5 sm:p-7' : 'h-60 p-4')}>
          <Header card={card} topic={topic} large={large} />
          <div className="flex flex-1 flex-col justify-center py-3 text-center">
            <p className="text-xs font-medium tracking-wide text-slate-400 uppercase">{prompt[card.kind]}</p>
            <p className={cn('mt-2 font-semibold leading-snug text-slate-900', large ? 'text-xl sm:text-[22px]' : 'line-clamp-4 text-[15px]')}>{card.front}</p>
          </div>
          <div className="flex items-center justify-between gap-2 text-xs text-slate-400">
            <span className="inline-flex items-center gap-1"><RotateCw className="size-3.5" aria-hidden />Çevirmək üçün toxun</span>
            {required != null && <Dots streak={card.streak} required={required} mastered={card.mastered} />}
          </div>
        </div>

        {/* Back */}
        <div inert={!flipped} aria-hidden={!flipped}
          className={cn('flip-card-face flip-card-back tutor-border flex flex-col rounded-2xl border bg-white shadow-sm group-focus-visible:ring-2 group-focus-visible:ring-indigo-400',
            large ? 'min-h-[19rem] p-5 sm:p-7' : 'h-60 p-4')}>
          <Header card={card} topic={topic} large={large} />
          <p className={cn('mt-2 font-semibold leading-snug text-slate-500', large ? 'text-sm' : 'line-clamp-1 text-xs')}>{card.front}</p>
          <div className={cn('mt-3 min-h-0 flex-1 overflow-y-auto overscroll-contain pr-1', large && 'max-h-[55vh]')}>
            <Back card={card} large={large} />
          </div>
          {onSource && (
            <button type="button" onClick={source} className="mt-3 inline-flex items-center gap-1.5 self-start text-xs font-semibold text-indigo-700 hover:underline">
              <FileText className="size-3.5" aria-hidden />Sənəddə göstər{card.page != null ? ` · səh. ${card.page}` : ''}
            </button>
          )}
        </div>
      </div>
    </div>
  )
}

function Header({ card, topic, large }: { card: StudyCard; topic: string; large: boolean }) {
  return (
    <div className="flex items-start gap-2">
      <p className={cn('min-w-0 flex-1 font-semibold text-slate-600', large ? 'line-clamp-2 text-sm' : 'line-clamp-1 text-xs')} title={topic}>{topic}</p>
      <span className={cn('shrink-0 rounded-full px-2 py-0.5 text-[11px] font-semibold', kindTone[card.kind])}>
        {kindLabel[card.kind]}{card.kind === 'Question' || card.kind === 'Test' ? ` ${card.source}` : ''}
      </span>
    </div>
  )
}

function Back({ card, large }: { card: StudyCard; large: boolean }) {
  const text = large ? 'text-[15px]' : 'text-sm'
  if (card.kind === 'List') {
    return (
      <>
        <p className="text-xs font-semibold text-amber-800">{card.items.length} bənd</p>
        <ol className={cn('mt-1.5 list-decimal space-y-1 pl-5 leading-relaxed text-slate-800', text)}>
          {card.items.map(item => <li key={item}>{item}</li>)}
        </ol>
      </>
    )
  }
  if (card.kind === 'Test') {
    return (
      <>
        <p className={cn('flex gap-2 rounded-xl bg-emerald-50 px-3 py-2 font-semibold text-emerald-800', text)}>
          <Check className="mt-0.5 size-4 shrink-0" aria-hidden />{card.back}
        </p>
        {card.items.map(note => <p key={note} className={cn('mt-2 leading-relaxed text-slate-700', text)}>{note}</p>)}
        {card.others.length > 0 && (
          <>
            <p className="mt-3 text-xs font-semibold text-slate-500">Digər variantlar</p>
            <ul className="mt-1 space-y-1">
              {card.others.map(option => (
                <li key={option} className="flex gap-2 text-sm text-slate-500"><X className="mt-0.5 size-3.5 shrink-0 text-rose-400" aria-hidden />{option}</li>
              ))}
            </ul>
          </>
        )}
      </>
    )
  }
  return (
    <>
      <p className={cn('leading-relaxed whitespace-pre-line text-slate-800', text)}>{card.back}</p>
      {card.kind === 'Question' && card.items.length > 0 && (
        <>
          <p className="mt-3 text-xs font-semibold text-slate-500">Bu cavabdakı anlayışlar</p>
          <div className="mt-1.5 flex flex-wrap gap-1.5">
            {card.items.map(term => <span key={term} className="rounded-full bg-emerald-50 px-2 py-0.5 text-xs font-medium text-emerald-700">{term}</span>)}
          </div>
        </>
      )}
    </>
  )
}

function Dots({ streak, required, mastered }: { streak: number; required: number; mastered: boolean }) {
  return (
    <span className="inline-flex items-center gap-1" title={mastered ? 'Mənimsənilib' : `Ardıcıl ${required} dəfə "Bilirdim" lazımdır`}>
      {Array.from({ length: required }, (_, i) => (
        <span key={i} className={cn('inline-block size-2 rounded-full', mastered || i < streak ? 'bg-emerald-500' : 'bg-slate-200')} />
      ))}
    </span>
  )
}
