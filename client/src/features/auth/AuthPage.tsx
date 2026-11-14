import { ArrowLeft, Check, Eye, EyeOff, FlaskConical, MailCheck, Moon, Sun } from 'lucide-react'
import { useEffect, useState, type ComponentProps, type FormEvent, type ReactNode } from 'react'
import { Link } from 'react-router'
import { Button } from '../../components/common/Button'
import { FormField } from '../../components/common/FormField'
import { SupportContact } from '../../components/common/SupportContact'
import { fieldErrors } from '../../lib/form-errors'
import { cn } from '../../lib/utils'
import { useForgotPassword, useLogin, useRegister, useResendConfirmation } from './api'
import { ApiError } from '../../lib/api-client'
import { ProductPreview } from '../landing/ProductPreview'
import { markFreshLogin } from '../../lib/fresh-login'
import './auth.css'
import { useTheme } from '../../lib/use-theme'

type Mode = 'login' | 'register' | 'forgot'

export function AuthPage({ initialMode = 'login' }: { initialMode?: 'login' | 'register' }) {
  const [mode, setMode] = useState<Mode>(initialMode)

  return (
    <AuthShell>
      {mode === 'forgot' ? (
        <ForgotPasswordForm onBack={() => setMode('login')} />
      ) : (
        <SignInForm mode={mode} onModeChange={setMode} />
      )}
    </AuthShell>
  )
}

/** Authentication layout using the shared application theme. */
export function AuthShell({ children }: { children: ReactNode }) {
  const { dark, toggleTheme } = useTheme()
  // Client-side navigation keeps the landing scroll position; the form should start at the top.
  useEffect(() => { window.scrollTo(0, 0) }, [])
  return (
    <div className="auth-page" data-theme={dark ? 'dark' : 'light'}>
      <header className="auth-header mx-auto flex w-full max-w-7xl items-center justify-between px-6 py-6 sm:px-10">
        <Link viewTransition to="/" className="flex items-center gap-2.5 text-lg font-bold tracking-[-0.03em]" aria-label="ExamR ana səhifə">
          <span className="auth-logo-mark grid size-[30px] place-items-center rounded-lg" aria-hidden><svg viewBox="0 0 16 16" className="size-4"><path d="M4 4h8M4 8h5.5M4 12h8" stroke="currentColor" strokeWidth="2" strokeLinecap="round" /></svg></span>ExamR
        </Link>
        <div className="flex items-center gap-3">
          <Link viewTransition to="/" className="auth-link hidden text-sm font-medium sm:inline">← Ana səhifə</Link>
          <button type="button" onClick={toggleTheme} aria-pressed={dark} aria-label={dark ? 'İşıqlı rejimə keç' : 'Qaranlıq rejimə keç'} className="auth-theme-toggle grid size-10 place-items-center rounded-full border transition-colors">
            {dark ? <Sun className="size-[18px]" aria-hidden /> : <Moon className="size-[18px]" aria-hidden />}
          </button>
        </div>
      </header>
      <main className="auth-main mx-auto grid w-full max-w-7xl flex-1 items-center gap-10 px-6 pb-10 pt-4 sm:px-10 lg:grid-cols-[1.1fr_1fr] lg:gap-16 lg:py-8">
        <section className="auth-story hidden self-center lg:block" aria-labelledby="auth-story-title">
          <p className="auth-eyebrow-mono">PDF · DOCX → imtahan</p>
          <h1 id="auth-story-title" className="mt-4 max-w-lg text-[clamp(2.4rem,3.6vw,3.3rem)] font-bold leading-[1.04] tracking-[-0.045em]">Konspektin artıq <span className="auth-mark">imtahandır.</span></h1>
          <p className="auth-muted mt-5 max-w-md text-base leading-7">Sənədini yüklə, vaxtlı imtahan ver, səhvlərini təkrar et. Qaldığın yerdən davam et.</p>
          <div className="mt-10 max-w-[520px]"><ProductPreview /></div>
        </section>
        <section className="auth-form-card mx-auto w-full max-w-[460px] rounded-3xl border p-6 sm:p-9" aria-label="Hesaba giriş və qeydiyyat">
          {/* Shown until the build sets VITE_DEMO_MODE=false. */}
          {import.meta.env.VITE_DEMO_MODE !== 'false' && <div role="note" className="auth-demo-banner mb-6 flex items-start gap-3 rounded-xl border px-3.5 py-3 text-xs leading-5">
            <FlaskConical className="mt-0.5 size-4 shrink-0" aria-hidden />
            <span><strong className="font-semibold">Demo versiya.</strong> ExamR hazırda sınaq mərhələsindədir: bəzi funksiyalar dəyişə və məlumatlar sıfırlana bilər. Həssas və ya şəxsi sənədlər yükləməyin.</span>
          </div>}
          <div className="auth-form-content">{children}</div>
          <div className="auth-form-footer mt-6 border-t pt-5 text-center text-xs">Davam etməklə hesabının yalnız sənə görünən şəxsi kitabxanaya sahib olacağını qəbul edirsən. <Link viewTransition to="/" className="auth-link font-medium sm:hidden">← Ana səhifə</Link></div>
        </section>
      </main>
      <footer className="auth-muted mx-auto flex w-full max-w-7xl justify-between gap-4 px-6 py-5 text-[11px] sm:px-10"><span>ExamR · Öyrənməyə davam et.</span><span>Diqqətin biliyində qalsın.</span></footer>
    </div>
  )
}

