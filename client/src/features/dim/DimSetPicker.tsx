import { Plus } from 'lucide-react'
import { useState } from 'react'
import { Alert } from '../../components/common/Alert'
import { Button } from '../../components/common/Button'
import { useCreateDimSet, type DimSourceDocument } from './api'
import { inputClass } from './ui'
import { useTranslation } from 'react-i18next'

/** Picks a document, or creates a new hand-made set and selects it. `onlyManual` hides uploaded PDFs. */
export function DimSetPicker({ documents, documentId, onDocument, onlyManual = false }: {
  documents: DimSourceDocument[]
  documentId: string
  onDocument: (id: string) => void
  onlyManual?: boolean
}) {
  const { t } = useTranslation()
  const createSet = useCreateDimSet()
  const [title, setTitle] = useState('')
  const shown = onlyManual ? documents.filter(d => d.isManual) : documents

  return (
    <>
      <label className="flex flex-col gap-1.5 text-sm font-medium text-slate-700">{onlyManual ? t('dim.set.set') : t('dim.set.setOrDoc')}
        <select className={inputClass} value={shown.some(d => d.id === documentId) ? documentId : ''} onChange={e => onDocument(e.target.value)}>
          <option value="">{t('dim.set.choose')}</option>
          {shown.map(d => <option key={d.id} value={d.id}>{d.isManual ? '✎ ' : ''}{d.originalFileName}</option>)}
        </select>
      </label>
      <div className="flex flex-col gap-1.5 text-sm font-medium text-slate-700">
        <label htmlFor={`dim-new-set-${onlyManual ? 'import' : 'manual'}`}>{t('dim.set.orNew')}</label>
        <div className="flex gap-2">
          <input id={`dim-new-set-${onlyManual ? 'import' : 'manual'}`} className={inputClass} value={title} maxLength={200}
            placeholder={t('dim.set.placeholder')} onChange={e => setTitle(e.target.value)} />
          <Button variant="secondary" className="h-10 shrink-0" loading={createSet.isPending} disabled={title.trim().length < 2}
            onClick={() => createSet.mutate(title.trim(), { onSuccess: set => { setTitle(''); onDocument(set.id) } })}>
            <Plus className="size-4" aria-hidden />{t('dim.set.create')}
          </Button>
        </div>
        {createSet.isError && <Alert title={t('dim.set.createFailed')}>{createSet.error.message}</Alert>}
      </div>
    </>
  )
}
