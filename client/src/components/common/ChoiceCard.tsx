import type { ReactNode } from 'react'
import { cn } from '../../lib/utils'

interface ChoiceCardProps {
  name: string
  checked: boolean
  onChange: () => void
  icon: ReactNode
  title: string
  description: string
}

/** Radio option drawn as a card (icon, title, one-line description). */
export function ChoiceCard({ name, checked, onChange, icon, title, description }: ChoiceCardProps) {
  return (
    <label
      className={cn(
        'flex cursor-pointer items-start gap-3 rounded-lg border p-3 text-sm transition-colors duration-150 has-[:focus-visible]:ring-2 has-[:focus-visible]:ring-indigo-500',
        checked ? 'border-indigo-500 bg-indigo-50' : 'border-slate-200 hover:border-slate-300 hover:bg-slate-50',
      )}
    >
      <input type="radio" name={name} checked={checked} onChange={onChange} className="sr-only" />
      <span className={cn('mt-0.5', checked ? 'text-indigo-600' : 'text-slate-400')} aria-hidden>
        {icon}
      </span>
      <span>
        <span className="block font-medium">{title}</span>
        <span className="mt-1 block text-xs leading-5 text-slate-500">{description}</span>
      </span>
    </label>
  )
}
