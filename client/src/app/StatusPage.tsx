import { ArrowLeft } from 'lucide-react'
import { Link, useNavigate, useSearchParams } from 'react-router'
import { SupportContact } from '../components/common/SupportContact'
import { useTheme } from '../lib/use-theme'
import { useTranslation } from 'react-i18next'

const knownCodes = [404, 403, 500]

/** Full-screen status page for any state; never shows raw technical errors. */
export function StatusPage({ code }: { code?: number }) {
  useTheme()
  const [params] = useSearchParams()
  const navigate = useNavigate()
  const { t } = useTranslation()
  const status = code ?? Number(params.get('code'))
  const known = knownCodes.includes(status) ? status : status >= 400 && status < 500 ? 404 : 500
  const title = t(`status.${known}.title`)
  const text = t(`status.${known}.text`)
  return (
    <main className="status-page flex min-h-dvh flex-col px-5 py-6 sm:px-10">
      <Link to="/" className="flex w-fit items-center gap-2.5 text-[17px] font-bold tracking-[-0.03em]" aria-label={t('common.homeAria')}>
        <span className="app-brand-mark grid size-[30px] place-items-center rounded-lg" aria-hidden><svg viewBox="0 0 16 16" className="size-4"><path d="M4 4h8M4 8h5.5M4 12h8" stroke="currentColor" strokeWidth="2" strokeLinecap="round" /></svg></span>
        ExamR
      </Link>
      <div className="mx-auto my-auto w-full max-w-xl py-16">
        <p className="ds-label">{t('common.errorCode', { code: known })}</p>
        <p className="status-code ds-num mt-4 font-bold leading-none" aria-hidden>{known}</p>
        <h1 className="mt-6 text-[28px] font-bold leading-tight tracking-[-0.035em] sm:text-[34px]"><span className="ds-mark">{title}</span></h1>
        <p className="ds-muted mt-4 max-w-[46ch] text-[16px] leading-7">{text}</p>
        {known !== 404 && <SupportContact className="ds-muted mt-3 max-w-[46ch]" topic={t('common.errorTopic', { code: known })} />}
        <div className="mt-8 flex flex-wrap items-center gap-x-5 gap-y-3">
          <Link to="/" className="btn-primary inline-flex h-11 items-center rounded-[10px] px-5 text-sm font-semibold">{t('common.backHome')}</Link>
          <button type="button" onClick={() => (window.history.length > 1 ? navigate(-1) : navigate('/'))} className="ds-link inline-flex items-center gap-1.5 text-sm">
            <ArrowLeft className="size-4" aria-hidden />{t('common.goBack')}
          </button>
        </div>
      </div>
    </main>
  )
}
