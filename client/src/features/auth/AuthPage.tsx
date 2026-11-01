import { ArrowLeft, MailCheck } from 'lucide-react'
import { useState, type FormEvent, type ReactNode } from 'react'
import { Button } from '../../components/common/Button'
import { FormField } from '../../components/common/FormField'
import { fieldErrors } from '../../lib/form-errors'
import { cn } from '../../lib/utils'
import { useForgotPassword, useLogin, useRegister } from './api'

type Mode = 'login' | 'register' | 'forgot'

export function AuthPage() {
  const [mode, setMode] = useState<Mode>('login')

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

/** Centered card layout shared by the sign-in and password-reset screens. */
export function AuthShell({ children }: { children: ReactNode }) {
  return (
    <div className="flex min-h-dvh items-center justify-center bg-slate-50 px-4 py-12">
      <div className="w-full max-w-sm">
        <div className="mb-8 flex flex-col items-center gap-3 text-center">
          <img src="/favicon.svg" alt="" className="size-10" />
          <div>
            <h1 className="text-2xl font-semibold tracking-tight">ExamFlow</h1>
            <p className="mt-1 text-slate-600">PDF-dən sınaq imtahanları</p>
          </div>
        </div>
        <div className="rounded-xl border border-slate-200 bg-white p-6 shadow-sm">{children}</div>
      </div>
    </div>
  )
}

function SignInForm({ mode, onModeChange }: { mode: 'login' | 'register'; onModeChange: (mode: Mode) => void }) {
  const login = useLogin()
  const register = useRegister()
  const mutation = mode === 'login' ? login : register

  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [displayName, setDisplayName] = useState('')
  const errors = fieldErrors(mutation.error)

  function switchMode(next: Mode) {
    onModeChange(next)
    login.reset()
    register.reset()
  }

  function submit(event: FormEvent) {
    event.preventDefault()
    if (mode === 'login') login.mutate({ email, password })
    else register.mutate({ email, password, displayName })
  }

  return (
    <>
      <div role="tablist" aria-label="Giriş növü" className="mb-6 grid grid-cols-2 rounded-lg bg-slate-100 p-1">
        {(['login', 'register'] as const).map((m) => (
          <button
            key={m}
            type="button"
            role="tab"
            aria-selected={mode === m}
            onClick={() => switchMode(m)}
            className={cn(
              'h-8 rounded-md font-medium transition-colors duration-150',
              mode === m ? 'bg-white text-slate-900 shadow-sm' : 'text-slate-600 hover:text-slate-900',
            )}
          >
            {m === 'login' ? 'Daxil ol' : 'Qeydiyyat'}
          </button>
        ))}
      </div>

      <form onSubmit={submit} className="flex flex-col gap-4" noValidate>
        {mode === 'register' && (
          <FormField
            label="Ad"
            autoComplete="name"
            value={displayName}
            onChange={(e) => setDisplayName(e.target.value)}
            error={errors.field('displayName')}
            required
          />
        )}
        <FormField
          label="Email"
          type="email"
          autoComplete="email"
          value={email}
          onChange={(e) => setEmail(e.target.value)}
          error={errors.field('email')}
          required
        />
        <FormField
          label="Şifrə"
          type="password"
          autoComplete={mode === 'login' ? 'current-password' : 'new-password'}
          value={password}
          onChange={(e) => setPassword(e.target.value)}
          error={errors.field('password')}
          required
        />
        {mode === 'register' && <p className="-mt-2 text-slate-500">Ən azı 8 simvol, böyük/kiçik hərf və rəqəm.</p>}
        {mode === 'login' && (
          <button
            type="button"
            onClick={() => switchMode('forgot')}
            className="-mt-2 self-end font-medium text-indigo-600 hover:text-indigo-700"
          >
            Şifrəmi unutdum
          </button>
        )}

        {errors.general && (
          <p role="alert" className="rounded-lg bg-rose-50 px-3 py-2 text-rose-700">
            {errors.general}
          </p>
        )}

        <Button type="submit" loading={mutation.isPending} className="mt-2 w-full">
          {mode === 'login' ? 'Daxil ol' : 'Hesab yarat'}
        </Button>
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
      <h2 className="text-lg font-semibold">Şifrəni bərpa et</h2>

      {forgot.isSuccess ? (
        <div role="status" className="mt-4 flex gap-3 rounded-lg bg-emerald-50 px-3 py-3 text-emerald-800">
          <MailCheck className="mt-0.5 size-5 shrink-0" aria-hidden />
          <p>{forgot.data.message}</p>
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
          <Button type="submit" loading={forgot.isPending} className="w-full">
            Link göndər
          </Button>
        </form>
      )}
    </>
  )
}
