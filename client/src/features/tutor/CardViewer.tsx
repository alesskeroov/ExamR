import { ChevronLeft, ChevronRight } from 'lucide-react'
import { useRef, useState, type KeyboardEvent, type TouchEvent } from 'react'
import type { StudyCard } from './api'
import { FlipCard } from './FlipCard'
import { GuideDialog } from './GuideDialog'

interface Props {
  cards: StudyCard[]
  index: number
  onIndex: (index: number) => void
  onClose: () => void
  topicTitle: (key: string) => string
  required: number
  onSource: (card: StudyCard) => void
}

/** One card at reading size: tap or Space flips, ←/→ (or a swipe on phones) moves through the filtered cards. */
export function CardViewer({ cards, index, onIndex, onClose, topicTitle, required, onSource }: Props) {
  const [flipped, setFlipped] = useState(false)
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

  if (!card) return null
  return (
    <GuideDialog title={`Kart ${index + 1} / ${cards.length}`} variant="full" onClose={onClose}>
      <div className="flex min-h-0 flex-1 flex-col" onKeyDown={onKey} onTouchStart={onTouchStart} onTouchEnd={onTouchEnd}>
        <div className="min-h-0 flex-1 overflow-y-auto overscroll-contain px-4 py-4 sm:px-6 sm:py-6">
          <div key={card.id} className="tutor-enter">
            <FlipCard card={card} topic={topicTitle(card.topicKey)} flipped={flipped} onFlip={() => setFlipped(f => !f)}
              required={required} onSource={c => { onClose(); onSource(c) }} autoFocus />
          </div>
        </div>
        <nav aria-label="Kartlar arasında keçid"
          className="tutor-border flex shrink-0 items-center gap-2 border-t px-4 pt-3 pb-[max(.75rem,env(safe-area-inset-bottom))] sm:px-6">
          <button type="button" onClick={() => go(index - 1)} disabled={index === 0}
            className="tutor-shared-action flex-1 sm:flex-none sm:px-5">
            <ChevronLeft className="size-4" aria-hidden />Əvvəlki
          </button>
          <span className="hidden flex-1 text-center text-xs text-slate-500 sm:block">← → keçid · Boşluq — çevir · telefonda sürüşdür</span>
          <button type="button" onClick={() => go(index + 1)} disabled={index === cards.length - 1}
            className="tutor-shared-action flex-1 sm:flex-none sm:px-5">
            Növbəti<ChevronRight className="size-4" aria-hidden />
          </button>
        </nav>
      </div>
    </GuideDialog>
  )
}