export function AuthPasswordField(props: ComponentProps<typeof FormField>) {
  const [visible, setVisible] = useState(false)
  return <div className="auth-password relative">
    <FormField {...props} type={visible ? 'text' : 'password'} className={cn('pr-12', props.className)} />
    <button type="button" aria-label={visible ? 'Şifrəni gizlət' : 'Şifrəni göstər'} aria-pressed={visible} onClick={() => setVisible(value => !value)} className="auth-password-toggle absolute right-2 top-[31px] grid size-8 place-items-center rounded-lg">
      {visible ? <EyeOff className="size-[18px]" aria-hidden /> : <Eye className="size-[18px]" aria-hidden />}
    </button>
  </div>
}

function SignInForm({ mode, onModeChange }: { mode: 'login' | 'register'; onModeChange: (mode: Mode) => void }) {
  const login = useLogin()
  const register = useRegister()
  const mutation = mode === 'login' ? login : register

  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [displayName, setDisplayName] = useState('')
  const [rememberMe, setRememberMe] = useState(false)
  const [localErrors, setLocalErrors] = useState<Record<string, string>>({})
  const errors = fieldErrors(mutation.error)
  const fieldError = (name: string) => localErrors[name] ?? errors.field(name)
  const unconfirmed = mode === 'login' && login.error instanceof ApiError && login.error.problem?.code === 'email_not_confirmed'
  const [googleError] = useState(() => {
    // The Google callback redirects back with ?authError=…; show it once and clean the URL.
    const code = new URLSearchParams(window.location.search).get('authError')
    if (code) window.history.replaceState(null, '', window.location.pathname)
    return code ? googleErrors[code] ?? 'Google ilə giriş alınmadı. Yenidən cəhd edin.' : null
  })

  function switchMode(next: Mode) {
    onModeChange(next)
    login.reset()
    register.reset()
    setLocalErrors({})
  }

  function submit(event: FormEvent) {
    event.preventDefault()
    // Client checks only give faster feedback; the API validates everything again.
    const problems: Record<string, string> = {}
    if (mode === 'register' && !displayName.trim()) problems.displayName = 'Adınızı daxil edin.'
    if (!email.trim()) problems.email = 'E-mail ünvanını daxil edin.'
    else if (!/^[^\s@]+@[^\s@]+\.[^\s@]{2,}$/.test(email.trim())) problems.email = 'E-mail ünvanı düzgün formatda deyil.'
    if (!password) problems.password = 'Şifrəni daxil edin.'
    else if (mode === 'register' && !(password.length >= 8 && /[a-z]/.test(password) && /[A-Z]/.test(password) && /\d/.test(password)))
      problems.password = 'Şifrə ən azı 8 simvol olmalı, böyük və kiçik hərf, rəqəm daxil etməlidir.'
    setLocalErrors(problems)
    if (Object.keys(problems).length) return
    if (mode === 'login') login.mutate({ email, password, rememberMe })
    else register.mutate({ email, password, displayName })
  }

  if (mode === 'register' && register.isSuccess) {
    return <VerifyPending email={register.data.email} onBack={() => { register.reset(); switchMode('login') }} />
  }

  return (
    <>
      <div key={mode} data-mode={mode} className="auth-mode-enter mb-6 min-h-[80px]">
        <p className="auth-eyebrow-mono mb-3">{mode === 'login' ? 'Yenidən xoş gördük' : 'Yeni başlanğıc'}</p>
        <h2 className="text-[28px] font-bold leading-tight tracking-[-0.035em]">{mode === 'login' ? 'Öyrənməyə davam et.' : 'İlk addımını at.'}</h2>
        <p className="auth-muted mt-2 text-sm">{mode === 'login' ? 'Hesabına daxil ol, qaldığın yerdən davam et.' : 'ExamR hesabını yarat və hazırlığa başla.'}</p>
      </div>
      <div role="tablist" data-mode={mode} aria-label="Giriş növü" className="auth-tabs mb-6 grid grid-cols-2 rounded-xl p-1">
        {(['login', 'register'] as const).map((m) => (
          <button
            key={m}
            type="button"
            role="tab"
            disabled={mutation.isPending}
            aria-selected={mode === m}
            onClick={() => switchMode(m)}
            className={cn(
              'h-10 rounded-lg text-sm font-medium transition-colors duration-150 disabled:opacity-60',
              mode === m ? 'auth-tab-active' : 'auth-muted',
            )}
          >
            {m === 'login' ? 'Daxil ol' : 'Qeydiyyat'}
          </button>
        ))}
      </div>

      {new URLSearchParams(window.location.search).get('account') === 'password-changed' && <p role="status" className="mb-4 rounded-lg bg-emerald-50 p-3 text-sm text-emerald-800">Şifrə dəyişdirildi. Yeni şifrə ilə daxil olun.</p>}
      {new URLSearchParams(window.location.search).get('account') === 'deleted' && <p role="status" className="mb-4 rounded-lg bg-emerald-50 p-3 text-sm text-emerald-800">Hesab bağlandı və profil anonimləşdirildi.</p>}
      <form onSubmit={submit} className="flex flex-col gap-4" noValidate>
        {mode === 'register' && (
          <FormField label="Ad" autoComplete="name" value={displayName} onChange={e => setDisplayName(e.target.value)} error={fieldError('displayName')} maxLength={100} required />
        )}
        <FormField
          label="Email"
          type="email"
          autoComplete="email"
          value={email}
          onChange={(e) => setEmail(e.target.value)}
          error={fieldError('email')}
          required
        />
        <AuthPasswordField
          label="Şifrə"
          type="password"
          autoComplete={mode === 'login' ? 'current-password' : 'new-password'}
          value={password}
          onChange={(e) => setPassword(e.target.value)}
          error={fieldError('password')}
          required
        />
        <div className="auth-mode-slot">
          <div className="auth-mode-panel" data-active={mode === 'register'} aria-hidden={mode !== 'register'}>
            <div className="auth-mode-panel-inner space-y-2">
            <p className="auth-muted text-xs">Şifrə: ən azı 8 simvol, böyük/kiçik hərf və rəqəm.</p>
            </div>
          </div>
          <div className="auth-mode-panel" data-active={mode === 'login'} aria-hidden={mode !== 'login'}>
            <div className="auth-mode-panel-inner flex flex-wrap items-center justify-between gap-3">
              <label className="auth-remember-option">
                <input type="checkbox" checked={rememberMe} onChange={event => setRememberMe(event.target.checked)} disabled={mode !== 'login' || mutation.isPending} className="auth-remember" />
                <span className="auth-remember-box" aria-hidden="true"><Check className="size-3" /></span>
                Məni xatırla
              </label>
              <button type="button" disabled={mode !== 'login' || mutation.isPending} onClick={() => switchMode('forgot')} className="auth-link text-xs font-medium">Şifrəmi unutdum</button>
            </div>
          </div>
        </div>
        <div className={errors.general || googleError ? undefined : 'hidden'} aria-live="polite">
          {(errors.general ?? googleError) && <p role="alert" className="rounded-lg bg-rose-50 px-3 py-2 text-rose-700">{errors.general ?? googleError}</p>}
          {unconfirmed && <div className="mt-2"><ResendLink email={email} /></div>}
          {/* Signed-out users cannot use the in-app form, so a failed Google sign-in or a locked account shows the address. */}
          {googleError && !errors.general && <SupportContact className="auth-muted mt-2" topic="Giriş problemi" lead="Problem davam etsə, bizə yazın:" />}
        </div>

        <Button type="submit" loading={mutation.isPending} className="auth-submit w-full">
          <span key={mode} className="auth-submit-label">{mode === 'login' ? 'Daxil ol' : 'Hesab yarat'}</span>
        </Button>
        <div className="auth-muted flex items-center gap-3 text-xs" aria-hidden><span className="h-px flex-1 bg-current opacity-20" />və ya<span className="h-px flex-1 bg-current opacity-20" /></div>
        <a href={`/api/auth/google?rememberMe=${mode === 'login' && rememberMe}`} onClick={markFreshLogin} className="auth-google flex h-[46px] w-full items-center justify-center gap-3 rounded-[10px] border text-sm font-medium transition-colors">
          <GoogleLogo />Google ilə davam et
        </a>
      </form>
    </>
  )
}

