import { useId, type InputHTMLAttributes } from 'react'
import { cn } from '../../lib/utils'

interface FormFieldProps extends InputHTMLAttributes<HTMLInputElement> {
  label: string
  error?: string
}

export function FormField({ label, error, className, ...input }: FormFieldProps) {
  const id = useId()
  const errorId = `${id}-error`

  return (
    <div className="flex flex-col gap-1.5">
      <label htmlFor={id} className="font-medium text-slate-700">
        {label}
      </label>
      <input
        id={id}
        aria-invalid={error ? true : undefined}
        aria-describedby={error ? errorId : undefined}
        className={cn(
          'h-10 rounded-lg border bg-white px-3 text-slate-900 transition-colors duration-150 placeholder:text-slate-400',
          'focus:border-indigo-500 focus:ring-2 focus:ring-indigo-500/20 focus:outline-none',
          error ? 'border-rose-400' : 'border-slate-300',
          className,
        )}
        {...input}
      />
      {error && (
        <p id={errorId} className="text-rose-600">
          {error}
        </p>
      )}
    </div>
  )
}
