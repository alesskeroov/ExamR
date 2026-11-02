import {
  Check,
  Copy,
  Crown,
  Loader2,
  LogOut,
  Mic,
  MicOff,
  UserX,
  Wifi,
  WifiOff,
  X,
} from 'lucide-react'
import { useEffect, useState, type ReactNode } from 'react'
import { Link, useNavigate, useParams } from 'react-router'
import { Alert } from '../../components/common/Alert'
import { Button } from '../../components/common/Button'
import { formatNumber } from '../../lib/format'
import { useConfirm } from '../../lib/use-confirm'
import { cn } from '../../lib/utils'
import { NotificationBell } from '../friends/NotificationBell'
import { BattleMessageButton, ChatPanel } from './ChatPanel'
import { RoomInviteModal } from './RoomInviteModal'
import { useBattleRoom, type BattleParticipant, type BattleState, type ConnectionStatus } from './api'

type Room = ReturnType<typeof useBattleRoom>

export function BattleRoomPage() {
  const { code = '' } = useParams()
  const navigate = useNavigate()
  const confirm = useConfirm()
  const room = useBattleRoom(code.toUpperCase())
  const [chatOpen, setChatOpen] = useState(false)
  const [seenMessages, setSeenMessages] = useState(0)

  const state = room.state
  const myId = room.myUserId
  const isHost = state?.hostUserId === myId
  const myself = state?.participants.find((p) => p.userId === myId)
  const unread = chatOpen ? 0 : room.messages.length - seenMessages

  // Messages count as read while the sheet is open; remember the count when it closes.
  const openChat = () => setChatOpen(true)
  const closeChat = () => {
    setSeenMessages(room.messages.length)
    setChatOpen(false)
  }

  async function leave() {
    await room.leave()
    navigate('/battle/rooms')
  }

  if (room.fatal) {
    return (
      <Centered>
        <Alert title="Otağa qoşulmaq mümkün olmadı">{room.fatal}</Alert>
        <Link to="/battle" className="mt-4 inline-flex h-10 items-center rounded-lg btn-primary px-4 font-medium">
          Battle səhifəsinə qayıt
        </Link>
      </Centered>
    )
  }

  if (!state || !myId) {
    return (
      <Centered>
        <Loader2 className="size-6 animate-spin text-slate-400" aria-label="Qoşulur" />
        <p className="mt-3 text-slate-500">Otağa qoşulur…</p>
      </Centered>
    )
  }

  const chat = (className?: string) => (
    <ChatPanel
      messages={room.messages}
      myUserId={myId}
      muted={!!myself?.isMuted}
      disabled={room.status !== 'connected'}
      onSend={room.send}
      className={className}
    />
  )

  return (
    <div className="flex min-h-dvh flex-col bg-slate-50">
      <header className="sticky top-0 z-20 border-b border-slate-200 bg-white/95 backdrop-blur">
        <div className="mx-auto flex h-14 max-w-[1200px] items-center gap-3 px-4">
          <Link to="/" aria-label="ExamR ana səhifə" className="shrink-0">
            <img src="/favicon.svg" alt="" className="size-7" />
          </Link>
          <div className="min-w-0 flex-1">
            <p className="truncate font-semibold">{state.name}</p>
            <p className="truncate text-xs text-slate-500" title={state.settings.documentName}>
              Otaq <span className="font-mono font-semibold tracking-wider text-slate-700">{state.code}</span> · suallar{' '}
              {state.settings.rangeFrom}–{state.settings.rangeTo} · {state.settings.documentName}
            </p>
          </div>
          <ConnectionBadge status={room.status} />
          <NotificationBell />
          <Button variant="ghost" className="h-9 px-2 sm:px-3" onClick={leave}>
            <LogOut className="size-4" aria-hidden />
            <span className="hidden sm:inline">Çıx</span>
          </Button>
        </div>
      </header>

      <div className="mx-auto grid w-full max-w-[1200px] flex-1 gap-6 px-4 py-6 lg:grid-cols-[minmax(0,1fr)_340px]">
        <main className="flex min-w-0 flex-col gap-4">
          <div className="flex items-center justify-between gap-3">
            <Link to="/battle/rooms" className="text-sm font-medium text-slate-600 hover:text-indigo-600">Otaqlarım</Link>
            {isHost && <Button variant="ghost" className="h-8 px-2 text-xs" onClick={async () => {
              if (await confirm({ title: 'Otaq bağlansın?', description: 'Otaq bütün iştirakçılar üçün bağlanacaq.', confirmLabel: 'Otağı bağla', danger: true }) && await room.closeRoom()) navigate('/battle/rooms')
            }}>Otağı bağla</Button>}
          </div>
          {room.actionError && (
            <Alert title={room.actionError} action={<Button variant="ghost" onClick={room.clearActionError}>Bağla</Button>} />
          )}
          {state.status === 'Lobby' && <Lobby state={state} room={room} isHost={isHost} myId={myId} />}
          {(state.status === 'Question' || state.status === 'Reveal') && (
            <Stage state={state} room={room} isHost={isHost} myId={myId} />
          )}
          {state.status === 'Finished' && <FinalResults state={state} myId={myId} />}
        </main>

        <aside className="hidden min-h-0 lg:sticky lg:top-20 lg:flex lg:h-[calc(100dvh-7rem)]">{chat('w-full')}</aside>
      </div>

      {/* Mobile: chat in a bottom sheet. */}
      <BattleMessageButton unread={unread} onClick={openChat} />
      {chatOpen && (
        <div className="fixed inset-0 z-40 flex flex-col justify-end bg-slate-900/40 lg:hidden" onClick={closeChat}>
          <div className="relative h-[75dvh]" onClick={(e) => e.stopPropagation()}>
            <button
              type="button"
              onClick={closeChat}
              aria-label="Çatı bağla"
              className="absolute top-2.5 right-3 z-10 rounded-lg p-1.5 text-slate-500 hover:bg-slate-100"
            >
              <X className="size-5" />
            </button>
            {chat('h-full rounded-b-none')}
          </div>
        </div>
      )}
    </div>
  )
}

