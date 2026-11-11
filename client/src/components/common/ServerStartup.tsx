import { useEffect, useState, type ReactNode } from 'react'
import { GraduationCap, Loader2 } from 'lucide-react'
import { checkServerLiveness } from '../../lib/api-client'
import { Alert } from './Alert'
import { Button } from './Button'

const retryInterval = 4_000
const requestTimeout = 15_000
const startupTimeout = 90_000

export function ServerStartup({ children }: { children: ReactNode }) {
  const [ready, setReady] = useState(false)
  const [attempt, setAttempt] = useState(0)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    let stopped = false
    let controller: AbortController
    let retryTimer: ReturnType<typeof setTimeout>
    let requestTimer: ReturnType<typeof setTimeout>
    let connectionError: string | null = null
    const startupTimer = setTimeout(() => {
      stopped = true
      controller.abort()
      clearTimeout(requestTimer)
      clearTimeout(retryTimer)
      setError(connectionError ?? '90 saniyə ərzində serverə qoşulmaq mümkün olmadı. Zəhmət olmasa yenidən cəhd edin.')
    }, startupTimeout)

    async function check() {
      controller = new AbortController()
      requestTimer = setTimeout(() => controller.abort(), requestTimeout)
      try {
        await checkServerLiveness(controller.signal)
        if (!stopped) {
          clearTimeout(startupTimer)
          setReady(true)
        }
      } catch (cause) {
        if (stopped) return
        // A cold start can return gateway errors or an HTML loading page before the API is ready.
        // Wait for this request to settle before scheduling another; never retry app/auth writes here.
        connectionError = !controller.signal.aborted && cause instanceof Error && !(cause instanceof TypeError)
          ? cause.message : null
        retryTimer = setTimeout(() => void check(), retryInterval)
      } finally {
        clearTimeout(requestTimer)
      }
    }

    void check()
    return () => {
      stopped = true
      controller.abort()
      clearTimeout(requestTimer)
      clearTimeout(retryTimer)
      clearTimeout(startupTimer)
    }
  }, [attempt])

  if (ready) return children

  return (
    <main className="flex min-h-dvh items-center justify-center bg-paper px-6 py-12 text-ink">
      <div className="w-full max-w-sm text-center">
        <div className="relative mx-auto mb-8 flex size-20 items-center justify-center rounded-3xl border border-line bg-surface shadow-sm">
          <GraduationCap className="size-9 text-accent" aria-hidden />
          {!error && <span className="absolute -right-1 -bottom-1 rounded-full border-4 border-paper bg-accent-soft p-1.5"><Loader2 className="size-4 text-accent motion-safe:animate-spin" aria-hidden /></span>}
        </div>
        <p className="mb-3 text-xs font-semibold tracking-[0.25em] text-faint">EXAMR</p>
        <h1 className="text-2xl font-semibold tracking-tight sm:text-3xl">{error ? 'Bağlantı alınmadı' : 'ExamR hazırlanır...'}</h1>
        {!error && <p className="mt-4 text-sm leading-6 text-muted">Serverə qoşuluruq, zəhmət olmasa gözləyin...</p>}
        {error ? (
          <div className="mt-6 text-left"><Alert title="Server bağlantısında xəta">{error}</Alert></div>
        ) : (
          <>
            <div role="progressbar" aria-label="Server bağlantısı yoxlanılır" className="mt-8 h-1 overflow-hidden rounded-full bg-line">
              <div className="server-startup-progress h-full w-1/3 rounded-full bg-accent" />
            </div>
            <p role="status" className="mt-4 text-xs text-muted">Bağlantı yoxlanılır...</p>
          </>
        )}
        {error && (
          <div className="mt-8">
            <Button variant="secondary" onClick={() => {
              setError(null)
              setAttempt(value => value + 1)
            }}>Yenidən cəhd et</Button>
          </div>
        )}
      </div>
    </main>
  )
}
