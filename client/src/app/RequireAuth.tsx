import { Loader2 } from 'lucide-react'
import type { ReactNode } from 'react'
import { AuthPage } from '../features/auth/AuthPage'
import { useCurrentUser } from '../features/auth/api'

/** Shows the sign-in screen until the user is authenticated. */
export function RequireAuth({ children }: { children: ReactNode }) {
  const { data: user, isPending, isError, refetch } = useCurrentUser()

  if (isPending) {
    return (
      <div className="flex min-h-dvh items-center justify-center text-slate-500">
        <Loader2 className="size-6 animate-spin" aria-label="Yüklənir" />
      </div>
    )
  }

  if (isError) {
    return (
      <div className="flex min-h-dvh flex-col items-center justify-center gap-3 px-4 text-center">
        <p className="font-medium">Serverə qoşulmaq mümkün olmadı.</p>
        <p className="text-slate-600">API-nin işlədiyini yoxlayın (http://localhost:5180).</p>
        <button type="button" onClick={() => refetch()} className="font-medium text-indigo-600 hover:text-indigo-700">
          Yenidən cəhd et
        </button>
      </div>
    )
  }

  return user ? children : <AuthPage />
}
