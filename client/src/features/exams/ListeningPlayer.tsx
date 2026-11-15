import { Headphones } from 'lucide-react'

/**
 * Listening recording of the current question (DİM English section). Rendered with a key per recording above the
 * question, so moving between questions of the same recording does not interrupt playback.
 */
export function ListeningPlayer({ src, title, className, hint = true }: { src: string; title: string; className?: string; hint?: boolean }) {
  return (
    <section data-no-swipe aria-label={`Dinləmə: ${title}`} className={className ?? 'rounded-xl border border-indigo-200 bg-indigo-50/60 px-3.5 py-3 sm:px-5'}>
      <p className="flex items-center gap-2 text-sm font-semibold text-indigo-900">
        <Headphones className="size-4 shrink-0" aria-hidden />
        <span className="min-w-0 truncate">{title}</span>
      </p>
      {/* Native controls: keyboard and screen-reader accessible, with seeking (the API serves byte ranges). */}
      <audio controls preload="metadata" src={src} className="mt-2 w-full">
        Brauzeriniz səsi oxuya bilmir.
      </audio>
      {hint && <p className="mt-1.5 text-xs text-indigo-900/70">Bu səs bir neçə suala aiddir; suallar arasında keçəndə dayanmır.</p>}
    </section>
  )
}
