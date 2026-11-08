import { Info } from 'lucide-react'
import { useId, useState, type ReactNode } from 'react'

interface InfoTipProps {
  /** Accessible name of the button. */
  label: string
  children: ReactNode
}

/** Small "i" button whose text shows on hover, keyboard focus or tap. */
export function InfoTip({ label, children }: InfoTipProps) {
  const [open, setOpen] = useState(false)
  const id = useId()
  return (
    <span className="relative inline-flex" onMouseEnter={() => setOpen(true)} onMouseLeave={() => setOpen(false)}>
      <button
        type="button"
        aria-label={label}
        aria-describedby={open ? id : undefined}
        onClick={() => setOpen(true)}
        onFocus={() => setOpen(true)}
        onBlur={() => setOpen(false)}
        onKeyDown={(e) => e.key === 'Escape' && setOpen(false)}
        className="flex size-8 items-center justify-center rounded-full text-slate-400 transition-colors duration-150 hover:bg-slate-100 hover:text-indigo-600 focus-visible:ring-2 focus-visible:ring-indigo-500 focus-visible:outline-none"
      >
        <Info className="size-4" aria-hidden />
      </button>
      {open && (
        <span
          id={id}
          role="tooltip"
          className="absolute top-full right-0 z-20 mt-1 w-60 rounded-lg border border-slate-200 bg-white px-3 py-2 text-xs leading-5 text-slate-700 shadow-lg"
        >
          {children}
        </span>
      )}
    </span>
  )
}
