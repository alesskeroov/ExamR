import { Ban, RotateCw } from 'lucide-react'
import { useState } from 'react'
import { useNavigate } from 'react-router'
import { useDimReport, useStartDimTest } from './api'
import { DimStartDialog } from './DimStartDialog'

/**
 * "Yenidən imtahan ver" for a finished DİM attempt. When the attempt limit is used up (or the test is closed)
 * the button is shown disabled with the reason. Renders nothing for ordinary exams.
 */
export function DimRetakeButton({ examId, questionCount, durationMinutes }: { examId: string; questionCount: number; durationMinutes: number }) {
  const navigate = useNavigate()
  const report = useDimReport(examId)
  const start = useStartDimTest()
  const [choosing, setChoosing] = useState(false)
  const data = report.data
  if (!data) return null

  const limitReached = data.attemptLimit !== null && data.attemptsUsed >= data.attemptLimit
  if (limitReached || !data.canStart) {
    return (
      <span
        role="note"
        title={limitReached ? `Bu sınaq üçün ${data.attemptLimit} cəhdin hamısından istifadə etmisiniz.` : 'Sınaq hazırda bağlıdır.'}
        className="inline-flex h-10 cursor-not-allowed items-center gap-2 rounded-lg border border-slate-200 bg-slate-100 px-4 font-medium text-slate-400"
      >
        <Ban className="size-4" aria-hidden />
        {limitReached ? `Cəhd limiti bitib (${data.attemptsUsed}/${data.attemptLimit})` : 'Sınaq bağlıdır'}
      </span>
    )
  }

  return (
    <>
      <button type="button" onClick={() => { start.reset(); setChoosing(true) }}
        className="btn-primary inline-flex h-10 items-center gap-2 rounded-lg px-4 font-medium">
        <RotateCw className="size-4" aria-hidden />
        Yenidən imtahan ver
        {data.attemptLimit !== null && <span className="text-xs opacity-80">({data.attemptsUsed}/{data.attemptLimit})</span>}
      </button>
      {choosing && (
        <DimStartDialog testId={data.testId} title={data.title} questionCount={questionCount} durationMinutes={durationMinutes}
          starting={start.isPending} error={start.error?.message}
          onClose={() => setChoosing(false)}
          onStart={mode => start.mutate({ testId: data.testId, revealMode: mode }, { onSuccess: ({ examId: next }) => navigate(`/exams/${next}`) })} />
      )}
    </>
  )
}
