import { ApiError } from './api-client'

/** Splits an API error into per-field validation messages and one general message. */
export function fieldErrors(error: unknown) {
  const apiError = error instanceof ApiError ? error : undefined
  return {
    field: (name: string) => apiError?.problem?.errors?.[name]?.join(' '),
    general: error && !apiError?.problem?.errors ? (error as Error).message : undefined,
  }
}
