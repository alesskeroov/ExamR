import type { DimTrack } from './api'

export const inputClass = 'h-10 w-full rounded-lg border border-slate-300 bg-white px-3 text-sm text-slate-900 focus:border-indigo-500 focus:ring-2 focus:ring-indigo-500/20 focus:outline-none'

export const trackLabel: Record<DimTrack, string> = { Buraxilis: 'Buraxılış', Blok: 'Blok' }

/** ISO instant → value of a <input type="datetime-local"> in the browser's time zone. */
export function toLocalInput(iso: string | null) {
  if (!iso) return ''
  const date = new Date(iso)
  return new Date(date.getTime() - date.getTimezoneOffset() * 60_000).toISOString().slice(0, 16)
}

/** <input type="datetime-local"> value → ISO instant, or null when empty. */
export const fromLocalInput = (value: string) => (value ? new Date(value).toISOString() : null)
