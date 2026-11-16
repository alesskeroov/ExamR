import { Link } from 'react-router'
import { useTranslation } from 'react-i18next'

/** 404 inside the signed-in layout: keeps the sidebar so the user can navigate on. */
export function NotFoundPage() {
  const { t } = useTranslation()
  return (
    <div className="py-10 sm:py-16">
      <p className="ds-label">{t('common.errorCode', { code: 404 })}</p>
      <p className="status-code ds-num mt-4 font-bold leading-none" aria-hidden>404</p>
      <h1 className="mt-6 text-[26px] font-bold leading-tight tracking-[-0.035em] sm:text-[32px]"><span className="ds-mark">{t('status.404.title')}</span></h1>
      <p className="ds-muted mt-4 max-w-[46ch] text-[15px] leading-7">{t('status.404.inApp')}</p>
      <Link to="/" className="btn-primary mt-8 inline-flex h-11 items-center rounded-[10px] px-5 text-sm font-semibold">{t('common.backToDashboard')}</Link>
    </div>
  )
}
