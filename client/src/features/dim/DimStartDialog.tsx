import { Eye, EyeOff, Play } from 'lucide-react'
import { useEffect, useId, useRef, useState } from 'react'
import { Button } from '../../components/common/Button'
import { ChoiceCard } from '../../components/common/ChoiceCard'
import type { RevealMode } from '../exams/api'

interface DimStartDialogProps {
  testId: string
  title: string
  questionCount: number
  durationMinutes: number
  starting: boolean
  /** Shown inside the dialog when starting failed. */
  error?: string | null
  onStart: (mode: RevealMode) => void
  onClose: () => void
}

/** Asked before a DİM attempt starts: how the correct answers are shown, like on the regular exam setup page. */
export function DimStartDialog({ testId, title, questionCount, durationMinutes, starting, error, onStart, onClose }: DimStartDialogProps) {
  const dialog = useRef<HTMLDialogElement>(null)
  const titleId = useId()
  const [mode, setMode] = useState<RevealMode>('AtEnd')
  useEffect(() => {
    const element = dialog.current
    element?.showModal()
    return () => element?.close()
  }, [])

  return (
    <dialog ref={dialog} aria-labelledby={titleId} onCancel={event => { event.preventDefault(); if (!starting) onClose() }}
      className="m-auto max-h-[85dvh] w-[calc(100%-2rem)] max-w-lg overflow-y-auto rounded-2xl border border-slate-200 bg-white p-6 shadow-2xl backdrop:bg-slate-900/45 backdrop:backdrop-blur-sm">
      <h2 id={titleId} className="text-lg font-semibold text-slate-900">{title}</h2>
      <p className="mt-1 text-sm text-slate-600">{questionCount} sual · {durationMinutes} dəqiqə. Düzgün cavablar nə vaxt göstərilsin?</p>
      <div className="mt-4 grid gap-3">
        <ChoiceCard name={`reveal-${testId}`} checked={mode === 'AtEnd'} onChange={() => setMode('AtEnd')} icon={<EyeOff className="size-5" />}
          title="İmtahan bitdikdə" description="Real imtahan kimi: cavablar sona qədər dəyişdirilə bilər" />
        <ChoiceCard name={`reveal-${testId}`} checked={mode === 'Immediate'} onChange={() => setMode('Immediate')} icon={<Eye className="size-5" />}
          title="Hər sualdan sonra" description="Hər sualda göz ikonu ilə düzgün cavabı görə bilərsiniz; göstərildikdən sonra variant dəyişmir" />
      </div>
      {error && <p role="alert" className="mt-4 rounded-lg bg-rose-50 px-3 py-2 text-sm text-rose-700">{error}</p>}
      <div className="mt-6 flex justify-end gap-3">
        <Button variant="secondary" disabled={starting} onClick={onClose}>Ləğv et</Button>
        <Button loading={starting} onClick={() => onStart(mode)}><Play className="size-4" aria-hidden />İmtahanı başla</Button>
      </div>
    </dialog>
  )
}
