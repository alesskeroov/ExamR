import { MutationCache, QueryCache, QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { ConfirmProvider } from '../components/common/ConfirmProvider'
import { ErrorBoundary } from '../components/common/ErrorBoundary'
import { ServerStartup } from '../components/common/ServerStartup'
import { RouterProvider } from 'react-router'
import { ApiError } from '../lib/api-client'
import { clearToken } from '../lib/auth-token'
import { pausePollingWhenIdle } from '../lib/idle-polling'
import { queryKeys } from '../lib/query-keys'
import { useTheme } from '../lib/use-theme'
import { router } from './router'

// A 401 anywhere means the token expired or was revoked: drop it so the sign-in screen shows.
function handleUnauthorized(error: unknown) {
  if (error instanceof ApiError && error.status === 401) {
    clearToken()
    queryClient.setQueryData(queryKeys.me, null)
  }
}

const queryClient: QueryClient = new QueryClient({
  queryCache: new QueryCache({ onError: handleUnauthorized }),
  mutationCache: new MutationCache({ onError: handleUnauthorized }),
  defaultOptions: {
    queries: {
      staleTime: 30_000,
      retry: (count, error) => !(error instanceof ApiError && error.status < 500) && count < 1,
      refetchOnWindowFocus: false,
    },
  },
})

pausePollingWhenIdle()

export function Providers() {
  useTheme()
  return (
    <QueryClientProvider client={queryClient}>
      <ErrorBoundary><ServerStartup><ConfirmProvider><RouterProvider router={router} /></ConfirmProvider></ServerStartup></ErrorBoundary>
    </QueryClientProvider>
  )
}
