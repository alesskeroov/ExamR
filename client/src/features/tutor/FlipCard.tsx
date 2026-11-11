import { Check, FileText, RotateCw, X } from 'lucide-react'
import { useEffect, useRef, type KeyboardEvent, type MouseEvent } from 'react'
import { cn } from '../../lib/utils'
import type { CardKind, StudyCard } from './api'
import { splitCardAnswer } from './card-answer'

// Shared with the card grid (kept here: this file is the card's look).
const kindLabel: Record<CardKind, string> = { Question: 'Sual', Term: 'Anlayış', List: 'Siyahı', Test: 'Test', Table: 'Cədvəl', Custom: 'Mənim kartım' }
const prompt: Record<CardKind, string> = {
  Question: 'Cavabı yadına sal',
  Term: 'Bu anlayış nədir?',
  List: 'Bəndləri sadala',
  Test: 'Düzgün cavab hansıdır?',
  Table: 'Bu sətir üzrə məlumatları xatırla',
  Custom: 'Cavabı yadına sal',
}
const kindTone: Record<CardKind, string> = {
  Question: 'bg-indigo-50 text-indigo-700',
  Term: 'bg-emerald-50 text-emerald-700',
  List: 'bg-amber-50 text-amber-800',
  Test: 'bg-sky-50 text-sky-700',
  Table: 'bg-rose-50 text-rose-700',
  Custom: 'bg-slate-100 text-slate-700',
}
const kindAccent: Record<CardKind, string> = {
  Question: 'bg-indigo-500',
  Term: 'bg-emerald-500',
  List: 'bg-amber-500',
  Test: 'bg-sky-500',
  Table: 'bg-rose-500',
  Custom: 'bg-slate-500',
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
  /** Focus the card when it appears, so Space/Enter flip it right away. */
  autoFocus?: boolean
}

/**
 * A flashcard that turns over on tap (or Enter/Space): topic and question in front, the document's answer and details behind.
 * It fills the height its parent gives it, so every card has the same frame; long content scrolls inside the card.
 */
export function FlipCard({ card, topic, flipped, onFlip, onSource, required, autoFocus = false }: Props) {
  const self = useRef<HTMLDivElement>(null)
  useEffect(() => { if (autoFocus) self.current?.focus({ preventScroll: true }) }, [autoFocus, card.id])
  const onKey = (event: KeyboardEvent) => {
    if (event.target !== event.currentTarget) return
    if (event.key === 'Enter' || event.key === ' ') { event.preventDefault(); event.stopPropagation(); onFlip() }
  }
  const source = (event: MouseEvent) => { event.stopPropagation(); onSource?.(card) }
  const face = 'flip-card-face tutor-border relative flex h-full min-h-0 flex-col overflow-hidden rounded-3xl border bg-white px-5 pt-6 pb-5 shadow-sm sm:px-8 sm:pt-8 group-focus-visible:ring-2 group-focus-visible:ring-indigo-400'

  return (
    <div ref={self} role="button" tabIndex={0} aria-pressed={flipped} onClick={onFlip} onKeyDown={onKey} data-autofocus={autoFocus || undefined}
      aria-label={flipped ? 'Kartı ön üzə çevir' : 'Kartı çevir və cavabı gör'}
      className={cn('flip-card group block h-full w-full cursor-pointer text-left outline-none', flipped && 'is-flipped')}>
      <div className="flip-card-inner">
        {/* Front */}
        <div inert={flipped} aria-hidden={flipped} className={face}>
          <span className={cn('absolute inset-x-0 top-0 h-1.5', kindAccent[card.kind])} aria-hidden />
          <Header card={card} topic={topic} />
          <div className="guide-scroll my-4 flex min-h-0 flex-1 flex-col overflow-y-auto overscroll-contain">
            {/* my-auto centers short text; long text starts at the top and scrolls. */}
            <div className="my-auto py-2 text-center">
              <p className="text-[11px] font-semibold tracking-[.12em] text-slate-400 uppercase">{prompt[card.kind]}</p>
              <p className="mx-auto mt-3 max-w-xl text-xl leading-snug font-semibold whitespace-pre-line text-slate-900 sm:text-2xl">{card.front}</p>
            </div>
          </div>
          <div className="flex items-center justify-between gap-2 text-xs text-slate-500">
            <span className="inline-flex items-center gap-1.5"><RotateCw className="size-3.5" aria-hidden />Çevirmək üçün toxun</span>
            {required != null && <Dots streak={card.streak} required={required} mastered={card.mastered} />}
          </div>
        </div>

        {/* Back */}
        <div inert={!flipped} aria-hidden={!flipped} className={cn(face, 'flip-card-back')}>
          <span className={cn('absolute inset-x-0 top-0 h-1.5', kindAccent[card.kind])} aria-hidden />
          <Header card={card} topic={topic} />
          <p className="mt-3 line-clamp-2 shrink-0 text-sm leading-snug font-semibold text-slate-500">{card.front}</p>
          <div className="guide-scroll guide-scroll-fade mt-3 min-h-0 flex-1 overflow-y-auto overscroll-contain pr-3">
            <Back card={card} />
          </div>
          {onSource && !card.own && (
            <button type="button" onClick={source} className="mt-3 inline-flex min-h-9 shrink-0 items-center gap-1.5 self-start rounded-lg text-sm font-semibold text-indigo-700 hover:underline">
              <FileText className="size-4" aria-hidden />Sənəddə göstər{card.page != null ? ` · səh. ${card.page}` : ''}
            </button>
          )}
        </div>
      </div>
    </div>
  )
}

