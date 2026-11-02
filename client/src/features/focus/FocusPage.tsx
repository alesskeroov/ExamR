import { useEffect, useRef, useState } from 'react'
import { BookOpen, Check, Coffee, Library, ListChecks, Pause, Play, RotateCcw, Square, Target, X } from 'lucide-react'
import { Alert } from '../../components/common/Alert'
import { Button } from '../../components/common/Button'
import { EmptyState } from '../../components/common/EmptyState'
import { PageHeader } from '../../components/common/PageHeader'
import { cn } from '../../lib/utils'
import { ExamSetupPage } from '../exams/ExamSetupPage'
import { StudyPage } from '../study/StudyPage'
import { useFocusQuestions, useFocusStats, useSaveFocusSession, type FocusActivity } from './api'

type Phase = 'setup' | 'focus' | 'result' | 'break' | 'breakDone'
interface Result { planned: number; focusSeconds: number; answered: number; correct: number; completed: boolean }
interface FocusState {
  phase: Phase; minutes: number; activity: FocusActivity; documentId: string
  /** Wall-clock end while running; null while paused. Remaining time is derived from it, so nothing drifts. */
  endAt: number | null; remaining: number; total: number
  startedAt: string; cycles: number; answered: number; correct: number; questionIndex: number
  breakLong: boolean; result: Result | null
}

const KEY = 'examr.focus'
const DURATIONS = [15, 25, 45, 60]
const card = 'rounded-xl border border-slate-200 bg-white shadow-sm'
const initial: FocusState = { phase: 'setup', minutes: 25, activity: 'exam', documentId: '', endAt: null, remaining: 25 * 60_000, total: 25 * 60_000,
  startedAt: '', cycles: 0, answered: 0, correct: 0, questionIndex: 0, breakLong: false, result: null }

function load(): FocusState {
  try {
    const raw = localStorage.getItem(KEY)
    const saved = raw ? { ...initial, ...JSON.parse(raw) as FocusState } : initial
    // Older saved state may still use the removed "questions" activity.
    return ['exam', 'mistakes', 'study'].includes(saved.activity) ? saved : { ...saved, activity: 'exam' }
  } catch { return initial }
}
// 25 → 5 minutes; other lengths scale the same way. Every 4th completed focus earns a 15-minute break.
const breakMinutes = (minutes: number, long: boolean) => long ? 15 : Math.max(3, Math.round(minutes / 5))
const clock = (ms: number) => { const s = Math.ceil(ms / 1000); return `${String(Math.floor(s / 60)).padStart(2, '0')}:${String(s % 60).padStart(2, '0')}` }
const minutesText = (seconds: number) => seconds < 3600 ? `${Math.round(seconds / 60)} dəq` : `${Math.floor(seconds / 3600)} saat ${Math.round((seconds % 3600) / 60)} dəq`

function notify(text: string) {
  try { if ('Notification' in window && Notification.permission === 'granted') new Notification('ExamR Focus', { body: text }) } catch { /* optional */ }
}

