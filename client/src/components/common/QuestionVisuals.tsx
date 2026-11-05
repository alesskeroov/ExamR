import { useState } from 'react'
import { ImageOff } from 'lucide-react'
import { cn } from '../../lib/utils'
import type { VisualDto } from '../../lib/visuals'

// PDF points → CSS pixels at 100 % zoom.
const POINTS_TO_PX = 96 / 72

interface QuestionVisualsProps {
  visuals: VisualDto[]
  /** Builds the file URL of a visual (see visualUrls). */
  src: (id: string) => string
  /** "option": smaller, for images inside an answer option. */
  size?: 'question' | 'option'
  className?: string
}

/** Images of a question or option, in reading order. Renders nothing when there are none. */
export function QuestionVisuals({ visuals, src, size = 'question', className }: QuestionVisualsProps) {
  if (visuals.length === 0) return null

  return (
    // Spans, not divs: option images are rendered inside <button> and <span> elements.
    <span className={cn('flex min-w-0 flex-wrap items-start gap-2', className)}>
      {visuals.map((visual, index) => (
        <VisualImage key={visual.id} visual={visual} src={src(visual.id)} size={size}
          alt={visuals.length > 1 ? `Şəkil ${index + 1}` : 'Şəkil'} />
      ))}
    </span>
  )
}

function VisualImage({ visual, src, size, alt }: { visual: VisualDto; src: string; size: 'question' | 'option'; alt: string }) {
  const [failed, setFailed] = useState(false)

  if (!visual.hasFile || failed) {
    return (
      <span className="flex items-center gap-2 rounded-lg border border-dashed border-slate-300 bg-slate-50 px-3 py-2 text-xs text-slate-600">
        <ImageOff className="size-4 shrink-0" aria-hidden />
        {visual.hasFile ? 'Şəkil yüklənmədi.' : `Şəkil göstərilə bilmir — PDF-in ${visual.pageNumber}-ci səhifəsinə baxın.`}
      </span>
    )
  }

  return (
    <img
      src={src}
      alt={alt}
      loading="lazy"
      decoding="async"
      onError={() => setFailed(true)}
      // White backing keeps transparent formula images readable in dark mode.
      className={cn('h-auto max-w-full rounded-lg border border-slate-200 bg-white object-contain', size === 'option' ? 'max-h-40' : 'max-h-96')}
      style={{ width: Math.round(visual.width * POINTS_TO_PX), aspectRatio: `${visual.width} / ${visual.height}` }}
    />
  )
}
