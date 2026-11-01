import { FileUp } from 'lucide-react'
import { useRef, useState, type DragEvent } from 'react'
import { cn } from '../../lib/utils'

interface FileDropzoneProps {
  accept: string
  disabled?: boolean
  hint?: string
  onFile: (file: File) => void
}

/** Drag & drop area that is also a keyboard-accessible file picker button. */
export function FileDropzone({ accept, disabled, hint, onFile }: FileDropzoneProps) {
  const inputRef = useRef<HTMLInputElement>(null)
  const [dragging, setDragging] = useState(false)

  function handleDrop(event: DragEvent) {
    event.preventDefault()
    setDragging(false)
    const file = event.dataTransfer.files[0]
    if (file && !disabled) onFile(file)
  }

  return (
    <div
      onDragOver={(e) => {
        e.preventDefault()
        if (!disabled) setDragging(true)
      }}
      onDragLeave={() => setDragging(false)}
      onDrop={handleDrop}
      className={cn(
        'flex flex-col items-center justify-center rounded-xl border-2 border-dashed bg-white px-6 py-10 text-center transition-colors duration-150',
        dragging ? 'border-indigo-500 bg-indigo-50' : 'border-slate-300',
        disabled && 'opacity-60',
      )}
    >
      <div className={cn('mb-3 rounded-full p-3', dragging ? 'bg-indigo-100 text-indigo-600' : 'bg-slate-100 text-slate-500')}>
        <FileUp className="size-6" aria-hidden />
      </div>
      <p className="font-medium">
        {dragging ? 'Faylı buraxın' : 'PDF faylını bura sürüşdürün və ya'}{' '}
        {!dragging && (
          <button
            type="button"
            disabled={disabled}
            onClick={() => inputRef.current?.click()}
            className="font-semibold text-indigo-600 hover:text-indigo-700 disabled:cursor-not-allowed"
          >
            seçin
          </button>
        )}
      </p>
      {hint && <p className="mt-1 text-slate-500">{hint}</p>}
      <input
        ref={inputRef}
        type="file"
        accept={accept}
        className="sr-only"
        tabIndex={-1}
        onChange={(e) => {
          const file = e.target.files?.[0]
          if (file) onFile(file)
          e.target.value = ''
        }}
      />
    </div>
  )
}