export function FocusPage() {
  const [state, setState] = useState(load)
  const [now, setNow] = useState(() => Date.now())
  const [message, setMessage] = useState('')
  const save = useSaveFocusSession()
  const update = (patch: Partial<FocusState>) => setState(s => ({ ...s, ...patch }))
  const remaining = state.endAt === null ? state.remaining : Math.max(0, state.endAt - now)

  useEffect(() => { try { localStorage.setItem(KEY, JSON.stringify(state)) } catch { /* storage unavailable */ } }, [state])
  // One interval while running; it only refreshes "now", the remaining time always comes from endAt.
  useEffect(() => {
    if (state.endAt === null) return
    const timer = setInterval(() => setNow(Date.now()), 250)
    return () => clearInterval(timer)
  }, [state.endAt])

  function startFocus(s: FocusState = state) {
    if ('Notification' in window && Notification.permission === 'default') void Notification.requestPermission().catch(() => undefined)
    const total = s.minutes * 60_000
    setMessage('')
    setNow(Date.now())
    setState({ ...s, phase: 'focus', total, remaining: total, endAt: Date.now() + total, startedAt: new Date().toISOString(), answered: 0, correct: 0, questionIndex: 0, result: null })
  }

  function finishFocus(completed: boolean) {
    const left = state.endAt === null ? state.remaining : Math.max(0, state.endAt - Date.now())
    const focusSeconds = Math.round((state.total - (completed ? 0 : left)) / 1000)
    const result = { planned: state.minutes, focusSeconds, answered: state.answered, correct: state.correct, completed }
    save.mutate({ activity: state.activity, plannedMinutes: state.minutes, focusSeconds, answered: state.answered, correct: state.correct, completed, startedAt: state.startedAt })
    const cycles = completed ? state.cycles + 1 : state.cycles
    update({ phase: 'result', endAt: null, remaining: 0, result, cycles, breakLong: completed && cycles % 4 === 0 })
    if (completed) { setMessage('Fokus sessiyası tamamlandı. Fasilə vaxtıdır.'); notify('Fokus sessiyası tamamlandı. Fasilə vaxtıdır.') }
  }

  function startBreak() {
    const total = breakMinutes(state.minutes, state.breakLong) * 60_000
    setNow(Date.now())
    update({ phase: 'break', total, remaining: total, endAt: Date.now() + total })
  }

  // Phase transitions when a running timer reaches zero.
  const finished = state.endAt !== null && remaining === 0
  const finishRef = useRef(finishFocus)
  useEffect(() => { finishRef.current = finishFocus })
  useEffect(() => {
    if (!finished) return
    if (state.phase === 'focus') finishRef.current(true)
    else if (state.phase === 'break') {
      setState(s => ({ ...s, phase: 'breakDone', endAt: null, remaining: 0 }))
      setMessage('Fasilə bitdi. Növbəti fokus sessiyasına hazırsan?')
      notify('Fasilə bitdi. Növbəti fokus sessiyasına hazırsan?')
    }
  }, [finished, state.phase])

  const running = state.endAt !== null
  const pauseResume = () => running ? update({ endAt: null, remaining }) : (setNow(Date.now()), update({ endAt: Date.now() + state.remaining }))

  return <div className="mx-auto max-w-4xl">
    <PageHeader title="Focus Mode" description="Diqqətini bir mövzuya ver: vaxtı seç, öyrən, fasilə et." />
    {message && <div className="mb-4"><Alert tone="info" title={message} /></div>}
    {save.error && <div className="mb-4"><Alert title="Sessiya saxlanmadı">{save.error.message}</Alert></div>}

    {state.phase === 'setup' && <Setup state={state} update={update} onStart={() => startFocus()} />}

    {(state.phase === 'focus' || state.phase === 'break') && <section className={cn(card, 'flex flex-col items-center gap-6 p-6 sm:p-8')}>
      <span className={cn('inline-flex items-center gap-2 rounded-full px-3 py-1 text-xs font-semibold', state.phase === 'focus' ? 'bg-indigo-50 text-indigo-700' : 'bg-emerald-50 text-emerald-700')}>
        {state.phase === 'focus' ? <><Target className="size-3.5" aria-hidden />Fokus · {state.minutes} dəq</> : <><Coffee className="size-3.5" aria-hidden />{state.breakLong ? 'Uzun fasilə' : 'Qısa fasilə'}</>}
      </span>
      <Ring progress={1 - remaining / state.total} label={clock(remaining)} tone={state.phase === 'focus' ? 'focus' : 'break'} paused={!running} />
      <div className="flex flex-wrap justify-center gap-3">
        <Button variant="secondary" onClick={pauseResume}>{running ? <><Pause className="size-4" aria-hidden />Pauza</> : <><Play className="size-4" aria-hidden />Davam et</>}</Button>
        {state.phase === 'focus'
          ? <Button variant="ghost" className="text-rose-600" onClick={() => finishFocus(false)}><Square className="size-4" aria-hidden />Sessiyanı bitir</Button>
          : <Button variant="ghost" onClick={() => startFocus()}><X className="size-4" aria-hidden />Fasiləni ötür</Button>}
      </div>
    </section>}

    {state.phase === 'focus' && <div className="mt-6">
      {state.activity === 'study' ? <StudyPage /> : state.activity === 'exam' ? <ExamSetupPage /> : <Practice state={state} update={update} />}
    </div>}

    {state.phase === 'result' && state.result && <ResultCard result={state.result} breakText={state.result.completed ? `${breakMinutes(state.minutes, state.breakLong)} dəq ${state.breakLong ? 'uzun ' : ''}fasilə` : null}
      onBreak={startBreak} onSkip={() => startFocus()} onDone={() => update({ phase: 'setup' })} />}

    {state.phase === 'breakDone' && <section className={cn(card, 'flex flex-col items-center gap-4 p-8 text-center')}>
      <span className="grid size-14 place-items-center rounded-2xl bg-indigo-50 text-indigo-600"><Target className="size-7" aria-hidden /></span>
      <h2 className="text-lg font-semibold">Fasilə bitdi</h2>
      <div className="flex flex-wrap justify-center gap-3"><Button onClick={() => startFocus()}><Play className="size-4" aria-hidden />Növbəti fokusa başla</Button><Button variant="secondary" onClick={() => update({ phase: 'setup' })}>Bitir</Button></div>
    </section>}

    {(state.phase === 'setup' || state.phase === 'result' || state.phase === 'breakDone') && <Stats />}
  </div>
}

