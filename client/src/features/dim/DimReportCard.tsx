import { Info, TrendingDown } from 'lucide-react'
import { formatNumber } from '../../lib/format'
import { useDimReport } from './api'

/** DİM breakdown under an exam result; renders nothing for ordinary exams. */
export function DimReportCard({ examId }: { examId: string }) {
  const report = useDimReport(examId)
  if (report.isPending) return <div className="mb-5 h-40 animate-pulse rounded-xl bg-slate-200/60" aria-label="DİM nəticəsi yüklənir" />
  if (!report.data) return null

  const { score, notice } = report.data
  const max = report.data.track === 'Buraxilis' ? 300 : 400

  return (
    <section aria-label="DİM nəticəsi" className="mb-5 rounded-xl border border-slate-200 bg-white p-4 sm:p-6">
      <div className="flex flex-wrap items-baseline justify-between gap-2">
        <h2 className="text-lg font-semibold">DİM nəticəsi</h2>
        <p className="text-sm text-slate-500">
          {report.data.track === 'Buraxilis' ? 'Buraxılış' : 'Blok'}{report.data.groupName && ` · ${report.data.groupName}`} · {report.data.title}
        </p>
      </div>

      <div className="mt-4 grid gap-3 sm:grid-cols-2">
        <Metric label="Qapalı hissə balı" value={score.closedPoints} max={score.closedMaxPoints}
          hint="Qapalı tapşırıqların rəsmi düsturla gətirdiyi bal" />
        <Metric label="Təxmini bal" value={score.estimatedPoints} max={score.fullMaxPoints}
          hint={score.fullMaxPoints === max ? `${max} ballıq şkala üzrə təxmin` : 'Seçilmiş fənlər üzrə təxmin'} />
      </div>

      <p className="mt-3 flex gap-2 rounded-lg bg-amber-50 p-3 text-sm text-amber-900">
        <Info className="mt-0.5 size-4 shrink-0" aria-hidden />
        <span>{notice}</span>
      </p>

      <div className="mt-5 overflow-x-auto">
        <table className="w-full min-w-[520px] text-left text-sm">
          <caption className="sr-only">Fənlər üzrə nəticə</caption>
          <thead className="text-xs tracking-wide text-slate-500 uppercase">
            <tr className="border-b border-slate-200">
              <th scope="col" className="py-2 pr-3 font-medium">Fənn</th>
              <th scope="col" className="px-2 py-2 text-right font-medium">Düzgün</th>
              <th scope="col" className="px-2 py-2 text-right font-medium">Səhv</th>
              <th scope="col" className="px-2 py-2 text-right font-medium">Boş</th>
              <th scope="col" className="px-2 py-2 text-right font-medium">Qapalı bal</th>
              <th scope="col" className="py-2 pl-2 text-right font-medium">Təxmini</th>
            </tr>
          </thead>
          <tbody className="tabular-nums">
            {score.subjects.map(s => (
              <tr key={s.subject} className="border-b border-slate-100 last:border-0">
                <th scope="row" className="py-2.5 pr-3 font-medium text-slate-800">
                  {s.name}{s.coefficient !== 1 && <span className="ml-1.5 text-xs font-normal text-slate-500">×{formatNumber(s.coefficient)}</span>}
                </th>
                <td className="px-2 py-2.5 text-right text-emerald-700">{s.correct}</td>
                <td className="px-2 py-2.5 text-right text-rose-700">{s.incorrect}</td>
                <td className="px-2 py-2.5 text-right text-slate-500">{s.unanswered}</td>
                <td className="px-2 py-2.5 text-right">{formatNumber(s.closedPoints)} <span className="text-slate-400">/ {formatNumber(s.closedMaxPoints)}</span></td>
                <td className="py-2.5 pl-2 text-right font-medium">{formatNumber(s.estimatedPoints)} <span className="font-normal text-slate-400">/ {formatNumber(s.fullMaxPoints)}</span></td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      <div className="mt-5">
        <h3 className="flex items-center gap-2 text-sm font-semibold text-slate-800">
          <TrendingDown className="size-4 text-rose-600" aria-hidden />Zəif mövzular
        </h3>
        {score.weakTopics.length === 0 ? (
          <p className="mt-2 text-sm text-slate-500">Zəif mövzu aşkar edilmədi (və ya suallara mövzu təyin olunmayıb).</p>
        ) : (
          <ul className="mt-2 grid gap-2 sm:grid-cols-2">
            {score.weakTopics.map(t => (
              <li key={`${t.subject}-${t.topic}`} className="flex items-center justify-between gap-3 rounded-lg border border-rose-100 bg-rose-50/60 px-3 py-2 text-sm">
                <span className="min-w-0">
                  <span className="block truncate font-medium text-slate-800">{t.topic}</span>
                  <span className="text-xs text-slate-500">{score.subjects.find(s => s.subject === t.subject)?.name ?? t.subject}</span>
                </span>
                <span className="shrink-0 text-right text-rose-700 tabular-nums">{t.correct}/{t.total} · {formatNumber(t.accuracy)}%</span>
              </li>
            ))}
          </ul>
        )}
      </div>
    </section>
  )
}

function Metric({ label, value, max, hint }: { label: string; value: number; max: number; hint: string }) {
  const percent = max > 0 ? Math.min(100, (value / max) * 100) : 0
  return (
    <div className="rounded-lg border border-slate-200 p-3">
      <p className="text-xs font-medium tracking-wide text-slate-500 uppercase">{label}</p>
      <p className="mt-1 text-2xl font-semibold tabular-nums">
        {formatNumber(value)} <span className="text-base font-normal text-slate-400">/ {formatNumber(max)}</span>
      </p>
      <div className="mt-2 h-1.5 overflow-hidden rounded-full bg-slate-100" role="presentation">
        <div className="h-full rounded-full bg-indigo-600" style={{ width: `${percent}%` }} />
      </div>
      <p className="mt-1.5 text-xs text-slate-500">{hint}</p>
    </div>
  )
}
