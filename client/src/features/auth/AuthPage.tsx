import { useState, type FormEvent } from 'react'
import { Button } from '../../components/common/Button'
import { FormField } from '../../components/common/FormField'
import { ApiError } from '../../lib/api-client'
import { cn } from '../../lib/utils'
import { useLogin, useRegister } from './api'

type Mode = 'login' | 'register'

export function AuthPage() {
  const [mode, setMode] = useState<Mode>('login')
  const login = useLogin()
  const register = useRegister()
  const mutation = mode === 'login' ? login : register

  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [displayName, setDisplayName] = useState('')

  const error = mutation.error instanceof ApiError ? mutation.error : undefined
  const fieldError = (name: string) => error?.problem?.errors?.[name]?.join(' ')
  const generalError = mutation.error && !error?.problem?.errors ? mutation.error.message : undefined

  function switchMode(next: Mode) {
    setMode(next)
    login.reset()
    register.reset()
  }

  function submit(event: FormEvent) {
    event.preventDefault()
    if (mode === 'login') login.mutate({ email, password })
    else register.mutate({ email, password, displayName })
  }

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

        <div className="rounded-xl border border-slate-200 bg-white p-6 shadow-sm">
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
                error={fieldError('displayName')}
                required
              />
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
            <FormField
              label="Şifrə"
              type="password"
              autoComplete={mode === 'login' ? 'current-password' : 'new-password'}
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              error={fieldError('password')}
              required
            />
            {mode === 'register' && <p className="-mt-2 text-slate-500">Ən azı 8 simvol, böyük/kiçik hərf və rəqəm.</p>}

            {generalError && (
              <p role="alert" className="rounded-lg bg-rose-50 px-3 py-2 text-rose-700">
                {generalError}
              </p>
            )}

            <Button type="submit" loading={mutation.isPending} className="mt-2 w-full">
              {mode === 'login' ? 'Daxil ol' : 'Hesab yarat'}
            </Button>
          </form>
        </div>
      </div>
    </div>
  )
}