function Ring({ progress, label, tone, paused }: { progress: number; label: string; tone: 'focus' | 'break'; paused: boolean }) {
  const r = 88, c = 2 * Math.PI * r
  return <div className="relative grid size-56 place-items-center sm:size-72" role="timer" aria-live="off" aria-label={`Qalan vaxt ${label}`}>
    <svg viewBox="0 0 200 200" className="absolute inset-0 -rotate-90">
      <circle cx="100" cy="100" r={r} fill="none" strokeWidth="8" className="stroke-slate-100" />
      <circle cx="100" cy="100" r={r} fill="none" strokeWidth="8" strokeLinecap="round" strokeDasharray={c} strokeDashoffset={c * (1 - progress)}
        className={cn('transition-[stroke-dashoffset] duration-300 ease-linear', tone === 'focus' ? 'stroke-indigo-600' : 'stroke-emerald-500')} />
    </svg>
    <div className="text-center"><p className={cn('text-4xl font-semibold tabular-nums tracking-tight sm:text-6xl', paused && 'animate-pulse opacity-70')}>{label}</p>
      {paused && <p className="mt-1 text-xs font-medium text-slate-500">Pauza</p>}</div>
  </div>
}

function Setup({ state, update, onStart }: { state: FocusState; update: (p: Partial<FocusState>) => void; onStart: () => void }) {
  const activities: { id: FocusActivity; label: string; hint: string; icon: typeof Library }[] = [
    { id: 'exam', label: 'İmtahana başla', hint: 'Vaxtlı təsadüfi imtahan', icon: ListChecks },
    { id: 'mistakes', label: 'Səhv suallar', hint: 'İmtahanlarda səhv etdiklərin', icon: RotateCcw },
    { id: 'study', label: 'Öyrənmə köməkçisi', hint: 'Addım-addım məşq', icon: BookOpen },
  ]
  return <section className={cn(card, 'space-y-6 p-5 sm:p-6')}>
    <fieldset><legend className="mb-3 text-sm font-medium">Müddət</legend>
      <div className="grid grid-cols-4 gap-2">{DURATIONS.map(m => <button key={m} type="button" aria-pressed={state.minutes === m} onClick={() => update({ minutes: m })}
        className={cn('h-14 rounded-xl border text-sm font-semibold transition-all', state.minutes === m ? 'border-indigo-500 bg-indigo-50 text-indigo-700 shadow-sm' : 'border-slate-200 text-slate-600 hover:border-slate-300')}>{m}<span className="ml-1 text-xs font-normal">dəq</span></button>)}</div>
    </fieldset>
    <fieldset><legend className="mb-3 text-sm font-medium">Fəaliyyət</legend>
      <div className="grid gap-2 sm:grid-cols-3">{activities.map(a => <button key={a.id} type="button" aria-pressed={state.activity === a.id} onClick={() => update({ activity: a.id })}
        className={cn('flex items-start gap-3 rounded-xl border p-3 text-left transition-all', state.activity === a.id ? 'border-indigo-500 bg-indigo-50' : 'border-slate-200 hover:border-slate-300')}>
        <a.icon className={cn('mt-0.5 size-5 shrink-0', state.activity === a.id ? 'text-indigo-600' : 'text-slate-400')} aria-hidden />
        <span><span className="block text-sm font-medium">{a.label}</span><span className="block text-xs text-slate-500">{a.hint}</span></span></button>)}</div>
    </fieldset>
    {state.activity !== 'mistakes' && <p className="text-xs text-slate-500">{state.activity === 'exam' ? 'İmtahan nəticəsi Tarixçədə saxlanır' : 'Öyrənmə köməkçisinin nəticələri öz tarixçəsində qalır'}; Focus statistikasına yalnız fokus vaxtı yazılır.</p>}
    <p className="text-xs text-slate-500">Hər fokusdan sonra {breakMinutes(state.minutes, false)} dəq fasilə, 4 sessiyadan sonra 15 dəq uzun fasilə.</p>
    <Button className="w-full sm:w-auto" onClick={onStart}><Play className="size-4" aria-hidden />Başlat</Button>
  </section>
}

