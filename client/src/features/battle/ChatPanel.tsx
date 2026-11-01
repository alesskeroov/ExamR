import { MessageCircle, MicOff, Send } from 'lucide-react'
import { useEffect, useRef, useState, type FormEvent } from 'react'
import { cn } from '../../lib/utils'
import { MAX_MESSAGE_LENGTH, type ChatMessage } from './api'

interface ChatPanelProps {
  messages: ChatMessage[]
  myUserId: string
  muted: boolean
  disabled: boolean
  onSend: (text: string) => Promise<boolean>
  className?: string
}

const time = (iso: string) => new Date(iso).toLocaleTimeString('az-AZ', { hour: '2-digit', minute: '2-digit' })

/** Room chat. Text is rendered as plain React text, so HTML/script in messages is shown, never executed. */
export function ChatPanel({ messages, myUserId, muted, disabled, onSend, className }: ChatPanelProps) {
  const [text, setText] = useState('')
  const [sending, setSending] = useState(false)
  const listRef = useRef<HTMLOListElement>(null)

  useEffect(() => {
    const list = listRef.current
    if (list) list.scrollTop = list.scrollHeight
  }, [messages])

  async function submit(event: FormEvent) {
    event.preventDefault()
    const trimmed = text.trim()
    if (!trimmed || sending) return
    setSending(true)
    if (await onSend(trimmed)) setText('')
    setSending(false)
  }

  return (
    <section aria-label="Otaq çatı" className={cn('flex min-h-0 flex-col rounded-xl border border-slate-200 bg-white shadow-sm', className)}>
      <h2 className="border-b border-slate-200 px-4 py-3 font-semibold">Chat</h2>

      <ol ref={listRef} aria-live="polite" className="flex min-h-0 flex-1 flex-col gap-3 overflow-y-auto px-4 py-3">
        {messages.length === 0 && <li className="m-auto text-center text-slate-400">Hələ mesaj yoxdur. Salam deyin!</li>}
        {messages.map((m) => {
          const mine = m.userId === myUserId
          return (
            <li key={m.id} className={cn('flex max-w-[85%] flex-col', mine ? 'items-end self-end' : 'items-start')}>
              <span className="mb-0.5 text-xs text-slate-500">
                {mine ? 'Siz' : m.displayName} · <time dateTime={m.sentAt}>{time(m.sentAt)}</time>
              </span>
              <p
                className={cn(
                  'rounded-2xl px-3 py-2 break-words whitespace-pre-wrap',
                  mine ? 'rounded-br-sm bg-indigo-600 text-white' : 'rounded-bl-sm bg-slate-100 text-slate-900',
                )}
              >
                {m.text}
              </p>
            </li>
          )
        })}
      </ol>

      {muted ? (
        <p className="flex items-center gap-2 border-t border-slate-200 px-4 py-3 text-amber-800">
          <MicOff className="size-4" aria-hidden />
          Host sizi səssiz rejimə keçirib.
        </p>
      ) : (
        <form onSubmit={submit} className="flex items-end gap-2 border-t border-slate-200 p-3">
          <label className="min-w-0 flex-1">
            <span className="sr-only">Mesaj</span>
            <input
              value={text}
              onChange={(e) => setText(e.target.value)}
              maxLength={MAX_MESSAGE_LENGTH}
              disabled={disabled}
              placeholder="Mesaj yazın…"
              className="h-10 w-full rounded-lg border border-slate-300 bg-white px-3 focus:border-indigo-500 focus:ring-2 focus:ring-indigo-500/20 focus:outline-none disabled:bg-slate-50"
            />
          </label>
          <button
            type="submit"
            disabled={disabled || sending || !text.trim()}
            aria-label="Göndər"
            className="flex size-10 shrink-0 items-center justify-center rounded-lg bg-indigo-600 text-white hover:bg-indigo-700 disabled:bg-indigo-300"
          >
            <Send className="size-4" aria-hidden />
          </button>
        </form>
      )}
      {!muted && text.length > MAX_MESSAGE_LENGTH - 50 && (
        <p className="px-4 pb-2 text-right text-xs text-slate-500 tabular-nums">
          {text.length}/{MAX_MESSAGE_LENGTH}
        </p>
      )}
    </section>
  )
}

/** Mobile only: floating button that opens the chat sheet, with an unread counter. */
export function BattleMessageButton({ unread, onClick }: { unread: number; onClick: () => void }) {
  return (
    <button
      type="button"
      onClick={onClick}
      aria-label={unread > 0 ? `Chat, ${unread} yeni mesaj` : 'Chat'}
      className="fixed right-4 bottom-4 z-30 flex size-14 items-center justify-center rounded-full bg-indigo-600 text-white shadow-lg hover:bg-indigo-700 lg:hidden"
    >
      <MessageCircle className="size-6" aria-hidden />
      {unread > 0 && (
        <span className="absolute -top-1 -right-1 flex min-w-6 items-center justify-center rounded-full bg-rose-500 px-1.5 text-xs font-semibold tabular-nums">
          {unread > 99 ? '99+' : unread}
        </span>
      )}
    </button>
  )
}
