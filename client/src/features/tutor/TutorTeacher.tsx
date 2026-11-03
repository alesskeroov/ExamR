import { CheckCircle2, ChevronDown, CircleHelp, Eye, Lightbulb, ListChecks, ListOrdered, Feather, Play, StepForward } from 'lucide-react'
import { useState, type ReactNode } from 'react'
import { cn } from '../../lib/utils'
import type { TutorAction, TutorDifficulty, TutorTopic } from './api'

interface Props {
  topics: TutorTopic[]
  loading: boolean
  current: TutorTopic | null
  difficulty: TutorDifficulty
  busy: boolean
  onDifficulty: (difficulty: TutorDifficulty) => void
  onAction: (action: TutorAction, topicKey?: string) => void
}

const levels: [TutorDifficulty, string][] = [['Easy', 'Asan'], ['Medium', 'Orta'], ['Hard', 'Çətin']]

/** Teacher-mode controls above the composer: current topic, difficulty and the one-tap teaching actions. */
export function TutorTeacher({ topics, loading, current, difficulty, busy, onDifficulty, onAction }: Props) {
  const [listOpen, setListOpen] = useState(false)
  const mastered = topics.filter(t => t.status === 'Mastered').length
  const weak = topics.filter(t => t.weak).length
  const awaiting = current?.awaitingAnswer ?? false
  const pick = (key: string) => { setListOpen(false); onAction('Teach', key) }

  return (
    <div className="mx-auto mb-2 max-w-3xl">
      {listOpen && (
        <div id="tutor-topics" className="tutor-enter tutor-border mb-2 max-h-[min(50dvh,22rem)] overflow-y-auto rounded-2xl border bg-white p-2 shadow-lg">
          <div className="flex items-center justify-between px-2 pt-1 pb-2 text-xs text-slate-500">
            <span>{mastered}/{topics.length} mövzu mənimsənilib{weak > 0 && <> · <span className="font-semibold text-amber-700">{weak} zəif</span></>}</span>
            <span className="h-1.5 w-24 overflow-hidden rounded-full bg-slate-100" aria-hidden>
              <span className="block h-full rounded-full bg-emerald-500 transition-[width]" style={{ width: `${topics.length ? (mastered / topics.length) * 100 : 0}%` }} />
            </span>
          </div>
          <ul>
            {topics.map((topic, index) => (
              <li key={topic.key}>
                <button type="button" onClick={() => pick(topic.key)} disabled={busy}
                  className={cn('flex w-full items-center gap-3 rounded-xl px-2 py-2 text-left hover:bg-slate-50 disabled:opacity-60', topic.key === current?.key && 'bg-indigo-50/60')}>
                  <StatusDot topic={topic} index={index} />
                  <span className="min-w-0 flex-1">
                    <span className="block truncate text-sm font-medium text-slate-800">{topic.title}</span>
                    <span className="block text-[11px] text-slate-500">
                      {topic.pageFrom ? `səh. ${topic.pageFrom}${topic.pageTo && topic.pageTo !== topic.pageFrom ? `–${topic.pageTo}` : ''} · ` : ''}
                      {topic.status === 'NotStarted' ? `${topic.steps} hissə` : `${topic.currentStep + 1}/${topic.steps} hissə`}
                      {topic.attempts > 0 && ` · ${topic.correct}/${topic.attempts} düz`}
                    </span>
                  </span>
                  {topic.weak && <span className="shrink-0 rounded-full bg-amber-50 px-2 py-0.5 text-[11px] font-semibold text-amber-700">Zəif</span>}
                </button>
              </li>
            ))}
          </ul>
        </div>
      )}

      <div className="flex flex-wrap items-center gap-2">
        <button type="button" onClick={() => setListOpen(open => !open)} aria-expanded={listOpen} aria-controls="tutor-topics" disabled={loading || topics.length === 0}
          className="tutor-border inline-flex h-8 min-w-0 max-w-full flex-1 items-center gap-2 rounded-full border bg-white px-3 text-xs font-medium text-slate-700 hover:bg-slate-50 sm:flex-none">
          <ListOrdered className="size-3.5 shrink-0 text-indigo-600" aria-hidden />
          <span className="truncate">{loading ? 'Mövzular yüklənir…' : current ? `${current.title} · ${current.currentStep + 1}/${current.steps}` : 'Mövzu seç'}</span>
          <ChevronDown className={cn('size-3.5 shrink-0 transition-transform', listOpen && 'rotate-180')} aria-hidden />
        </button>
        <div role="radiogroup" aria-label="Çətinlik" className="flex shrink-0 rounded-full bg-slate-100 p-0.5 text-[11px] font-medium">
          {levels.map(([value, label]) => (
            <button key={value} type="button" role="radio" aria-checked={difficulty === value} onClick={() => onDifficulty(value)}
              className={cn('rounded-full px-2.5 py-1 transition-colors', difficulty === value ? 'bg-white text-slate-900 shadow-sm' : 'text-slate-500 hover:text-slate-800')}>
              {label}
            </button>
          ))}
        </div>
      </div>

      {current && (
        <div className="-mx-1 mt-2 flex gap-1.5 overflow-x-auto px-1 pb-0.5 [scrollbar-width:none]" role="toolbar" aria-label="Müəllim əməliyyatları">
          {current.status === 'NotStarted'
            ? <Chip icon={<Play />} label="Öyrənməyə başla" primary disabled={busy} onClick={() => onAction('Teach', current.key)} />
            : <Chip icon={<StepForward />} label={current.currentStep + 1 >= current.steps ? 'Növbəti mövzu' : 'Növbəti hissə'} disabled={busy} onClick={() => onAction('Next', current.key)} />}
          <Chip icon={<CircleHelp />} label="Məni yoxla" disabled={busy} onClick={() => onAction('Quiz', current.key)} />
          <Chip icon={<ListChecks />} label="Variantlı test" disabled={busy} onClick={() => onAction('MultipleChoice', current.key)} />
          {awaiting && <Chip icon={<Lightbulb />} label="İpucu" disabled={busy} onClick={() => onAction('Hint', current.key)} />}
          <Chip icon={<Feather />} label="Sadə izah et" disabled={busy} onClick={() => onAction('Simplify', current.key)} />
          {awaiting && <Chip icon={<Eye />} label="Doğru cavab" disabled={busy} onClick={() => onAction('Reveal', current.key)} />}
        </div>
      )}
    </div>
  )
}