function Lobby({ state, room, isHost, myId }: { state: BattleState; room: Room; isHost: boolean; myId: string }) {
  const [copied, setCopied] = useState(false)
  const [inviteOpen, setInviteOpen] = useState(false)
  const online = state.participants.filter((p) => p.isConnected).length
  const link = `${window.location.origin}/battle/${state.code}`

  async function copy() {
    try {
      await navigator.clipboard.writeText(link)
      setCopied(true)
      setTimeout(() => setCopied(false), 2000)
    } catch {
      // Clipboard blocked: the code is still visible.
    }
  }

  return (
    <>
      <section className="rounded-2xl bg-gradient-to-br from-indigo-600 to-violet-600 p-6 text-center text-white shadow-sm sm:p-8">
        <p className="text-indigo-100">Otaq kodu</p>
        <p className="mt-1 font-mono text-5xl font-bold tracking-[0.25em] sm:text-6xl">{state.code}</p>
        <button
          type="button"
          onClick={copy}
          className="mt-4 inline-flex items-center gap-2 rounded-lg bg-white/15 px-4 py-2 font-medium hover:bg-white/25"
        >
          {copied ? <Check className="size-4" aria-hidden /> : <Copy className="size-4" aria-hidden />}
          {copied ? 'Link kopyalandı' : 'Dəvət linkini kopyala'}
        </button>
        <p className="mt-4 text-sm text-indigo-100">
          {state.settings.questionCount} sual · hər suala {state.settings.secondsPerQuestion} saniyə
        </p>
        <button type="button" onClick={() => setInviteOpen(true)} className="mt-4 block w-full text-sm font-medium underline underline-offset-4">Dost dəvət et</button>
      </section>
      {inviteOpen && <RoomInviteModal code={state.code} isFull={state.participants.length >= 10} onClose={() => setInviteOpen(false)} />}

      <section className="rounded-xl border border-slate-200 bg-white p-5 shadow-sm">
        <div className="mb-4 flex items-center justify-between gap-3">
          <h2 className="font-semibold">
            İştirakçılar <span className="text-slate-400 tabular-nums">{state.participants.length}/10</span>
          </h2>
          {isHost ? (
            <Button onClick={room.start} disabled={online < 2}>
              Yarışı başlat
            </Button>
          ) : (
            <p className="flex items-center gap-2 text-slate-500">
              <Loader2 className="size-4 animate-spin" aria-hidden />
              Host-un başlatması gözlənilir…
            </p>
          )}
        </div>
        {isHost && online < 2 && <p className="mb-4 text-slate-500">Başlamaq üçün ən azı 2 onlayn iştirakçı lazımdır. Kodu dostlarınızla paylaşın.</p>}
        <ul className="grid gap-2 sm:grid-cols-2">
          {state.participants.map((p) => (
            <li key={p.userId} className="flex flex-wrap items-center gap-3 rounded-lg border border-slate-200 px-3 py-2.5">
              <Avatar name={p.displayName} src={p.avatarUrl} />
              <span className={cn('min-w-0 flex-1', !p.isConnected && 'text-slate-400')}>
                <span className="flex items-center gap-1.5 truncate font-medium">
                  {p.displayName}
                  {p.userId === myId && <span className="text-slate-400">(siz)</span>}
                  {p.isHost && <Crown className="size-4 shrink-0 text-amber-500" aria-label="Host" />}
                  {p.isMuted && <MicOff className="size-4 shrink-0 text-slate-400" aria-label="Səssiz" />}
                </span>
                <span className="text-xs text-slate-500">{p.isConnected ? 'onlayn' : 'oflayn'}</span>
              </span>
              {isHost && !p.isHost && <HostActions participant={p} room={room} />}
            </li>
          ))}
        </ul>
      </section>
    </>
  )
}

