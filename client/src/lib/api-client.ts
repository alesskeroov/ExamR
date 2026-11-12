
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
    super(
      problem?.detail ??
        (status === 429 ? 'Çox sayda cəhd. Bir az sonra yenidən yoxlayın.' : (problem?.title ?? `Sorğu uğursuz oldu (${status})`)),
    )
    this.name = 'ApiError'
    this.status = status
    this.problem = problem
  }
}

// Required by the API on cookie-authenticated writes (CSRF protection); cross-site forms cannot send it.
const CSRF_HEADER = 'X-ExamR-Request'

type RequestOptions = Omit<RequestInit, 'body'> & { body?: unknown }

// Render's free host sleeps when idle. Through the Pages proxy (Cloudflare IPs) Render refuses to wake it and answers
// 429 "hibernate-rate-limited"; the proxy then adds this header with the API's direct URL.
const WAKE_HEADER = 'x-examr-wake-url'
const wakeDeadline = 180_000
// Render holds a request open while it boots (about a minute); aborting and resending makes it rate-limit wake-ups.
const bootRequestTimeout = 95_000
const directWakeInterval = 60_000

function wakeUrlOf(response: Response): string | null {
  const url = response.headers.get(WAKE_HEADER)
  return url?.startsWith('https://') ? url : null
}

async function fetchWithTimeout(url: string, init: RequestInit, timeout: number): Promise<Response> {
  const controller = new AbortController()
  const timer = setTimeout(() => controller.abort(), timeout)
  try {
    return await fetch(url, { ...init, signal: controller.signal })
  } finally {
    clearTimeout(timer)
  }
}

async function isServerHealthy(response: Response | null): Promise<boolean> {
  if (!response?.ok) return false
  const data: unknown = await response.json().catch(() => null)
  return !!data && typeof data === 'object' && 'status' in data && data.status === 'Healthy'
}

async function runWake(): Promise<void> {
  const deadline = Date.now() + wakeDeadline
  let lastDirectWake = -Infinity
  while (Date.now() < deadline) {
    const response = await fetchWithTimeout('/api/health/live', {
      cache: 'no-store',
      credentials: 'omit',
      headers: { Accept: 'application/json' },
    }, bootRequestTimeout).catch(() => null)
    if (await isServerHealthy(response)) return
    const wakeUrl = response && wakeUrlOf(response)
    if (wakeUrl && Date.now() - lastDirectWake >= directWakeInterval) {
      lastDirectWake = Date.now()
      // The visitor's own connection may wake the host. The no-cors response is opaque; it settling means Render
      // answered, so probe through the proxy again right away.
      await fetchWithTimeout(wakeUrl, { mode: 'no-cors', cache: 'no-store', credentials: 'omit' }, bootRequestTimeout)
        .catch(() => undefined)
      continue
    }
    await new Promise(resolve => setTimeout(resolve, wakeUrl ? 10_000 : 3_000))
  }
}

let waking: Promise<void> | null = null

/** Makes sure the API host is up. Shared by all callers; settles once it is healthy or after about three minutes. */
export function wakeServer(): Promise<void> {
  waking ??= runWake().finally(() => {
    waking = null
  })
  return waking
}

export async function apiRequest<T>(path: string, { body, headers, ...init }: RequestOptions = {}): Promise<T> {
  const isFormData = body instanceof FormData

  const send = () => fetch(`/api${path}`, {
    ...init,
    credentials: 'same-origin',
    headers: {
      Accept: 'application/json',
      [CSRF_HEADER]: '1',
      ...(body !== undefined && !isFormData ? { 'Content-Type': 'application/json' } : {}),
      ...headers,
    },
    body: body === undefined ? undefined : isFormData ? body : JSON.stringify(body),
  })

  let response = await send()
  // A hibernation answer comes from Render's edge, so the request never reached the API and is safe to resend.
  if (wakeUrlOf(response)) {
    await wakeServer()
    response = await send()
    if (wakeUrlOf(response)) {
      throw new ApiError(503, { detail: 'Server hələ işə düşür. Bir az sonra yenidən cəhd edin.' })
    }
  }

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
    xhr.setRequestHeader(CSRF_HEADER, '1')
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

/** Download protected files with the same bearer credentials as JSON requests. */
export async function downloadFile(path: string, fileName: string): Promise<void> {
  const response = await fetch(`/api${path}`, { credentials: 'same-origin' })
  if (!response.ok) {
    const problem = await response.json().catch(() => undefined) as ProblemDetails | undefined
    throw new ApiError(response.status, problem)
  }
  const url = URL.createObjectURL(await response.blob())
  const link = document.createElement('a')
  link.href = url
  link.download = fileName
  document.body.append(link)
  link.click()
  link.remove()
  setTimeout(() => URL.revokeObjectURL(url), 1000)
}

/**
 * POST that answers with Server-Sent Events. Errors before the stream starts arrive as ProblemDetails (ApiError);
 * after that, events are passed to onEvent as they come. Aborting the signal stops the request.
 */
export async function postEventStream(path: string, body: unknown, signal: AbortSignal, onEvent: (event: string, data: unknown) => void): Promise<void> {
  const response = await fetch(`/api${path}`, {
    method: 'POST',
    credentials: 'same-origin',
    headers: { Accept: 'text/event-stream', 'Content-Type': 'application/json', [CSRF_HEADER]: '1' },
    body: JSON.stringify(body),
    signal,
  })
  if (!response.ok || !response.body) {
    const problem = await response.json().catch(() => undefined) as ProblemDetails | undefined
    throw new ApiError(response.status, problem)
  }
  const reader = response.body.pipeThrough(new TextDecoderStream()).getReader()
  let buffer = ''
  for (;;) {
    const { value, done } = await reader.read()
    if (done) break
    buffer += value
    let end
    while ((end = buffer.indexOf('\n\n')) >= 0) {
      const block = buffer.slice(0, end)
      buffer = buffer.slice(end + 2)
      let event = 'message'
      let data = ''
      for (const line of block.split('\n')) {
        if (line.startsWith('event: ')) event = line.slice(7)
        else if (line.startsWith('data: ')) data += line.slice(6)
      }
      if (data) onEvent(event, JSON.parse(data))
    }
  }
}
