import { Headphones, Pause, Play } from 'lucide-react'
import { useEffect, useRef, useState } from 'react'
import { ApiError } from '../../lib/api-client'
import { cn } from '../../lib/utils'
import { examAudioUrl, useStartAudioPlay, type ExamAudio } from './api'
import { useTranslation } from 'react-i18next'

const boxClass = 'rounded-xl border border-indigo-200 bg-indigo-50/60 px-3.5 py-3 sm:px-5'

/**
 * Listening recording with native controls (no play limit, or review after the exam). Rendered with a key per
 * recording, so moving between questions of the same recording does not interrupt playback.
 */
export function ListeningPlayer({ src, title, className, hint = true, onPlayingChange }: {
  src: string
  title: string
  className?: string
  hint?: boolean
  onPlayingChange?: (playing: boolean) => void
}) {
  const { t } = useTranslation()
  return (
    <section data-no-swipe aria-label={t('exam.listening.aria', { title })} className={className ?? boxClass}>
      <Heading title={title} />
      {/* Native controls: keyboard and screen-reader accessible, with seeking (the API serves byte ranges). */}
      <audio controls preload="metadata" src={src} className="mt-2 w-full"
        onPlay={() => onPlayingChange?.(true)} onPause={() => onPlayingChange?.(false)} onEnded={() => onPlayingChange?.(false)}>
        {t('exam.listening.unsupported')}
      </audio>
      {hint && <p className="mt-1.5 text-xs text-indigo-900/70">{t('exam.listening.shared')}</p>}
    </section>
  )
}

/**
 * Exam with a play limit (like the real exam: twice). Each play starts from the beginning and is counted on the
 * server before the recording is loaded; it can be paused, but not wound back. Navigation between questions does not
 * stop it (the runner keeps it on screen while it plays).
 */
export function LimitedListeningPlayer({ examId, audio, onPlayingChange }: {
  examId: string
  audio: ExamAudio
  onPlayingChange?: (playing: boolean) => void
}) {
  const { t } = useTranslation()
  const startPlay = useStartAudioPlay(examId)
  const element = useRef<HTMLAudioElement>(null)
  const max = audio.maxPlays ?? 0
  const used = audio.playsUsed ?? 0
  const [src, setSrc] = useState<string | null>(null)
  const [inRound, setInRound] = useState(false)
  const [playing, setPlaying] = useState(false)
  const [time, setTime] = useState({ current: 0, duration: 0 })
  const [error, setError] = useState<string | null>(null)
  const left = Math.max(0, max - used)

  const notify = useRef(onPlayingChange)
  useEffect(() => { notify.current = onPlayingChange })
  useEffect(() => { notify.current?.(inRound) }, [inRound])

  function startRound() {
    setError(null)
    // iOS Safari plays only from a tap; touching the element here, inside the tap, lets the later play() through.
    element.current?.play().catch(() => {})
    startPlay.mutate(audio.id, {
      onSuccess: () => {
        setInRound(true)
        const player = element.current
        if (!player) return
        if (src === null) setSrc(examAudioUrl(examId, audio.id))
        else { player.currentTime = 0; void player.play() }
      },
      onError: e => setError(e instanceof ApiError && e.status === 409 ? t('exam.listening.limitReached') : e.message),
    })
  }

  function toggle() {
    const player = element.current
    if (!player) return
    if (player.paused) void player.play()
    else player.pause()
  }

  const percent = time.duration > 0 ? Math.min(100, (time.current / time.duration) * 100) : 0
  return (
    <section data-no-swipe aria-label={t('exam.listening.aria', { title: audio.title })} className={boxClass}>
      <div className="flex flex-wrap items-center gap-x-3 gap-y-1">
        <Heading title={audio.title} />
        <span className={cn('ml-auto text-xs font-medium tabular-nums', left === 0 && !inRound ? 'text-rose-700' : 'text-indigo-900/80')}>
          {t('exam.listening.plays', { used, max })}
        </span>
      </div>
      <audio ref={element} src={src ?? undefined} preload="auto" autoPlay
        onPlay={() => setPlaying(true)} onPause={() => setPlaying(false)}
        onTimeUpdate={e => setTime({ current: e.currentTarget.currentTime, duration: e.currentTarget.duration || 0 })}
        onLoadedMetadata={e => setTime({ current: 0, duration: e.currentTarget.duration || 0 })}
        onEnded={() => { setPlaying(false); setInRound(false) }}
        onError={() => { if (src) setError(t('exam.listening.loadFailed')) }} />
      <div className="mt-2 flex items-center gap-3">
        {inRound ? (
          <button type="button" onClick={toggle} aria-label={playing ? t('exam.listening.pause') : t('dashboard.continue')}
            className="grid size-11 shrink-0 place-items-center rounded-full bg-indigo-600 text-white hover:bg-indigo-700">
            {playing ? <Pause className="size-5" aria-hidden /> : <Play className="size-5" aria-hidden />}
          </button>
        ) : (
          <button type="button" onClick={startRound} disabled={left === 0 || startPlay.isPending}
            className="inline-flex h-11 shrink-0 items-center gap-2 rounded-full bg-indigo-600 px-4 text-sm font-semibold text-white hover:bg-indigo-700 disabled:bg-slate-300 disabled:text-slate-600">
            <Play className="size-4" aria-hidden />
            {left === 0 ? t('usage.limitReached') : used === 0 ? t('exam.listening.listen') : t('exam.listening.again', { left })}
          </button>
        )}
        <div className="min-w-0 flex-1">
          <div className="h-1.5 overflow-hidden rounded-full bg-indigo-100" role="progressbar" aria-label={t('exam.listening.progress')}
            aria-valuemin={0} aria-valuemax={100} aria-valuenow={Math.round(percent)}>
            <div className="h-full rounded-full bg-indigo-600" style={{ width: `${percent}%` }} />
          </div>
          <p className="mt-1 text-xs text-indigo-900/70 tabular-nums">{format(time.current)} / {format(time.duration)}</p>
        </div>
      </div>
      <p className={cn('mt-1.5 text-xs', error ? 'text-rose-700' : 'text-indigo-900/70')} role={error ? 'alert' : undefined}>
        {error ?? t('exam.listening.rules', { max })}
      </p>
    </section>
  )
}

function Heading({ title }: { title: string }) {
  return (
    <p className="flex min-w-0 items-center gap-2 text-sm font-semibold text-indigo-900">
      <Headphones className="size-4 shrink-0" aria-hidden />
      <span className="min-w-0 truncate">{title}</span>
    </p>
  )
}

function format(seconds: number) {
  if (!Number.isFinite(seconds) || seconds <= 0) return '0:00'
  const s = Math.floor(seconds)
  return `${Math.floor(s / 60)}:${String(s % 60).padStart(2, '0')}`
}