function ForgotPasswordForm({ onBack }: { onBack: () => void }) {
  const forgot = useForgotPassword()
  const [email, setEmail] = useState('')
  const errors = fieldErrors(forgot.error)

  return (
    <>
      <button type="button" onClick={onBack} className="mb-4 inline-flex items-center gap-1.5 font-medium text-slate-600 hover:text-slate-900">
        <ArrowLeft className="size-4" aria-hidden />
        Girişə qayıt
      </button>
      <h2 className="text-[28px] font-bold leading-tight tracking-[-0.035em]">Şifrəni bərpa et</h2>

      {forgot.isSuccess ? (
        <div className="mt-4 flex flex-col gap-4">
          <div role="status" className="flex gap-3 rounded-lg bg-emerald-50 px-3 py-3 text-emerald-800">
            <MailCheck className="mt-0.5 size-5 shrink-0" aria-hidden />
            <p>{forgot.data.message} Spam qovluğunu da yoxla.</p>
          </div>
          <ResendButton onResend={() => forgot.mutate(email)} busy={forgot.isPending} />
          <SupportContact className="auth-muted text-center" topic="Şifrə bərpası" lead="Email gəlmirsə, bizə yazın:" />
          <button type="button" onClick={() => forgot.reset()} className="auth-link text-sm font-medium">Başqa email daxil et</button>
        </div>
      ) : (
        <form
          onSubmit={(e) => {
            e.preventDefault()
            forgot.mutate(email)
          }}
          className="mt-2 flex flex-col gap-4"
          noValidate
        >
          <p className="text-slate-600">Hesabınızın email ünvanını daxil edin — şifrəni yeniləmək üçün link göndərəcəyik.</p>
          <FormField
            label="Email"
            type="email"
            autoComplete="email"
            value={email}
            onChange={(e) => setEmail(e.target.value)}
            error={errors.field('email')}
            required
          />
          {errors.general && (
            <p role="alert" className="rounded-lg bg-rose-50 px-3 py-2 text-rose-700">
              {errors.general}
            </p>
          )}
          <Button type="submit" loading={forgot.isPending} className="auth-submit w-full">
            Link göndər
          </Button>
        </form>
      )}
    </>
  )
}

