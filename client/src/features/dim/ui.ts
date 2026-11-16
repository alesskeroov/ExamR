import type { DimTrack } from './api'
import i18n from '../../lib/i18n'

export const inputClass = 'h-10 w-full rounded-lg border border-slate-300 bg-white px-3 text-sm text-slate-900 focus:border-indigo-500 focus:ring-2 focus:ring-indigo-500/20 focus:outline-none'

/** Display name of a DİM track in the current interface language (read at render time). */
export const trackLabel = new Proxy({} as Record<DimTrack, string>, { get: (_, track: string) => i18n.t(`dim.track.${track}`) })
