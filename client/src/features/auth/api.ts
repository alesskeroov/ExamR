import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api, ApiError } from '../../lib/api-client'
import { clearToken, getToken, saveToken } from '../../lib/auth-token'
import { queryKeys } from '../../lib/query-keys'

export interface User {
  id: string
  email: string
  displayName: string
  roles: string[]
}

interface AuthResponse {
  accessToken: string
  expiresAt: string
  user: User
}

export interface LoginRequest {
  email: string
  password: string
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
      if (!getToken()) return null
      try {
        return await api.get<User>('/auth/me')
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
    onSuccess: ({ accessToken, expiresAt, user }) => {
      saveToken({ accessToken, expiresAt })
      queryClient.setQueryData(queryKeys.me, user)
    },
  })
}

export const useLogin = () => useSignIn('/auth/login')
export const useRegister = () => useSignIn('/auth/register')

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