function HostActions({ participant, room }: { participant: BattleParticipant; room: Room }) {
  const confirm = useConfirm()
  return (
    <span className="flex shrink-0 gap-1">
      <button type="button" className="rounded-lg px-2 py-1.5 text-xs font-medium text-slate-600 hover:bg-slate-100"
        aria-label={`${participant.displayName}: Host et`} onClick={async () => {
          if (await confirm({ title: 'Host səlahiyyəti ötürülsün?', description: `${participant.displayName} yeni Host olacaq. Siz adi iştirakçı olacaqsınız.`, confirmLabel: 'Host et' })) void room.transferHost(participant.userId)
        }}>Host et</button>
      <button
        type="button"
        onClick={() => room.setMuted(participant.userId, !participant.isMuted)}
        className="rounded-lg p-1.5 text-slate-500 hover:bg-slate-100 hover:text-slate-900"
        aria-label={participant.isMuted ? `${participant.displayName}: səsi aç` : `${participant.displayName}: səssiz et`}
        title={participant.isMuted ? 'Səsi aç' : 'Səssiz et'}
      >
        {participant.isMuted ? <Mic className="size-4" /> : <MicOff className="size-4" />}
      </button>
      <button
        type="button"
        onClick={async () => {
          if (await confirm({ title: 'İştirakçı çıxarılsın?', description: `${participant.displayName} otaqdan çıxarılacaq.`, confirmLabel: 'Otaqdan çıxar', danger: true })) void room.kick(participant.userId)
        }}
        className="rounded-lg p-1.5 text-slate-500 hover:bg-rose-50 hover:text-rose-600"
        aria-label={`${participant.displayName}: otaqdan çıxar`}
        title="Otaqdan çıxar"
      >
        <UserX className="size-4" />
      </button>
    </span>
  )
}