const googleErrors: Record<string, string> = {
  google_disabled: 'Google ilə giriş hələ konfiqurasiya edilməyib.',
  google_cancelled: 'Google girişi ləğv edildi və ya bu Google hesabına icazə verilmədi.',
  google_failed: 'Google ilə giriş alınmadı. Yenidən cəhd edin.',
  google_unverified: 'Google hesabınızın emaili təsdiqlənməyib.',
  account_exists: 'Bu email ilə hesab artıq var. Şifrə ilə daxil olun və Google-u Profil səhifəsindən əlaqələndirin.',
  locked: 'Hesab müvəqqəti bloklanıb. Bir az sonra yenidən cəhd edin.',
}

function GoogleLogo() {
  return <svg viewBox="0 0 48 48" className="size-5" aria-hidden>
    <path fill="#FFC107" d="M43.6 20.5H42V20H24v8h11.3C33.7 32.7 29.2 36 24 36c-6.6 0-12-5.4-12-12s5.4-12 12-12c3.1 0 5.8 1.2 7.9 3.1l5.7-5.7C34 6.1 29.3 4 24 4 12.9 4 4 12.9 4 24s8.9 20 20 20 20-8.9 20-20c0-1.3-.1-2.4-.4-3.5z" />
    <path fill="#FF3D00" d="m6.3 14.7 6.6 4.8C14.7 15.1 19 12 24 12c3.1 0 5.8 1.2 7.9 3.1l5.7-5.7C34 6.1 29.3 4 24 4 16.3 4 9.7 8.3 6.3 14.7z" />
    <path fill="#4CAF50" d="M24 44c5.2 0 9.9-2 13.4-5.2l-6.2-5.2C29.2 35.1 26.7 36 24 36c-5.2 0-9.6-3.3-11.3-7.9l-6.5 5C9.5 39.6 16.2 44 24 44z" />
    <path fill="#1976D2" d="M43.6 20.5H42V20H24v8h11.3c-.8 2.2-2.2 4.2-4.1 5.6l6.2 5.2C37 39.2 44 34 44 24c0-1.3-.1-2.4-.4-3.5z" />
  </svg>
}

