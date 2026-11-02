import { RefreshCw, ServerCrash, WifiOff } from 'lucide-react'
import { useEffect, useState, type ReactNode } from 'react'
import { AuthPage } from '../features/auth/AuthPage'
import { LandingPage } from '../features/landing/LandingPage'
import { useSearchParams } from 'react-router'
import { useCurrentUser } from '../features/auth/api'
import { SocialConnection } from '../features/friends/SocialConnection'

function useOnline() {
  const [online, setOnline] = useState(() => navigator.onLine)
  useEffect(() => {
    const update = () => setOnline(navigator.onLine)
    window.addEventListener('online', update)
    window.addEventListener('offline', update)
    return () => { window.removeEventListener('online', update); window.removeEventListener('offline', update) }
  }, [])
  return online
}

/** Shows the sign-in screen until the user is authenticated. */
export function RequireAuth({ children }: { children: ReactNode }) {
  const { data: user, isPending, isError, fetchStatus, refetch, isFetching } = useCurrentUser()
  const online = useOnline()
  const [searchParams] = useSearchParams()

  // Offline, TanStack Query pauses the request and isPending would stay true forever; it resumes on reconnect.
  if ((isPending && fetchStatus === 'paused') || (isError && !online)) {
    return <StatusScreen icon={<WifiOff className="size-7" aria-hidden />} title="İnternet bağlantısı yoxdur"
      description="Bağlantı bərpa olunduqda səhifə avtomatik yenilənəcək." busy={isFetching} onRetry={() => refetch()} />
  }

  if (isPending) {
    return (
      <div role="status" aria-label="Yüklənir" className="flex min-h-dvh flex-col items-center justify-center gap-5">
        <img src="/favicon.svg" alt="" className="app-loader-logo size-12" />
        <div className="app-loader-bar h-1 w-40 overflow-hidden rounded-full bg-indigo-100" />
      </div>
    )
  }

  if (isError) {
    return <StatusScreen icon={<ServerCrash className="size-7" aria-hidden />} title="Serverə qoşulmaq mümkün olmadı"
      description="Bir az sonra yenidən cəhd edin." busy={isFetching} onRetry={() => refetch()} />
  }

  if (user) return <><SocialConnection key={user.id} />{children}</>
  // Signed-out visitors see the public landing page; ?auth=login|register (or a Google error) opens the form.
  const auth = searchParams.get('auth')
  return auth === 'login' || auth === 'register' || searchParams.has('authError')
    ? <AuthPage key={auth} initialMode={auth === 'register' ? 'register' : 'login'} />
    : <LandingPage />
}

function StatusScreen({ icon, title, description, busy, onRetry }: { icon: ReactNode; title: string; description: string; busy: boolean; onRetry: () => void }) {
  return (
    <div role="alert" className="flex min-h-dvh flex-col items-center justify-center gap-3 px-4 text-center">
      <span className="mb-2 grid size-16 place-items-center rounded-2xl bg-indigo-50 text-indigo-600">{icon}</span>
      <p className="text-lg font-semibold">{title}</p>
      <p className="max-w-sm text-slate-600">{description}</p>
      <button type="button" disabled={busy} onClick={onRetry} className="mt-2 inline-flex h-10 items-center gap-2 rounded-xl btn-primary px-4 text-sm font-medium transition-colors disabled:opacity-60">
        <RefreshCw className="size-4" aria-hidden />{busy ? 'Yoxlanılır…' : 'Yenidən cəhd et'}
      </button>
    </div>
  )
}