function Stage({ state, room, isHost, myId }: { state: BattleState; room: Room; isHost: boolean; myId: string }) {
  const question = state.question!
  const reveal = state.reveal
  const mine = state.participants.find((p) => p.userId === myId)
  const answered = room.myAnswerOptionId
  const online = state.participants.filter((p) => p.isConnected).length
  const isLast = question.index + 1 === question.total

  return (
    <>
      <div className="flex flex-wrap items-center justify-between gap-3">
        <span className="text-sm font-medium text-slate-700 tabular-nums">
          Sual {question.index + 1} / {question.total}
        </span>
        {!reveal && (
          <span className="text-slate-500 tabular-nums">
            {state.answeredCount}/{online} cavab verdi
          </span>
        )}
        {isHost && (
          <Button variant="secondary" className="h-9" onClick={room.advance}>
            {reveal ? (isLast ? 'Nəticələr' : 'Növbəti sual') : 'Sualı bitir'}
          </Button>
        )}
      </div>

      {!reveal && <Countdown endsAt={question.endsAt} seconds={question.secondsPerQuestion} offset={room.serverOffset} />}

      <section className="rounded-xl border border-slate-200 bg-white p-5 sm:p-6">
        <h1 className="text-lg leading-8 font-medium break-words text-slate-900 sm:text-xl">{question.text}</h1>
      </section>

      <div role="radiogroup" aria-label="Variantlar" className="flex flex-col gap-2.5">
        {question.options.map((option) => {
          const isCorrect = reveal?.correctOptionId === option.id
          const isMine = answered === option.id
          const isWrongPick = !!reveal && isMine && !isCorrect

          return (
            <button
              key={option.id}
              type="button"
              role="radio"
              aria-checked={isMine}
              disabled={!!reveal || answered !== null}
              onClick={() => room.answer(question.index, option.id)}
              className={cn(
                'flex min-h-16 w-full items-start gap-3 rounded-xl border px-4 py-4 text-left text-sm text-slate-700 transition-colors duration-150 motion-reduce:transition-none sm:gap-4 sm:px-5 sm:text-[15px]',
                isCorrect ? 'border-emerald-300 bg-emerald-50/60'
                  : isWrongPick ? 'border-rose-300 bg-rose-50/60'
                    : isMine ? 'border-indigo-400 bg-indigo-50/60'
                      : 'border-slate-200 bg-white',
                !reveal && answered === null && 'hover:border-slate-400 hover:bg-slate-50',
                'disabled:cursor-default',
              )}
            >
              <span className={cn(
                'flex size-7 shrink-0 items-center justify-center rounded-md border text-xs font-medium',
                isCorrect ? 'border-emerald-200 bg-emerald-50 text-emerald-800'
                  : isWrongPick ? 'border-rose-200 bg-rose-50 text-rose-800'
                    : isMine ? 'border-indigo-200 bg-indigo-50 text-indigo-700'
                      : 'border-slate-200 bg-slate-50 text-slate-500',
              )} aria-hidden>{option.label}</span>
              <span className="min-w-0 flex-1 pt-0.5 leading-6 break-words">{option.text}</span>
              {isCorrect && <Check className="mt-1 size-4 shrink-0 text-emerald-700" aria-label="Düzgün cavab" />}
              {isWrongPick && <X className="mt-1 size-4 shrink-0 text-rose-700" aria-label="Sizin cavabınız, yanlış" />}
              {isMine && !reveal && <Check className="mt-1 size-4 shrink-0 text-indigo-600" aria-label="Seçilmiş cavab" />}
            </button>
          )
        })}
      </div>

      {!reveal && answered && (
        <p role="status" className="text-center font-medium text-slate-600">
          Cavabınız qəbul edildi — digərləri gözlənilir…
        </p>
      )}

      {reveal && mine && (
        <div
          role="status"
          className={cn(
            'rounded-xl px-4 py-3 text-sm font-medium',
            mine.lastCorrect ? 'bg-emerald-50 text-emerald-800' : 'bg-rose-50 text-rose-800',
          )}
        >
          {mine.lastCorrect ? `Düzgün! +${formatNumber(mine.lastPoints ?? 0)} xal` : answered ? 'Yanlış cavab · 0 xal' : 'Cavab verilmədi · 0 xal'}
        </div>
      )}

      <Leaderboard participants={state.participants} myId={myId} room={room} isHost={isHost} />
    </>
  )
}

/** Shrinking bar + seconds left, synced to the server deadline. */
function Countdown({ endsAt, seconds, offset }: { endsAt: string; seconds: number; offset: number }) {
  const remaining = () => Math.max(0, Date.parse(endsAt) - (Date.now() + offset))
  const [left, setLeft] = useState(remaining)

  useEffect(() => {
    const timer = window.setInterval(() => setLeft(Math.max(0, Date.parse(endsAt) - (Date.now() + offset))), 100)
    return () => window.clearInterval(timer)
  }, [endsAt, offset])

  const fraction = Math.min(1, left / (seconds * 1000))
  const secondsLeft = Math.ceil(left / 1000)

  return (
    <div className="flex items-center gap-3" role="timer" aria-label={`${secondsLeft} saniyə qalıb`}>
      <div className="h-1.5 flex-1 overflow-hidden rounded-full bg-slate-200">
        <div
          className={cn('h-full rounded-full transition-[width] duration-100 ease-linear', secondsLeft <= 5 ? 'bg-rose-500' : 'bg-indigo-600')}
          style={{ width: `${fraction * 100}%` }}
        />
      </div>
      <span className={cn('w-10 text-right text-lg font-medium tabular-nums', secondsLeft <= 5 ? 'text-rose-600' : 'text-slate-700')}>
        {secondsLeft}
      </span>
    </div>
  )
}

function Leaderboard({ participants, myId, room, isHost }: { participants: BattleParticipant[]; myId: string; room?: Room; isHost?: boolean }) {
  return (
    <section className="rounded-xl border border-slate-200 bg-white p-4 shadow-sm sm:p-5">
      <h2 className="mb-3 font-semibold">Liderlər cədvəli</h2>
      <ol className="flex flex-col gap-1.5">
        {participants.map((p) => (
          <li
            key={p.userId}
            className={cn('flex flex-wrap items-center gap-3 rounded-lg px-3 py-2', p.userId === myId ? 'bg-indigo-50' : 'bg-slate-50')}
          >
            <span className="w-6 text-center font-bold text-slate-500 tabular-nums">{p.rank}</span>
            <Avatar name={p.displayName} small />
            <span className={cn('min-w-0 flex-1 truncate font-medium', !p.isConnected && 'text-slate-400')}>{p.displayName}</span>
            {p.lastPoints !== null && (
              <span className={cn('text-sm font-semibold tabular-nums', p.lastPoints > 0 ? 'text-emerald-600' : 'text-slate-400')}>
                +{formatNumber(p.lastPoints)}
              </span>
            )}
            <span className="w-16 text-right font-bold tabular-nums">{formatNumber(p.score)}</span>
            {p.isHost && <Crown className="size-4 text-amber-500" aria-label="Host" />}
            {isHost && room && !p.isHost && <HostActions participant={p} room={room} />}
          </li>
        ))}
      </ol>
    </section>
  )
}