function Chip({ icon, label, onClick, disabled, primary }: { icon: ReactNode; label: string; onClick: () => void; disabled?: boolean; primary?: boolean }) {
  return (
    <button type="button" onClick={onClick} disabled={disabled}
      className={cn('inline-flex h-8 shrink-0 items-center gap-1.5 rounded-full px-3 text-xs font-medium whitespace-nowrap transition-colors disabled:opacity-50 [&_svg]:size-3.5',
        primary ? 'btn-primary' : 'tutor-border border bg-white text-slate-700 hover:border-indigo-200 hover:bg-indigo-50/50')}>
      <span aria-hidden className="contents">{icon}</span>{label}
    </button>
  )
}

function StatusDot({ topic, index }: { topic: TutorTopic; index: number }) {
  if (topic.status === 'Mastered') return <CheckCircle2 className="size-5 shrink-0 text-emerald-600" aria-label="Mənimsənilib" />
  return (
    <span className={cn('grid size-5 shrink-0 place-items-center rounded-full text-[10px] font-bold',
      topic.status === 'InProgress' ? 'bg-indigo-100 text-indigo-700' : 'bg-slate-100 text-slate-500')}
      aria-label={topic.status === 'InProgress' ? 'Davam edir' : 'Başlanmayıb'}>
      {index + 1}
    </span>
  )
}
