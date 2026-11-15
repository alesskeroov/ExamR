import { Plus } from 'lucide-react'
import { useEffect, useRef, useState, type ReactNode } from 'react'
import type { CardDeck } from './api'
import { CardEditor } from './CardEditor'

interface Props {
  chatId: string
  deck: CardDeck
  /** Topic the new card goes into. */
  topicKey?: string
  className?: string
  children: ReactNode
}

const minSelection = 3

/**
 * Text inside can be turned into an own card: select a passage, press "Seçilmiş mətndən kart et", write the question.
 * The selection becomes the answer, so the card says exactly what the material says.
 */
export function SelectionToCard({ chatId, deck, topicKey, className, children }: Props) {
  const box = useRef<HTMLDivElement>(null)
  const [picked, setPicked] = useState('')
  const [answer, setAnswer] = useState<string | null>(null)

  useEffect(() => {
    let clear: ReturnType<typeof setTimeout> | undefined
    const update = () => {
      const selection = document.getSelection()
      const text = selection && !selection.isCollapsed ? selection.toString().trim() : ''
      const inside = !!selection?.anchorNode && !!box.current?.contains(selection.anchorNode)
      clearTimeout(clear)
      if (inside && text.length >= minSelection) setPicked(text)
      // A tap on the button collapses the selection on touch screens first: keep the button long enough to be pressed.
      else clear = setTimeout(() => setPicked(''), 400)
    }
    document.addEventListener('selectionchange', update)
    return () => { clearTimeout(clear); document.removeEventListener('selectionchange', update) }
  }, [])

  return (
    <div ref={box} className={className}>
      {children}
      {picked && answer === null && (
        <div className="pointer-events-none fixed inset-x-0 bottom-4 z-50 flex justify-center px-4">
          <button type="button" onMouseDown={e => e.preventDefault()} onClick={() => { setAnswer(picked); setPicked('') }}
            className="btn-primary pointer-events-auto inline-flex h-11 items-center gap-2 rounded-full px-5 text-sm font-semibold shadow-lg">
            <Plus className="size-4" aria-hidden />Seçilmiş mətndən kart et
          </button>
        </div>
      )}
      {answer !== null && (
        <CardEditor chatId={chatId} deck={deck} topicKey={topicKey} initialBack={answer}
          onClose={() => { setAnswer(null); document.getSelection()?.removeAllRanges() }} />
      )}
    </div>
  )
}