function Practice({ state, update }: { state: FocusState; update: (p: Partial<FocusState>) => void }) {
  const questions = useFocusQuestions(state.activity)
  const [picked, setPicked] = useState<string | null>(null)
  if (questions.isPending) return <div className={cn(card, 'h-48 animate-pulse')} />
  if (questions.error) return <Alert title="Suallar yüklənmədi">{questions.error.message}</Alert>
  const items = questions.data.items.filter(q => q.options.some(o => o.isCorrect))
  if (items.length === 0) return <EmptyState icon={<Library className="size-6" />} title="Səhv sual yoxdur"
    description="İmtahanlarda səhv etdiyin suallar burada təkrar üçün görünəcək." />
  const q = items[state.questionIndex % items.length]
  const choose = (id: string) => {
    if (picked) return
    setPicked(id)
    update({ answered: state.answered + 1, correct: state.correct + (q.options.find(o => o.id === id)?.isCorrect ? 1 : 0) })
  }
  return <article className={cn(card, 'p-5 sm:p-6')}>
    <div className="mb-3 flex justify-between text-xs text-slate-500"><span>Sual {state.questionIndex % items.length + 1} / {items.length}</span><span className="tabular-nums">{state.correct} / {state.answered} düzgün</span></div>
    <h3 className="mb-4 text-base leading-7 font-medium">{q.text}</h3>
    <ul className="space-y-2">{q.options.map(o => {
      const show = !!picked && (o.isCorrect || o.id === picked)
      return <li key={o.id}><button type="button" disabled={!!picked} onClick={() => choose(o.id)}
        className={cn('flex w-full items-start gap-3 rounded-xl border px-3 py-2.5 text-left text-sm transition-colors',
          show ? o.isCorrect ? 'border-emerald-300 bg-emerald-50 text-emerald-900' : 'border-rose-300 bg-rose-50 text-rose-900' : 'border-slate-200 hover:border-indigo-300 hover:bg-indigo-50/40')}>
        <span className="font-semibold text-slate-500">{o.label})</span><span className="flex-1">{o.text}</span>{show && o.isCorrect && <Check className="size-4 text-emerald-600" aria-hidden />}
      </button></li>
    })}</ul>
    {picked && <div className="mt-4 flex justify-end"><Button onClick={() => { setPicked(null); update({ questionIndex: state.questionIndex + 1 }) }}>Növbəti sual</Button></div>}
  </article>
}

