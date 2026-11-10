import './exam-flow.css'
import { Check } from 'lucide-react'
import { examThemes, useExamTheme } from '../../lib/use-exam-theme'
import { cn } from '../../lib/utils'

/** Settings card: the default reading theme for the exam screen, with a small question preview. */
export function ExamThemeSettings() {
  const { theme, setTheme } = useExamTheme()
  return (
    <section aria-labelledby="exam-theme-heading" className="rounded-xl border border-slate-200 bg-white p-5">
      <h2 id="exam-theme-heading" className="font-semibold">Görünüş</h2>
      <p className="mt-1 text-sm text-slate-500">İmtahan ekranının oxuma teması. İmtahan zamanı başlıqdakı palitra düyməsi ilə də dəyişə bilərsiniz; seçim bu cihazda yadda qalır.</p>
      <div className="mt-4 grid gap-4 md:grid-cols-[minmax(0,1fr)_minmax(0,1fr)]">
        <div role="radiogroup" aria-label="Oxuma teması" className="flex flex-col gap-1.5">
          {examThemes.map(option => {
            const active = theme === option.id
            return (
              <button key={option.id} type="button" role="radio" aria-checked={active} onClick={() => setTheme(option.id)}
                className={cn('flex items-center gap-3 rounded-xl border px-3 py-2.5 text-left transition-colors',
                  active ? 'border-slate-400 bg-slate-50' : 'border-slate-200 hover:bg-slate-50')}>
                <span aria-hidden className="exam-theme-swatch" style={{ background: option.swatch }} />
                <span className="min-w-0 flex-1">
                  <span className="block text-sm font-semibold text-slate-900">{option.label}</span>
                  <span className="block text-xs text-slate-500">{option.hint}</span>
                </span>
                {active && <Check className="size-4 shrink-0 text-slate-700" aria-hidden />}
              </button>
            )
          })}
        </div>
        <ThemePreview theme={theme} />
      </div>
    </section>
  )
}

function ThemePreview({ theme }: { theme: string }) {
  return (
    <div aria-hidden data-exam-theme={theme} className="exam-theme-preview">
      <div className="exam-theme-preview-card">
        <p className="exam-theme-preview-muted">Sual 3 / 20</p>
        <p className="exam-theme-preview-text">Fotosintez zamanı bitki hansı qazı udur?</p>
        {['Oksigen', 'Karbon qazı', 'Azot'].map((answer, index) => (
          <div key={answer} className="exam-theme-preview-option" data-selected={index === 1}>
            <span className="exam-theme-preview-letter">{String.fromCharCode(65 + index)}</span>{answer}
          </div>
        ))}
      </div>
    </div>
  )
}
