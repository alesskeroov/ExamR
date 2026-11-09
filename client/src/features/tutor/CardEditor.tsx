import { useId, useState, type FormEvent } from 'react'
import { Alert } from '../../components/common/Alert'
import { ApiError } from '../../lib/api-client'
import { topicStats, useSaveCard, type CardDeck, type StudyCard } from './api'
import { GuideDialog } from './GuideDialog'
import { TopicPicker } from './TopicPicker'

interface Props {
  chatId: string
  deck: CardDeck
  /** The card to rewrite; none = a new own card. */
  card?: StudyCard
  /** Topic a new card starts in. */
  topicKey?: string
  onClose: () => void
  onSaved?: (card: StudyCard) => void
}

const maxFront = 500
const maxBack = 4000

/** A card's text as one block: lists, table rows and test answers become plain lines the student can rewrite. */
function startBack(card?: StudyCard) {
  if (!card) return ''
  return [card.back, ...card.items].filter(Boolean).join('\n')
}

function message(error: unknown) {
  if (error instanceof ApiError) return Object.values(error.problem?.errors ?? {}).flat()[0] ?? error.message
  return 'Kart yadda saxlanmadı. Yenidən yoxlayın.'
}

/** Writes a new card or rewrites one: a simple question in front and its answer behind. */
export function CardEditor({ chatId, deck, card, topicKey = '', onClose, onSaved }: Props) {
  const [front, setFront] = useState(card?.front ?? '')
  const [back, setBack] = useState(startBack(card))
  const [topic, setTopic] = useState(card ? card.topicKey : topicKey)
  const save = useSaveCard(chatId)
  const frontId = useId()
  const backId = useId()
  const valid = front.trim().length >= 2 && back.trim().length >= 1

  const submit = (event: FormEvent) => {
    event.preventDefault()
    if (!valid || save.isPending) return
    save.mutate({ id: card?.id, front: front.trim(), back: back.trim(), topicKey: card ? undefined : topic || null }, {
      onSuccess: saved => { onSaved?.(saved); onClose() },
    })
  }

  // Own cards without a topic live under "Mənim kartlarım"; it is not offered as a topic to pick.
  const topics = deck.topics.filter(t => t.key !== 'own')
  return (
    <GuideDialog title={card ? 'Kartı redaktə et' : 'Yeni kart'} onClose={onClose}>
      <form onSubmit={submit} className="guide-scroll flex min-h-0 flex-col gap-4 overflow-y-auto px-4 py-4 sm:px-5">
        {card && !card.own && (
          <p className="text-sm text-slate-600">Dəyişiklik yalnız sizin kartınızda görünür; sənəd və digər istifadəçilərin kartları dəyişmir.</p>
        )}
        {!card && (
          <div className="flex flex-col gap-1.5">
            <span className="text-sm font-semibold text-slate-700">Mövzu</span>
            <TopicPicker topics={topics} stats={topicStats(deck)} value={topic} onChange={setTopic} allLabel="Mövzusuz — «Mənim kartlarım»" />
          </div>
        )}
        <div className="flex flex-col gap-1.5">
          <label htmlFor={frontId} className="text-sm font-semibold text-slate-700">Ön üz — sual</label>
          <textarea id={frontId} value={front} onChange={e => setFront(e.target.value)} maxLength={maxFront} rows={3} required autoFocus
            placeholder="Məsələn: NAT nə edir?"
            className="tutor-border min-h-20 resize-y rounded-xl border bg-white px-3 py-2 text-base text-slate-900 outline-none focus:border-indigo-400 sm:text-sm" />
          <span className="self-end text-xs text-slate-500">{front.length} / {maxFront}</span>
        </div>
        <div className="flex flex-col gap-1.5">
          <label htmlFor={backId} className="text-sm font-semibold text-slate-700">Arxa üz — cavab</label>
          <textarea id={backId} value={back} onChange={e => setBack(e.target.value)} maxLength={maxBack} rows={6} required
            className="tutor-border min-h-32 resize-y rounded-xl border bg-white px-3 py-2 text-base text-slate-900 outline-none focus:border-indigo-400 sm:text-sm" />
          <span className="self-end text-xs text-slate-500">{back.length} / {maxBack}</span>
        </div>
        {save.isError && <Alert title="Yadda saxlanmadı">{message(save.error)}</Alert>}
        <div className="flex flex-col-reverse gap-2 sm:flex-row sm:justify-end">
          <button type="button" onClick={onClose} className="tutor-shared-action sm:px-5">Ləğv et</button>
          <button type="submit" disabled={!valid || save.isPending}
            className="btn-primary inline-flex h-11 items-center justify-center rounded-xl px-5 text-sm font-semibold disabled:opacity-60">
            {save.isPending ? 'Saxlanılır…' : 'Yadda saxla'}
          </button>
        </div>
      </form>
    </GuideDialog>
  )
}