function ResultCard({ result, breakText, onBreak, onSkip, onDone }: { result: Result; breakText: string | null; onBreak: () => void; onSkip: () => void; onDone: () => void }) {
  const wrong = result.answered - result.correct
  const rows: [string, string][] = [
    ['Planlaşdırılan', `${result.planned} dəq`], ['Faktiki fokus', minutesText(result.focusSeconds)], ['Cavablanan', String(result.answered)],
    ['Düzgün', String(result.correct)], ['Səhv', String(wrong)], ['Düzgünlük', result.answered ? `${Math.round(result.correct / result.answered * 100)}%` : '—'],
  ]
  return <section className={cn(card, 'p-6')}>
    <div className="mb-5 flex items-center justify-between gap-3"><h2 className="text-lg font-semibold">Sessiya nəticəsi</h2>
      <span className={cn('rounded-full px-3 py-1 text-xs font-semibold', result.completed ? 'bg-emerald-50 text-emerald-700' : 'bg-amber-50 text-amber-700')}>{result.completed ? 'Tamamlandı' : 'Yarımçıq'}</span></div>
    <dl className="grid grid-cols-2 gap-3 sm:grid-cols-3">{rows.map(([k, v]) => <div key={k} className="rounded-xl bg-slate-50 p-3"><dt className="text-xs text-slate-500">{k}</dt><dd className="text-lg font-semibold tabular-nums">{v}</dd></div>)}</dl>
    <div className="mt-6 flex flex-wrap gap-3">
      {breakText ? <><Button onClick={onBreak}><Coffee className="size-4" aria-hidden />Fasiləni başlat · {breakText}</Button><Button variant="secondary" onClick={onSkip}>Fasiləni ötür</Button></>
        : <Button onClick={onDone}>Yeni sessiya</Button>}
      {breakText && <Button variant="ghost" onClick={onDone}>Bitir</Button>}
    </div>
  </section>
}

function Stats() {
  const stats = useFocusStats()
  const max = Math.max(1, ...(stats.data?.lastSevenDays.map(d => d.focusSeconds) ?? [1]))
  return <section className={cn(card, 'mt-6 p-5 sm:p-6')} aria-label="Fokus statistikası">
    <h2 className="mb-4 font-semibold">Statistika</h2>
    {stats.isPending ? <div className="h-32 animate-pulse rounded-lg bg-slate-100" /> : stats.error ? <Alert title="Statistika yüklənmədi">{stats.error.message}</Alert> : <>
      <div className="grid grid-cols-3 gap-3">{[['Bu gün', minutesText(stats.data.todaySeconds)], ['Bu həftə', minutesText(stats.data.weekSeconds)], ['Tamamlanan', String(stats.data.completedSessions)]].map(([k, v]) =>
        <div key={k} className="rounded-xl bg-slate-50 p-3"><p className="text-xs text-slate-500">{k}</p><p className="text-lg font-semibold tabular-nums">{v}</p></div>)}</div>
      <div className="mt-5 flex h-28 items-end gap-2" role="img" aria-label="Son 7 günün fokus aktivliyi">{stats.data.lastSevenDays.map(d =>
        <div key={d.date} className="flex flex-1 flex-col items-center gap-1.5" title={`${d.date}: ${minutesText(d.focusSeconds)}`}>
          <div className="w-full max-w-10 rounded-md bg-indigo-500 transition-all duration-500" style={{ height: `${Math.max(4, d.focusSeconds / max * 88)}px`, opacity: d.focusSeconds ? 1 : .2 }} />
          <span className="text-[11px] text-slate-500">{new Date(`${d.date}T00:00:00`).toLocaleDateString('az-AZ', { weekday: 'short' })}</span>
        </div>)}</div>
    </>}
  </section>
}
