import { UsageMeter } from '../../components/common/UsageMeter'
import { useUsageLimit } from '../usage/api'
import { AlertCircle, FileText, Trash2 } from 'lucide-react'
import { useState } from 'react'
import { useSearchParams } from 'react-router'
import { FileDropzone } from '../../components/common/FileDropzone'
import { EmptyState } from '../../components/common/EmptyState'
import { PageHeader } from '../../components/common/PageHeader'
import { Alert } from '../../components/common/Alert'
import { StatusBadge } from '../../components/common/StatusBadge'
import { ApiError } from '../../lib/api-client'
import { formatDate, formatNumber } from '../../lib/format'
import { useConfirm } from '../../lib/use-confirm'
import { cn } from '../../lib/utils'
import { MAX_FILE_SIZE_BYTES, useDeleteDocument, useMyDocuments, useUploadDocument } from './api'
import { DocumentResult } from './DocumentResult'
import { documentStatus } from './documentStatus'
import { Button } from '../../components/common/Button'
import { DocumentSharingModal } from './DocumentSharingModal'
import { SharedDocuments } from './SharedDocuments'

export function UploadPage({ admin = false }: { admin?: boolean }) {
  const monthly = useUsageLimit('Upload')
  const [searchParams, setSearchParams] = useSearchParams()
  const selectedId = searchParams.get('doc')
  const select = (id: string) => setSearchParams({ doc: id })

  const [progress, setProgress] = useState(0)
  const [clientError, setClientError] = useState<string | null>(null)
  const upload = useUploadDocument(setProgress)

  function handleFile(file: File) {
    if (monthly.blocked) return
    setClientError(null)
    upload.reset()
    if (!/\.(pdf|docx)$/i.test(file.name)) {
      setClientError('Yalnız PDF və DOCX faylları qəbul edilir.')
      return
    }
    if (file.size > MAX_FILE_SIZE_BYTES) {
      setClientError('Fayl 20 MB-dan böyük ola bilməz.')
      return
    }

    setProgress(0)
    upload.mutate(file, { onSuccess: (document) => select(document.id) })
  }

  const uploadError = upload.error instanceof ApiError ? upload.error : null
  const existingDocumentId = uploadError?.status === 409 ? (uploadError.problem?.documentId as string | undefined) : undefined
  const errorMessage = clientError ?? upload.error?.message

  return (
    <>
      <PageHeader title={admin ? "Sənədlərin idarə edilməsi" : "Sənəd yüklə"} description="PDF və Word (.docx) fayllarından sualları və cavabları avtomatik çıxarın." />

      <div className="flex flex-col gap-6">
        <div id="document-upload" className="flex min-w-0 flex-col gap-6">
          {!admin && <UsageMeter kind="Upload" />}
          <FileDropzone
            accept="application/pdf,.pdf,application/vnd.openxmlformats-officedocument.wordprocessingml.document,.docx"
            disabled={upload.isPending || monthly.blocked}
            hint="Maksimum 20 MB · mətn əsaslı PDF və DOCX"
            onFile={handleFile}
          />

          {upload.isPending && (
            <div role="status" className="rounded-xl border border-slate-200 bg-white p-4 shadow-sm">
              <div className="mb-2 flex justify-between font-medium">
                <span>Yüklənir…</span>
                <span className="tabular-nums">{progress}%</span>
              </div>
              <div className="h-2 overflow-hidden rounded-full bg-slate-100">
                <div className="h-full rounded-full bg-indigo-600 transition-[width] duration-150" style={{ width: `${progress}%` }} />
              </div>
            </div>
          )}

          {errorMessage && (
            <div role="alert" className="flex gap-3 rounded-xl border border-rose-200 bg-rose-50 px-4 py-3 text-rose-800">
              <AlertCircle className="mt-0.5 size-5 shrink-0" aria-hidden />
              <div>
                <p className="font-medium">{errorMessage}</p>
                {existingDocumentId && (
                  <button
                    type="button"
                    onClick={() => {
                      upload.reset()
                      select(existingDocumentId)
                    }}
                    className="mt-1 font-semibold text-rose-900 underline underline-offset-2 hover:no-underline"
                  >
                    Mövcud nəticəni göstər
                  </button>
                )}
              </div>
            </div>
          )}
        </div>

        <RecentDocuments admin={admin} key={upload.data?.id ?? 'documents'} selectedId={selectedId} onSelect={select} onDeleted={(id) => { if (selectedId === id) setSearchParams({}) }} />
        {!admin && <SharedDocuments onSelect={select} />}
        {selectedId && <DocumentResult key={selectedId} documentId={selectedId} />}
      </div>
    </>
  )
}

