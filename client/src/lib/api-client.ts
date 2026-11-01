// Thin fetch wrapper. All requests are same-origin (/api via Vite proxy in dev, served by the API in prod).

export interface ProblemDetails {
  type?: string
  title?: string
  status?: number
  detail?: string
  errors?: Record<string, string[]>
  [extension: string]: unknown
}

export class ApiError extends Error {
  readonly status: number
  readonly problem?: ProblemDetails

  constructor(status: number, problem?: ProblemDetails) {
    super(problem?.detail ?? problem?.title ?? `Sorğu uğursuz oldu (${status})`)
    this.name = 'ApiError'
    this.status = status
    this.problem = problem
  }
}

type RequestOptions = Omit<RequestInit, 'body'> & { body?: unknown }

export async function apiRequest<T>(path: string, { body, headers, ...init }: RequestOptions = {}): Promise<T> {
  const isFormData = body instanceof FormData

  const response = await fetch(`/api${path}`, {
    credentials: 'same-origin',
    ...init,
    headers: {
      Accept: 'application/json',
      ...(body !== undefined && !isFormData ? { 'Content-Type': 'application/json' } : {}),
      ...headers,
    },
    body: body === undefined ? undefined : isFormData ? body : JSON.stringify(body),
  })

  const text = await response.text()
  const data: unknown = text ? JSON.parse(text) : undefined

  if (!response.ok) {
    throw new ApiError(response.status, data as ProblemDetails | undefined)
  }

  return data as T
}

/** Multipart upload with progress (fetch cannot report upload progress). */
export function uploadFile<T>(path: string, file: File, onProgress?: (percent: number) => void): Promise<T> {
  return new Promise((resolve, reject) => {
    const xhr = new XMLHttpRequest()
    xhr.open('POST', `/api${path}`)
    xhr.setRequestHeader('Accept', 'application/json')
    xhr.upload.onprogress = (event) => {
      if (event.lengthComputable) onProgress?.(Math.round((event.loaded / event.total) * 100))
    }
    xhr.onload = () => {
      const data: unknown = xhr.responseText ? JSON.parse(xhr.responseText) : undefined
      if (xhr.status >= 200 && xhr.status < 300) resolve(data as T)
      else reject(new ApiError(xhr.status, data as ProblemDetails | undefined))
    }
    xhr.onerror = () => reject(new Error('Şəbəkə xətası: serverə qoşulmaq mümkün olmadı.'))

    const form = new FormData()
    form.append('file', file)
    xhr.send(form)
  })
}

export const api = {
  get: <T>(path: string, init?: RequestOptions) => apiRequest<T>(path, { ...init, method: 'GET' }),
  post: <T>(path: string, body?: unknown, init?: RequestOptions) => apiRequest<T>(path, { ...init, method: 'POST', body }),
  put: <T>(path: string, body?: unknown, init?: RequestOptions) => apiRequest<T>(path, { ...init, method: 'PUT', body }),
  delete: <T = void>(path: string, init?: RequestOptions) => apiRequest<T>(path, { ...init, method: 'DELETE' }),
}
