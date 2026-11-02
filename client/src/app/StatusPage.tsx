import { ArrowLeft } from 'lucide-react'
import { Link, useNavigate, useSearchParams } from 'react-router'
import { useTheme } from '../lib/use-theme'

const copy: Record<number, { title: string; text: string }> = {
  404: { title: 'Bu səhifə burada deyil.', text: 'Link köhnəlib, yanlış yazılıb və ya səhifə silinib. Ana səhifədən davam edə bilərsən.' },
  403: { title: 'Bu səhifəyə girişin yoxdur.', text: 'Hesabının bu bölməyə icazəsi yoxdur. Fərqli hesabla daxil olmusansa, çıxış edib yenidən yoxla.' },
  500: { title: 'Bizdə nəsə alınmadı.', text: 'Xəta bizim tərəfimizdədir və qeydə alındı. Bir az sonra yenidən cəhd et.' },
}

/** Full-screen status page for any state; never shows raw technical errors. */
export function StatusPage({ code }: { code?: number }) {
  useTheme()
  const [params] = useSearchParams()
  const navigate = useNavigate()
  const status = code ?? Number(params.get('code'))
  const known = copy[status] ? status : status >= 400 && status < 500 ? 404 : 500
  const { title, text } = copy[known]
  return (
    <main className="status-page flex min-h-dvh flex-col px-5 py-6 sm:px-10">
      <Link to="/" className="flex w-fit items-center gap-2.5 text-[17px] font-bold tracking-[-0.03em]" aria-label="ExamR ana səhifə">
        <span className="app-brand-mark grid size-[30px] place-items-center rounded-lg" aria-hidden><svg viewBox="0 0 16 16" className="size-4"><path d="M4 4h8M4 8h5.5M4 12h8" stroke="currentColor" strokeWidth="2" strokeLinecap="round" /></svg></span>
        ExamR
      </Link>
      <div className="mx-auto my-auto w-full max-w-xl py-16">
        <p className="ds-label">Xəta · {known}</p>
        <p className="status-code ds-num mt-4 font-bold leading-none" aria-hidden>{known}</p>
        <h1 className="mt-6 text-[28px] font-bold leading-tight tracking-[-0.035em] sm:text-[34px]"><span className="ds-mark">{title}</span></h1>
        <p className="ds-muted mt-4 max-w-[46ch] text-[16px] leading-7">{text}</p>
        <div className="mt-8 flex flex-wrap items-center gap-x-5 gap-y-3">
          <Link to="/" className="btn-primary inline-flex h-11 items-center rounded-[10px] px-5 text-sm font-semibold">Ana səhifəyə qayıt</Link>
          <button type="button" onClick={() => (window.history.length > 1 ? navigate(-1) : navigate('/'))} className="ds-link inline-flex items-center gap-1.5 text-sm">
            <ArrowLeft className="size-4" aria-hidden />Geri qayıt
          </button>
        </div>
      </div>
    </main>
  )
}
