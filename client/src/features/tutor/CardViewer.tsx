import { ChevronLeft, ChevronRight, Eye, EyeOff, Pencil, Trash2 } from 'lucide-react'
import { useRef, useState, type KeyboardEvent, type TouchEvent } from 'react'
import { useDeleteCard, useHideCard, type CardDeck, type StudyCard } from './api'
import { CardEditor } from './CardEditor'
import { FlipCard } from './FlipCard'
import { GuideDialog } from './GuideDialog'
import { useTranslation } from 'react-i18next'

interface Props {
  chatId: string
  deck: CardDeck
  cards: StudyCard[]
  index: number
  onIndex: (index: number) => void
  onClose: () => void
  topicTitle: (key: string) => string
  required: number
  onSource: (card: StudyCard) => void
}

/** One card at reading size: tap or Space flips, ←/→ (or a swipe on phones) moves through the filtered cards. */
export function CardViewer({ chatId, deck, cards, index, onIndex, onClose, topicTitle, required, onSource }: Props) {
  const { t } = useTranslation()
  const [flipped, setFlipped] = useState(false)
  const [editing, setEditing] = useState(false)
  const [confirming, setConfirming] = useState(false)
  const hide = useHideCard(chatId)
  const remove = useDeleteCard(chatId)
  const touch = useRef<{ x: number; y: number } | null>(null)
  const card = cards[index]
  const go = (next: number) => {
    if (next < 0 || next >= cards.length) return
    setFlipped(false)
    onIndex(next)
  }

  const onKey = (event: KeyboardEvent) => {
    if ((event.target as HTMLElement).closest('input, textarea')) return
    if (event.key === 'ArrowRight') { event.preventDefault(); go(index + 1) }
    else if (event.key === 'ArrowLeft') { event.preventDefault(); go(index - 1) }
  }
  const onTouchStart = (event: TouchEvent) => { touch.current = { x: event.touches[0].clientX, y: event.touches[0].clientY } }
  const onTouchEnd = (event: TouchEvent) => {
    const start = touch.current
    touch.current = null
    if (!start) return
    const dx = event.changedTouches[0].clientX - start.x
    const dy = event.changedTouches[0].clientY - start.y
    // A clear horizontal swipe changes the card; vertical movement is scrolling inside a long answer.
    if (Math.abs(dx) > 60 && Math.abs(dx) > Math.abs(dy) * 1.5) go(dx < 0 ? index + 1 : index - 1)
  }

  /** The card leaves the filtered list (deleted, or hidden while hidden cards are not shown): stay on the same position. */
  const leave = () => {
    setConfirming(false)
    setFlipped(false)
    if (cards.length <= 1) onClose()
    else if (index === cards.length - 1) onIndex(index - 1)
  }

  if (!card) return null
  const icon = 'grid size-10 shrink-0 place-items-center rounded-full text-slate-500 hover:bg-slate-100 hover:text-slate-900 disabled:opacity-50'
  const actions = (
    <>
      <button type="button" onClick={() => setEditing(true)} aria-label={t('tutor.card.editTitle')} title={t('questions.edit')} className={icon}>
        <Pencil className="size-[18px]" />
      </button>
      {!card.own && (
        <button type="button" disabled={hide.isPending} aria-label={card.hidden ? t('tutor.card.show') : t('tutor.card.hide')} title={card.hidden ? t('tutor.card.showShort') : t('tutor.card.hideShort')}
          onClick={() => hide.mutate({ id: card.id, hidden: !card.hidden }, { onSuccess: saved => { if (saved.hidden) leave() } })} className={icon}>
          {card.hidden ? <Eye className="size-[18px]" /> : <EyeOff className="size-[18px]" />}
        </button>
      )}
      <button type="button" onClick={() => setConfirming(true)} aria-label={t('tutor.card.delete')} title={t('common.delete')} className={icon}>
        <Trash2 className="size-[18px]" />
      </button>
    </>
  )
  return (
    <GuideDialog title={`Kart ${index + 1} / ${cards.length}`} variant="full" onClose={onClose} actions={actions}>
      <div className="flex min-h-0 flex-1 flex-col" onKeyDown={onKey} onTouchStart={onTouchStart} onTouchEnd={onTouchEnd}>
        {/* The card takes the whole area, so every card has the same frame; its own content scrolls. */}
        <div className="min-h-0 flex-1 px-3 py-3 sm:px-6 sm:py-5">
          <div key={card.id} className="tutor-enter h-full">
            <FlipCard card={card} topic={topicTitle(card.topicKey)} flipped={flipped} onFlip={() => setFlipped(f => !f)}
              required={required} onSource={c => { onClose(); onSource(c) }} autoFocus />
          </div>
        </div>
        {confirming ? (
          <div role="alertdialog" aria-label={t('tutor.card.delete')}
            className="tutor-border flex shrink-0 flex-col gap-2 border-t px-4 pt-3 pb-[max(.75rem,env(safe-area-inset-bottom))] sm:flex-row sm:items-center sm:px-6">
            <p className="flex-1 text-sm text-slate-700">
              {card.own ? t('tutor.card.deleteOwn') : t('tutor.card.deleteFromMine')} {t('tutor.card.irreversible')}
              {remove.isError && <span className="block text-rose-700">{t('tutor.card.deleteFailed')}</span>}
            </p>
            <div className="flex gap-2">
              <button type="button" onClick={() => setConfirming(false)} className="tutor-shared-action flex-1 sm:flex-none sm:px-5">{t('common.cancel')}</button>
              <button type="button" disabled={remove.isPending} onClick={() => remove.mutate(card.id, { onSuccess: leave })}
                className="inline-flex h-11 flex-1 items-center justify-center rounded-xl bg-rose-600 px-5 text-sm font-semibold text-white hover:bg-rose-700 disabled:opacity-60 sm:flex-none">
                {remove.isPending ? t('tutor.card.deleting') : t('common.delete')}
              </button>
            </div>
          </div>
        ) : (
        <nav aria-label={t('tutor.card.navAria')}
          className="tutor-border flex shrink-0 items-center gap-2 border-t px-4 pt-3 pb-[max(.75rem,env(safe-area-inset-bottom))] sm:px-6">
          <button type="button" onClick={() => go(index - 1)} disabled={index === 0}
            className="tutor-shared-action flex-1 sm:flex-none sm:px-5">
            <ChevronLeft className="size-4" aria-hidden />{t('common.previous')}
          </button>
          <span className="hidden flex-1 text-center text-xs text-slate-500 sm:block">{t('tutor.card.keyboard')}</span>
          <button type="button" onClick={() => go(index + 1)} disabled={index === cards.length - 1}
            className="tutor-shared-action flex-1 sm:flex-none sm:px-5">
            {t('common.next')}<ChevronRight className="size-4" aria-hidden />
          </button>
        </nav>
        )}
      </div>
      {editing && <CardEditor chatId={chatId} deck={deck} card={card} onClose={() => setEditing(false)} />}
    </GuideDialog>
  )
}
