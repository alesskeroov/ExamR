import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api, ApiError } from '../../lib/api-client'
import { clearToken } from '../../lib/auth-token'
import { markFreshLogin } from '../../lib/fresh-login'
import { queryKeys } from '../../lib/query-keys'

export interface User {
  id: string
  email: string
  displayName: string
  nickname: string
  bio?: string | null
  hasPassword?: boolean
  avatarUrl: string | null
  roles: string[]
  googleLinked?: boolean
  emailConfirmed?: boolean
}

export function normalizeUser(user: User): User {
  return { ...user, nickname: user.nickname?.trim() || user.displayName || 'İstifadəçi', avatarUrl: user.avatarUrl ?? null }
}

interface AuthResponse {
  expiresAt: string
  user: User
}

export interface LoginRequest {
  email: string
  password: string
  rememberMe?: boolean
}

export interface RegisterRequest extends LoginRequest {
  displayName: string
}

export interface ResetPasswordRequest {
  userId: string
  token: string
  newPassword: string
}

/** Current user, or null when not signed in (no token, expired, or revoked). */
export function useCurrentUser() {
  return useQuery({
    queryKey: queryKeys.me,
    queryFn: async (): Promise<User | null> => {
      try {
        // A hanging request (connected Wi-Fi without internet) must not keep the app loading forever.
        return normalizeUser(await api.get<User>('/auth/me', { signal: AbortSignal.timeout(15_000) }))
      } catch (error) {
        if (error instanceof ApiError && error.status === 401) {
          clearToken()
          return null
        }
        throw error
      }
    },
    staleTime: Infinity,
  })
}

function useSignIn(path: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (request: LoginRequest | RegisterRequest) => api.post<AuthResponse>(path, request),
    // The API sets the HttpOnly session cookie (persistent only with Remember Me).
    onSuccess: ({ user }) => {
      queryClient.removeQueries({ predicate: (query) => query.queryKey[0] !== queryKeys.me[0] })
      clearToken()
      markFreshLogin()
      queryClient.setQueryData(queryKeys.me, normalizeUser(user))
    },
  })
}

export const useLogin = () => useSignIn('/auth/login')

/** Creates an unconfirmed account; no session until the emailed link confirms the address. */
export function useRegister() {
  return useMutation({
    mutationFn: (request: RegisterRequest) => api.post<{ email: string; message: string }>('/auth/register', request),
  })
}

export function useLogout() {
  const queryClient = useQueryClient()
  return useMutation({
    // The server revokes all existing tokens; the local token is dropped even if that call fails.
    mutationFn: () => api.post<void>('/auth/logout').catch(() => undefined),
    onSettled: () => {
      clearToken()
      // Keep the "me" query (RequireAuth observes it) and drop every other user's data.
      queryClient.setQueryData(queryKeys.me, null)
      queryClient.removeQueries({ predicate: (query) => query.queryKey[0] !== queryKeys.me[0] })
    },
  })
}

export function useForgotPassword() {
  return useMutation({
    mutationFn: (email: string) => api.post<{ message: string }>('/auth/forgot-password', { email }),
  })
}

export function useResetPassword() {
  return useMutation({
    mutationFn: (request: ResetPasswordRequest) => api.post<void>('/auth/reset-password', request),
  })
}

export function useResendConfirmation() {
  return useMutation({
    mutationFn: (email: string) => api.post<{ message: string }>('/auth/resend-confirmation', { email }),
  })
}

export function useConfirmEmail() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (request: { userId: string; token: string }) => api.post<{ message: string; user: User | null }>('/auth/confirm-email', request),
    // The first confirmation also opens a session; an already-confirmed link does not, so then re-check /auth/me.
    onSuccess: ({ user }) => {
      if (user) {
        markFreshLogin()
        queryClient.setQueryData(queryKeys.me, normalizeUser(user))
      } else void queryClient.invalidateQueries({ queryKey: queryKeys.me })
    },
  })
}