function FinalResults({ state, myId }: { state: BattleState; myId: string }) {
  const [first, second, third] = state.participants
  const mine = state.participants.find((p) => p.userId === myId)
  const podium = [
    { p: second, place: 2, height: 'h-24', color: 'bg-slate-300' },
    { p: first, place: 1, height: 'h-32', color: 'bg-amber-400' },
    { p: third, place: 3, height: 'h-16', color: 'bg-orange-300' },
  ]

  return (
    <>
      <section className="rounded-2xl bg-gradient-to-br from-indigo-600 to-violet-600 p-6 text-white shadow-sm">
        <h1 className="text-center text-2xl font-bold">Yarış bitdi!</h1>
        {mine && (
          <p className="mt-1 text-center text-indigo-100">
            Siz {mine.rank}-ci yerdəsiniz · {formatNumber(mine.score)} xal · {mine.correctCount}/{state.settings.questionCount} düzgün
          </p>
        )}
        <div className="mt-6 flex items-end justify-center gap-3" aria-label="TOP 3">
          {podium.map(({ p, place, height, color }) =>
            p ? (
              <div key={place} className="flex w-28 flex-col items-center gap-2">
                {place === 1 && <Crown className="size-7 text-amber-300" aria-hidden />}
                <Avatar name={p.displayName} />
                <span className="w-full truncate text-center font-semibold">{p.displayName}</span>
                <span className="text-sm text-indigo-100 tabular-nums">{formatNumber(p.score)}</span>
                <div className={cn('flex w-full items-start justify-center rounded-t-lg pt-2 text-2xl font-bold text-slate-900', height, color)}>
                  {place}
                </div>
              </div>
            ) : null,
          )}
        </div>
      </section>

      <Leaderboard participants={state.participants} myId={myId} />

      <div className="flex flex-wrap justify-center gap-3">
        <Link to="/battle" className="inline-flex h-10 items-center rounded-lg btn-primary px-4 font-medium">
          Yeni yarış
        </Link>
        <Link to="/exams?tab=battle" className="inline-flex h-10 items-center rounded-lg border border-slate-300 bg-white px-4 font-medium text-slate-700 hover:bg-slate-50">
          Tarixçə
        </Link>
      </div>
    </>
  )
}

function ConnectionBadge({ status }: { status: ConnectionStatus }) {
  if (status === 'connected') {
    return (
      <span className="hidden items-center gap-1.5 text-xs font-medium text-emerald-700 sm:flex">
        <Wifi className="size-4" aria-hidden />
        Bağlıdır
      </span>
    )
  }

  return (
    <span role="status" className="flex items-center gap-1.5 rounded-full bg-amber-50 px-2.5 py-1 text-xs font-medium text-amber-800">
      {status === 'disconnected' ? <WifiOff className="size-4" aria-hidden /> : <Loader2 className="size-4 animate-spin" aria-hidden />}
      {status === 'disconnected' ? 'Bağlantı kəsildi' : 'Yenidən qoşulur…'}
    </span>
  )
}

function Avatar({ name, small, src }: { name: string; small?: boolean; src?: string | null }) {
  const hue = [...name].reduce((sum, c) => sum + c.charCodeAt(0), 0) % 360
  return src ? <img src={src} alt="" className={cn('shrink-0 rounded-full border border-slate-200 object-cover', small ? 'size-7' : 'size-9')} /> : <span
      aria-hidden
      className={cn('flex shrink-0 items-center justify-center rounded-full font-semibold text-white', small ? 'size-7 text-xs' : 'size-9')}
      style={{ backgroundColor: `hsl(${hue} 55% 50%)` }}
    >
      {name.charAt(0).toUpperCase()}
    </span>
}

function Centered({ children }: { children: ReactNode }) {
  return <div className="mx-auto flex min-h-dvh max-w-md flex-col items-center justify-center px-4 text-center">{children}</div>
}
