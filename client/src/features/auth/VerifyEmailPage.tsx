import { CheckCircle2, Loader2, MailX } from 'lucide-react'
import { useEffect, useRef, useState } from 'react'
import { Link, useSearchParams } from 'react-router'
import { FormField } from '../../components/common/FormField'
import { Button } from '../../components/common/Button'
import { SupportContact } from '../../components/common/SupportContact'
import { AuthShell } from './AuthPage'
import { useConfirmEmail, useCurrentUser, useResendConfirmation } from './api'
import { useTranslation } from 'react-i18next'

/** Opened from the verification email: confirms once, then offers the next step or a fresh link. */
export function VerifyEmailPage() {
  const [params] = useSearchParams()
  const userId = params.get('uid') ?? ''
  const token = params.get('token') ?? ''
  const confirm = useConfirmEmail()
  const { data: user } = useCurrentUser()
  const resend = useResendConfirmation()
  const [email, setEmail] = useState('')
  const sent = useRef(false)
  const { t } = useTranslation()

  useEffect(() => {
    // StrictMode runs effects twice in development; the token must be submitted only once.
    if (sent.current || !userId || !token) return
    sent.current = true
    confirm.mutate({ userId, token })
  }, [confirm, userId, token])

  const failed = !userId || !token || confirm.isError
  return (
    <AuthShell>
      <p className="auth-eyebrow-mono mb-3">{t('auth.verifyPage.eyebrow')}</p>
      {confirm.isSuccess ? (
        <div className="flex flex-col gap-5">
          <h2 className="text-[28px] font-bold leading-tight tracking-[-0.035em]">{t('auth.verifyPage.done')}</h2>
          <p role="status" className="flex gap-3 rounded-lg bg-emerald-50 px-3 py-3 text-emerald-800">
            <CheckCircle2 className="mt-0.5 size-5 shrink-0" aria-hidden />{confirm.data.message}
          </p>
          {/* Signed in (fresh confirmation or an existing session) → dashboard; otherwise the sign-in form. */}
          {user
            ? <Link to="/" replace className="auth-submit inline-flex h-[46px] items-center justify-center font-semibold">{t('auth.verifyPage.goApp')}</Link>
            : <Link to="/?auth=login" replace className="auth-submit inline-flex h-[46px] items-center justify-center font-semibold">{t('landing.login')}</Link>}
        </div>
      ) : failed ? (
        <div className="flex flex-col gap-4">
          <h2 className="text-[28px] font-bold leading-tight tracking-[-0.035em]">{t('auth.verifyPage.failed')}</h2>
          <p role="alert" className="flex gap-3 rounded-lg bg-rose-50 px-3 py-3 text-rose-700">
            <MailX className="mt-0.5 size-5 shrink-0" aria-hidden />
            {confirm.error?.message ?? t('auth.verifyPage.incomplete')}
          </p>
          {resend.isSuccess ? (
            <p role="status" className="rounded-lg bg-emerald-50 px-3 py-3 text-emerald-800">{resend.data.message}</p>
          ) : (
            <form className="flex flex-col gap-3" onSubmit={e => { e.preventDefault(); resend.mutate(email) }} noValidate>
              <FormField label="Email" type="email" autoComplete="email" value={email} onChange={e => setEmail(e.target.value)} required />
              <Button type="submit" loading={resend.isPending} disabled={!email.trim()} className="auth-submit w-full">{t('auth.verifyPage.sendNew')}</Button>
            </form>
          )}
          <SupportContact className="auth-muted text-center" topic={t('auth.verifyPage.eyebrow')} lead={t('auth.verifyPage.supportLead')} />
          <Link to="/?auth=login" className="auth-link text-center text-sm font-medium">{t('auth.backToLogin')}</Link>
        </div>
      ) : (
        <p role="status" className="flex items-center gap-3 text-base"><Loader2 className="size-5 animate-spin" aria-hidden />{t('auth.verifyPage.confirming')}</p>
      )}
    </AuthShell>
  )
}
