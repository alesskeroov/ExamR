import { Check, FileText } from 'lucide-react'
import './landing.css'

const grid = Array.from({ length: 20 }, (_, i) => (i < 6 ? 'done' : i === 6 ? 'now' : 'todo'))

/** Illustrative ExamR screen built from the real exam layout; the content is sample data, labelled as such. */
export function ProductPreview() {
  return (
    <div className="lp-stage" aria-label="ExamR interfeysinin nümunəsi" role="img">
      <div className="lp-sheetdoc" aria-hidden>
        <div className="flex items-center justify-between gap-2">
          <span className="flex items-center gap-1.5 text-[12px] font-semibold"><FileText className="size-3.5" />biologiya-final.pdf</span>
          <span className="lp-chip"><Check className="size-3" strokeWidth={3} />48 sual</span>
        </div>
        <div className="lp-sheetdoc-line" style={{ width: '92%' }} />
        <div className="lp-sheetdoc-line" style={{ width: '78%' }} />
        <p className="lp-sheetdoc-q">7. Hüceyrədə enerjinin əsas hissəsi harada istehsal olunur?</p>
        <div className="lp-sheetdoc-line" style={{ width: '64%' }} />
        <div className="lp-sheetdoc-line" style={{ width: '85%' }} />
        <div className="lp-sheetdoc-line" style={{ width: '40%' }} />
      </div>

      <div className="lp-examcard" aria-hidden>
        <div className="lp-examcard-head">
          <span className="lp-mono">Sual 07 / 20</span>
          <span className="lp-timer lp-mono">18:42</span>
        </div>
        <div className="lp-examcard-body">
          <p className="lp-q">Hüceyrədə enerjinin əsas hissəsi harada istehsal olunur?</p>
          <div className="lp-options">
            <div className="lp-option"><b>A</b>Ribosom</div>
            <div className="lp-option" data-state="picked"><b>B</b>Mitoxondri</div>
            <div className="lp-option"><b>C</b>Holci aparatı</div>
            <div className="lp-option"><b>D</b>Lizosom</div>
          </div>
        </div>
        <div className="lp-grid20">{grid.map((s, i) => <i key={i} data-s={s} />)}</div>
      </div>

      <div className="lp-float" aria-hidden>
        <span className="lp-mono font-semibold">6/6</span>
        <span>düzgün · bu sessiya</span>
      </div>
      <span className="lp-tag">nümunə interfeys</span>
    </div>
  )
}
