import { Link } from 'react-router'

/** 404 inside the signed-in layout: keeps the sidebar so the user can navigate on. */
export function NotFoundPage() {
  return (
    <div className="py-10 sm:py-16">
      <p className="ds-label">Xəta · 404</p>
      <p className="status-code ds-num mt-4 font-bold leading-none" aria-hidden>404</p>
      <h1 className="mt-6 text-[26px] font-bold leading-tight tracking-[-0.035em] sm:text-[32px]"><span className="ds-mark">Bu səhifə burada deyil.</span></h1>
      <p className="ds-muted mt-4 max-w-[46ch] text-[15px] leading-7">Link köhnəlib və ya yanlış yazılıb. Sol menyudan istədiyin bölməni seç və ya Dashboard-a qayıt.</p>
      <Link to="/" className="btn-primary mt-8 inline-flex h-11 items-center rounded-[10px] px-5 text-sm font-semibold">Dashboard-a qayıt</Link>
    </div>
  )
}
