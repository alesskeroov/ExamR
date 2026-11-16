import { ImageOff, Plus } from 'lucide-react'
import { useState } from 'react'
import type { CardDeck } from './api'
import { CardEditor } from './CardEditor'
import { useTranslation } from 'react-i18next'

interface Props {
  chatId: string
  documentId: string
  deck: CardDeck
  count: number
}

/** Topic of the own cards written from picture slides (see the server's CardDeckService.PictureTopicKey). */
const slidesTopic = 'slides'

/**
 * A presentation whose slides are pictures: the slides are shown one under another (loaded as they scroll into view),
 * and the student turns what matters into own cards.
 */
export function SlideGallery({ chatId, documentId, deck, count }: Props) {
  const { t } = useTranslation()
  const [slide, setSlide] = useState<number | null>(null)
  const [broken, setBroken] = useState<Set<number>>(() => new Set())
  const numbers = Array.from({ length: count }, (_, i) => i + 1)

  return (
    <section aria-label={t('tutor.slides.aria')} className="flex flex-col gap-4">
      {numbers.map(n => (
        <figure key={n} className="tutor-border overflow-hidden rounded-2xl border bg-white">
          {broken.has(n) ? (
            <div className="flex aspect-video flex-col items-center justify-center gap-2 bg-slate-50 text-sm text-slate-500">
              <ImageOff className="size-6" aria-hidden />{t('tutor.slides.missing')}
            </div>
          ) : (
            <img src={`/api/learning/documents/${documentId}/slides/${n}`} alt={`Slayd ${n}`} loading="lazy" decoding="async"
              onError={() => setBroken(current => new Set(current).add(n))} className="block aspect-video w-full bg-slate-50 object-contain" />
          )}
          <figcaption className="tutor-border flex items-center gap-2 border-t px-3 py-2">
            <span className="flex-1 text-sm font-medium text-slate-700">Slayd {n}</span>
            <button type="button" onClick={() => setSlide(n)}
              className="inline-flex h-9 items-center gap-1.5 rounded-lg px-3 text-sm font-semibold text-indigo-700 hover:bg-indigo-50">
              <Plus className="size-4" aria-hidden />Kart yarat
            </button>
          </figcaption>
        </figure>
      ))}
      {slide !== null && (
        <CardEditor chatId={chatId} deck={deck} topicKey={slidesTopic} initialFront={`Slayd ${slide}: `} onClose={() => setSlide(null)} />
      )}
    </section>
  )
}
