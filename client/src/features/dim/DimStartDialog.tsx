import { Eye, EyeOff, Play } from 'lucide-react'
import { useEffect, useId, useRef, useState } from 'react'
import { Button } from '../../components/common/Button'
import { ChoiceCard } from '../../components/common/ChoiceCard'
import { SupportContact } from '../../components/common/SupportContact'
import type { RevealMode } from '../exams/api'
import { useTranslation } from 'react-i18next'

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
  const { t } = useTranslation()
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
      <p className="mt-1 text-sm text-slate-600">{t('common.questions', { count: questionCount })} · {t('exam.setup.minutes', { count: durationMinutes })}. {t('exam.setup.revealTitle')}</p>
      <div className="mt-4 grid gap-3">
        <ChoiceCard name={`reveal-${testId}`} checked={mode === 'AtEnd'} onChange={() => setMode('AtEnd')} icon={<EyeOff className="size-5" />}
          title={t('exam.setup.revealEnd')} description={t('dim.start.endText')} />
        <ChoiceCard name={`reveal-${testId}`} checked={mode === 'Immediate'} onChange={() => setMode('Immediate')} icon={<Eye className="size-5" />}
          title={t('exam.setup.revealEach')} description={t('dim.start.eachText')} />
      </div>
      {error && (
        <div role="alert" className="mt-4 rounded-lg bg-rose-50 px-3 py-2 text-sm text-rose-700">
          <p>{error}</p>
          <SupportContact className="mt-1" topic={t('dim.start.failedTopic')} reference={title} />
        </div>
      )}
      <div className="mt-6 flex justify-end gap-3">
        <Button variant="secondary" disabled={starting} onClick={onClose}>{t('common.cancel')}</Button>
        <Button loading={starting} onClick={() => onStart(mode)}><Play className="size-4" aria-hidden />{t('dim.start.start')}</Button>
      </div>
    </dialog>
  )
}
