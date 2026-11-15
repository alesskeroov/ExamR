import { Plus } from 'lucide-react'
import { useState } from 'react'
import { Alert } from '../../components/common/Alert'
import { Button } from '../../components/common/Button'
import { useCreateDimSet, type DimSourceDocument } from './api'
import { inputClass } from './ui'

/** Picks a document, or creates a new hand-made set and selects it. `onlyManual` hides uploaded PDFs. */
export function DimSetPicker({ documents, documentId, onDocument, onlyManual = false }: {
  documents: DimSourceDocument[]
  documentId: string
  onDocument: (id: string) => void
  onlyManual?: boolean
}) {
  const createSet = useCreateDimSet()
  const [title, setTitle] = useState('')
  const shown = onlyManual ? documents.filter(d => d.isManual) : documents

  return (
    <>
      <label className="flex flex-col gap-1.5 text-sm font-medium text-slate-700">{onlyManual ? 'Dəst' : 'Dəst / sənəd'}
        <select className={inputClass} value={shown.some(d => d.id === documentId) ? documentId : ''} onChange={e => onDocument(e.target.value)}>
          <option value="">Seçin</option>
          {shown.map(d => <option key={d.id} value={d.id}>{d.isManual ? '✎ ' : ''}{d.originalFileName}</option>)}
        </select>
      </label>
      <div className="flex flex-col gap-1.5 text-sm font-medium text-slate-700">
        <label htmlFor={`dim-new-set-${onlyManual ? 'import' : 'manual'}`}>və ya yeni dəst</label>
        <div className="flex gap-2">
          <input id={`dim-new-set-${onlyManual ? 'import' : 'manual'}`} className={inputClass} value={title} maxLength={200}
            placeholder="məs. İngilis dili — sınaq 1" onChange={e => setTitle(e.target.value)} />
          <Button variant="secondary" className="h-10 shrink-0" loading={createSet.isPending} disabled={title.trim().length < 2}
            onClick={() => createSet.mutate(title.trim(), { onSuccess: set => { setTitle(''); onDocument(set.id) } })}>
            <Plus className="size-4" aria-hidden />Yarat
          </Button>
        </div>
        {createSet.isError && <Alert title="Dəst yaradılmadı">{createSet.error.message}</Alert>}
      </div>
    </>
  )
}