/** Resend with a 60-second cooldown so a double click cannot flood the mailbox (the API rate-limits as well). */
export function ResendButton({ onResend, busy }: { onResend: () => void; busy: boolean }) {
  const [left, setLeft] = useState(60)
  useEffect(() => {
    if (left <= 0) return
    const timer = setTimeout(() => setLeft(s => s - 1), 1000)
    return () => clearTimeout(timer)
  }, [left])
  return <Button variant="secondary" className="w-full" disabled={left > 0} loading={busy} onClick={() => { onResend(); setLeft(60) }}>
    {left > 0 ? `Yenidən göndər (${left} san)` : 'Linki yenidən göndər'}
  </Button>
}

const RESEND_COOLDOWN = 60

/** Resend button with a visible cooldown; the API is rate limited and answers the same for unknown addresses. */
function ResendLink({ email }: { email: string }) {
  const resend = useResendConfirmation()
  const [left, setLeft] = useState(0)
  useEffect(() => {
    if (left <= 0) return
    const timer = setTimeout(() => setLeft(value => value - 1), 1000)
    return () => clearTimeout(timer)
  }, [left])
  const send = () => resend.mutate(email, { onSettled: () => setLeft(RESEND_COOLDOWN) })
  return (
    <div className="flex flex-col gap-2" aria-live="polite">
      <Button type="button" variant="secondary" loading={resend.isPending} disabled={left > 0 || !email.trim()} onClick={send} className="w-full">
        {left > 0 ? `Yenidən göndər (${left} s)` : 'Təsdiqləmə məktubunu yenidən göndər'}
      </Button>
      {resend.isSuccess && <p role="status" className="text-sm text-emerald-700">Məktub yenidən göndərildi. Gələnlər və Spam qovluğunu yoxlayın.</p>}
      {resend.isError && <p role="alert" className="text-sm text-rose-700">{resend.error.message}</p>}
    </div>
  )
}

function VerifyPending({ email, onBack }: { email: string; onBack: () => void }) {
  return (
    <div className="auth-mode-enter flex flex-col gap-5">
      <span className="grid size-12 place-items-center rounded-xl bg-indigo-50 text-indigo-700" aria-hidden><MailCheck className="size-6" /></span>
      <div>
        <h2 className="text-[28px] font-bold leading-tight tracking-[-0.035em]">E-mail ünvanınızı təsdiqləyin</h2>
        <p className="auth-muted mt-2 text-sm">
          Qeydiyyatınızı tamamlamaq üçün <strong className="break-all">{email}</strong> ünvanına təsdiqləmə linki göndərdik. Link 24 saat etibarlıdır.
        </p>
      </div>
      <p className="auth-muted text-sm">E-mail gəlməyibsə, Spam qovluğunu yoxlayın və ya yenidən göndərin:</p>
      <ResendLink email={email} />
      <button type="button" onClick={onBack} className="auth-link text-center text-sm font-medium">Girişə qayıt</button>
    </div>
  )
}