function RecentDocuments({ admin, selectedId, onSelect, onDeleted }: { admin: boolean; selectedId: string | null; onSelect: (id: string) => void; onDeleted: (id: string) => void }) {
  const [sharing, setSharing] = useState<{ id: string; name: string } | null>(null)
  const [page, setPage] = useState(1)
  const remove = useDeleteDocument()
  const confirm = useConfirm()
  const { data, isPending, error, refetch } = useMyDocuments(page)

  return (
    <section aria-labelledby="recent-heading" className="rounded-xl border border-slate-200 bg-white p-4 shadow-sm sm:p-5">
      <div className="mb-4 flex flex-wrap items-center justify-between gap-3">
        <h2 id="recent-heading" className="font-semibold">Mənim sənədlərim{data ? ` (${formatNumber(data.totalCount)})` : ''}</h2>
        <button type="button" onClick={() => {
          // The dropzone owns the file input; scroll to it and open the picker directly.
          const zone = document.getElementById('document-upload')
          zone?.scrollIntoView({ behavior: 'smooth', block: 'start' })
          zone?.querySelector<HTMLInputElement>('input[type="file"]:not(:disabled)')?.click()
        }} className="rounded-lg btn-primary px-4 py-2 text-sm font-medium transition-colors">Yeni sənəd yüklə</button>
      </div>
      {remove.error && <div className="mb-3"><Alert title={remove.error.message} /></div>}
      {isPending ? (
        <div className="h-32 animate-pulse rounded-xl bg-slate-200/60" aria-hidden />
      ) : error ? (
        <Alert title={error.message} action={<button type="button" onClick={() => void refetch()}>Yenidən cəhd et</button>} />
      ) : data.items.length === 0 ? (
        <EmptyState title="Hələ sənəd yükləməmisiniz" description="Yuxarıdakı yükləmə sahəsindən PDF və ya DOCX seçin." />
      ) : (
        <ul className="flex flex-col gap-1.5">
          {data.items.map((document) => {
            const status = documentStatus[document.status]
            return (
              <li key={document.id} className="flex flex-wrap items-center gap-2">
                <button
                  type="button"
                  onClick={() => onSelect(document.id)}
                  aria-current={document.id === selectedId ? 'true' : undefined}
                  className={cn(
                    'flex min-w-0 flex-1 items-start gap-3 rounded-lg border px-3 py-2.5 text-left transition-colors duration-150',
                    document.id === selectedId
                      ? 'border-indigo-200 bg-indigo-50'
                      : 'border-slate-200 bg-white hover:border-slate-300 hover:bg-slate-50',
                  )}
                >
                  <FileText className="mt-0.5 size-4 shrink-0 text-slate-400" aria-hidden />
                  <span className="min-w-0 flex-1">
                    <span className="block truncate font-medium">{document.originalFileName}</span>
                    <span className="block text-xs text-slate-500">
                      {formatDate(document.createdAt)}
                      {document.status === 'Completed' && ` · ${formatNumber(document.questionCount)} sual`}
                      {admin && ` · ${{ Private: 'Yalnız mən', Users: 'İstifadəçilər', Groups: 'Qruplar', UsersAndGroups: 'İstifadəçilər və qruplar', Everyone: 'Hamı' }[document.sharingStatus] ?? 'Server yenilənməlidir'}`}
                    </span>
                  </span>
                  {document.status !== 'Completed' && <StatusBadge tone={status.tone}>{status.label}</StatusBadge>}
                </button>
                {admin && document.canEdit && <Button variant="secondary" className="h-9 px-3 text-xs" disabled={!document.sharingStatus} onClick={() => setSharing({ id: document.id, name: document.originalFileName })}>Paylaşım</Button>}
                {document.canEdit && <button
                  type="button"
                  disabled={remove.isPending || document.status === 'Processing'}
                  aria-label={`${document.originalFileName} sənədini sil`}
                  title="Sənədi sil"
                  onClick={async () => {
                    if (!await confirm({ title: 'Sənəd silinsin?', description: `“${document.originalFileName}” və ona bağlı suallar və məşq nəticələri silinəcək. Bu əməliyyatı geri qaytarmaq mümkün deyil.`, confirmLabel: 'Sənədi sil', danger: true })) return
                    remove.mutate(document.id, { onSuccess: () => {
                      onDeleted(document.id)
                      if (data.items.length === 1 && page > 1) setPage(p => p - 1)
                    } })
                  }}
                  className="inline-flex shrink-0 items-center gap-2 rounded-lg p-3 text-rose-600 hover:bg-rose-50 disabled:opacity-40"
                ><Trash2 className="size-4" aria-hidden />Sil</button>}
              </li>
            )
          })}
        </ul>
      )}
      {data && data.totalCount > data.pageSize && (
        <nav aria-label="Yüklənmiş sənədlərin səhifələri" className="mt-4 flex items-center justify-between gap-2 text-sm">
          <button type="button" disabled={page === 1 || isPending} onClick={() => setPage(p => p - 1)} className="rounded-lg border border-slate-200 px-3 py-2 disabled:opacity-40">Əvvəlki</button>
          <span>{page} / {Math.ceil(data.totalCount / data.pageSize)}</span>
          <button type="button" disabled={page * data.pageSize >= data.totalCount || isPending} onClick={() => setPage(p => p + 1)} className="rounded-lg border border-slate-200 px-3 py-2 disabled:opacity-40">Növbəti</button>
        </nav>
      )}
      {sharing && <DocumentSharingModal documentId={sharing.id} name={sharing.name} onClose={() => setSharing(null)} />}
    </section>
  )
}