function Header({ card, topic }: { card: StudyCard; topic: string }) {
  return (
    <div className="flex items-start gap-3">
      {/* The topic, and the subsection the card was taken from when that is narrower. */}
      <div className="min-w-0 flex-1" title={card.section ? `${topic} › ${card.section}` : topic}>
        <p className={cn('text-sm leading-snug font-semibold text-slate-600', card.section ? 'line-clamp-1' : 'line-clamp-2')}>{topic}</p>
        {card.section && <p className="mt-0.5 line-clamp-1 text-xs leading-snug font-medium text-slate-500">{card.section}</p>}
      </div>
      <div className="flex shrink-0 flex-col items-end gap-1">
        <KindBadge card={card} />
        <ReviewBadge card={card} />
      </div>
    </div>
  )
}

/** "Yoxlanmalı": the extraction is unsure of the card; "Düzəldilib": the student rewrote it. */
export function ReviewBadge({ card }: { card: StudyCard }) {
  if (card.edited) return <span className="text-[11px] font-medium text-slate-500">Düzəldilib</span>
  if (!card.needsReview) return null
  return (
    <span className="rounded-full bg-amber-50 px-2 py-0.5 text-[11px] font-semibold text-amber-800"
      title="Bu kart avtomatik çıxarılıb və səhv ola bilər. Yoxlayın: redaktə edin, gizlədin və ya silin.">Yoxlanmalı</span>
  )
}

/** "Sual 12", "Anlayış", "Siyahı", "Test 45". */
export function KindBadge({ card }: { card: StudyCard }) {
  return (
    <span className={cn('shrink-0 rounded-full px-2.5 py-0.5 text-xs font-semibold', kindTone[card.kind])}>
      {card.isSection ? 'Bölmə' : kindLabel[card.kind]}{!card.isSection && (card.kind === 'Question' || card.kind === 'Test') ? ` ${card.source}` : ''}
    </span>
  )
}

/** The question side's prompt, for previews of a card. */
export function KindPrompt({ card }: { card: StudyCard }) {
  return <>{prompt[card.kind]}</>
}

/** Thin colored line that marks the card kind. */
export function KindAccent({ card }: { card: StudyCard }) {
  return <span className={cn('absolute inset-x-0 top-0 h-1', kindAccent[card.kind])} aria-hidden />
}

function Back({ card }: { card: StudyCard }) {
  // A card the student wrote or rewrote is plain text, whatever kind it was built as.
  if (card.edited || card.kind === 'Custom') {
    return <p className="text-[15px] leading-relaxed whitespace-pre-line text-slate-800 sm:text-base">{card.back}</p>
  }
  if (card.kind === 'Table') {
    return (
      <>
        <p className="text-sm font-semibold text-rose-700">{card.back}</p>
        <ul className="mt-3 space-y-3 text-[15px] leading-relaxed text-slate-800">
          {card.items.map((item, i) => {
            const colon = item.indexOf(':')
            return <li key={i} className="break-words">{colon > 0 ? <><strong>{item.slice(0, colon)}:</strong>{item.slice(colon + 1)}</> : item}</li>
          })}
        </ul>
      </>
    )
  }
  if (card.kind === 'List') {
    return (
      <>
        <p className="text-xs font-semibold text-amber-800">{card.items.length} bənd</p>
        <ol className="mt-2 space-y-2">
          {card.items.map((item, i) => (
            <li key={item} className="flex gap-3 text-[15px] leading-relaxed text-slate-800">
              <span className="grid size-6 shrink-0 place-items-center rounded-full bg-amber-50 text-xs font-semibold text-amber-800">{i + 1}</span>
              <span className="min-w-0 pt-0.5">{item}</span>
            </li>
          ))}
        </ol>
      </>
    )
  }
  if (card.kind === 'Test') {
    return (
      <>
        <p className="flex gap-2 rounded-2xl bg-emerald-50 px-4 py-3 text-[15px] font-semibold text-emerald-800">
          <Check className="mt-0.5 size-5 shrink-0" aria-hidden />{card.back}
        </p>
        {card.items.map(note => <p key={note} className="mt-3 text-[15px] leading-relaxed text-slate-700">{note}</p>)}
        {card.others.length > 0 && (
          <>
            <p className="mt-4 text-xs font-semibold text-slate-500">Digər variantlar</p>
            <ul className="mt-1.5 space-y-1.5">
              {card.others.map(option => (
                <li key={option} className="flex gap-2 text-sm leading-relaxed text-slate-500"><X className="mt-0.5 size-4 shrink-0 text-rose-400" aria-hidden />{option}</li>
              ))}
            </ul>
          </>
        )}
      </>
    )
  }
  return (
    <>
      {card.kind === 'Question' ? <QuestionAnswer key={`${card.id}:${card.back}`} text={card.back} />
        : <p className="text-[15px] leading-relaxed whitespace-pre-line text-slate-800 sm:text-base">{card.back}</p>}
      {card.kind === 'Question' && card.items.length > 0 && (
        <>
          <p className="mt-4 text-xs font-semibold text-slate-500">Bu cavabdakı anlayışlar</p>
          <div className="mt-1.5 flex flex-wrap gap-1.5">
            {card.items.map(term => <span key={term} className="rounded-full bg-emerald-50 px-2.5 py-1 text-xs font-medium text-emerald-700">{term}</span>)}
          </div>
        </>
      )}
    </>
  )
}

function QuestionAnswer({ text }: { text: string }) {
  const { preview, rest } = splitCardAnswer(text)
  return (
    <div className="text-[15px] leading-relaxed text-slate-800 sm:text-base">
      <p className="whitespace-pre-line">{preview}</p>
      {rest && (
        <details className="mt-4" onClick={event => event.stopPropagation()}>
          <summary className="cursor-pointer text-sm font-semibold text-indigo-700">Tam cavabı göstər</summary>
          <p className="mt-3 whitespace-pre-line">{rest}</p>
        </details>
      )}
    </div>
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
