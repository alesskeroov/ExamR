import { CheckCircle2 } from 'lucide-react'
import { useState, type FormEvent } from 'react'
import { Link, useSearchParams } from 'react-router'
import { Button } from '../../components/common/Button'
import { fieldErrors } from '../../lib/form-errors'
import { useResetPassword } from './api'
import { AuthPasswordField, AuthShell } from './AuthPage'

/** Opened from the emailed link: /reset-password?uid=…&token=… */
export function ResetPasswordPage() {
  const [searchParams] = useSearchParams()
  const userId = searchParams.get('uid')
  const token = searchParams.get('token')

  const reset = useResetPassword()
  const [password, setPassword] = useState('')
  const [confirm, setConfirm] = useState('')
  const [mismatch, setMismatch] = useState(false)
  const errors = fieldErrors(reset.error)

  function submit(event: FormEvent) {
    event.preventDefault()
    if (password !== confirm) {
      setMismatch(true)
      return
    }
    setMismatch(false)
    reset.mutate({ userId: userId!, token: token!, newPassword: password })
  }

  return (
    <AuthShell>
      <h2 className="text-[28px] font-bold leading-tight tracking-[-0.035em]">Yeni şifrə təyin et</h2>

      {!userId || !token ? (
        <p role="alert" className="mt-4 rounded-lg bg-rose-50 px-3 py-2 text-rose-700">
          Link natamamdır. Emaildəki linki tam açın və ya yeni link tələb edin.
        </p>
      ) : reset.isSuccess ? (
        <div className="mt-4 flex flex-col gap-4">
          <p role="status" className="flex gap-3 rounded-lg bg-emerald-50 px-3 py-3 text-emerald-800">
            <CheckCircle2 className="mt-0.5 size-5 shrink-0" aria-hidden />
            Şifrə yeniləndi. Bütün cihazlardan çıxış edildi — yeni şifrə ilə daxil olun.
          </p>
          <Link to="/?auth=login" className="auth-submit inline-flex h-[46px] items-center justify-center font-semibold">
            Girişə keç
          </Link>
        </div>
      ) : (
        <form onSubmit={submit} className="mt-4 flex flex-col gap-4" noValidate>
          <AuthPasswordField
            label="Yeni şifrə"
            type="password"
            autoComplete="new-password"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            error={errors.field('password') ?? errors.field('newPassword')}
            required
          />
          <AuthPasswordField
            label="Yeni şifrə (təkrar)"
            type="password"
            autoComplete="new-password"
            value={confirm}
            onChange={(e) => setConfirm(e.target.value)}
            error={mismatch ? 'Şifrələr eyni deyil.' : undefined}
            required
          />
          <p className="-mt-2 text-slate-500">Ən azı 8 simvol, böyük/kiçik hərf və rəqəm.</p>
          {errors.general && (
            <p role="alert" className="rounded-lg bg-rose-50 px-3 py-2 text-rose-700">
              {errors.general}{' '}
              <Link to="/?auth=login" className="font-semibold underline">
                Yeni link tələb et
              </Link>
            </p>
          )}
          <Button type="submit" loading={reset.isPending} className="auth-submit w-full">
            Şifrəni yenilə
          </Button>
        </form>
      )}
    </AuthShell>
  )
}
