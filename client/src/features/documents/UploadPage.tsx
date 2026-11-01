import { AlertCircle, FileText } from 'lucide-react'
import { useState } from 'react'
import { useSearchParams } from 'react-router'
import { FileDropzone } from '../../components/common/FileDropzone'
import { PageHeader } from '../../components/common/PageHeader'
import { StatusBadge } from '../../components/common/StatusBadge'
import { ApiError } from '../../lib/api-client'
import { formatDate, formatNumber } from '../../lib/format'
import { cn } from '../../lib/utils'
import { MAX_FILE_SIZE_BYTES, useMyDocuments, useUploadDocument } from './api'
import { DocumentResult } from './DocumentResult'
import { documentStatus } from './documentStatus'

export function UploadPage() {
  const [searchParams, setSearchParams] = useSearchParams()
  const selectedId = searchParams.get('doc')
  const select = (id: string) => setSearchParams({ doc: id })

  const [progress, setProgress] = useState(0)
  const [clientError, setClientError] = useState<string | null>(null)
  const upload = useUploadDocument(setProgress)

  function handleFile(file: File) {
    setClientError(null)
    upload.reset()
    if (!file.name.toLowerCase().endsWith('.pdf')) {
      setClientError('Yalnız PDF faylları qəbul edilir.')
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
      <PageHeader title="PDF yüklə" description="PDF faylından sualları və cavab variantlarını avtomatik çıxarın." />

      <div className="grid gap-6 lg:grid-cols-[minmax(0,1fr)_300px]">
        <div className="flex min-w-0 flex-col gap-6">
          <FileDropzone
            accept="application/pdf,.pdf"
            disabled={upload.isPending}
            hint="Maksimum 20 MB · mətn əsaslı PDF"
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

          {selectedId && <DocumentResult key={selectedId} documentId={selectedId} />}
        </div>

        <RecentDocuments selectedId={selectedId} onSelect={select} />
      </div>
    </>
  )
}

function RecentDocuments({ selectedId, onSelect }: { selectedId: string | null; onSelect: (id: string) => void }) {
  const { data, isPending, isError } = useMyDocuments()

  return (
    <aside aria-labelledby="recent-heading" className="lg:sticky lg:top-8 lg:self-start">
      <h2 id="recent-heading" className="mb-3 font-semibold">
        Yüklədiklərim
      </h2>
      {isPending ? (
        <div className="h-32 animate-pulse rounded-xl bg-slate-200/60" aria-hidden />
      ) : isError ? (
        <p className="text-rose-700">Siyahı yüklənmədi.</p>
      ) : data.items.length === 0 ? (
        <p className="text-slate-500">Hələ PDF yükləməmisiniz.</p>
      ) : (
        <ul className="flex flex-col gap-1.5">
          {data.items.map((document) => {
            const status = documentStatus[document.status]
            return (
              <li key={document.id}>
                <button
                  type="button"
                  onClick={() => onSelect(document.id)}
                  aria-current={document.id === selectedId ? 'true' : undefined}
                  className={cn(
                    'flex w-full items-start gap-3 rounded-lg border px-3 py-2.5 text-left transition-colors duration-150',
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
                    </span>
                  </span>
                  {document.status !== 'Completed' && <StatusBadge tone={status.tone}>{status.label}</StatusBadge>}
                </button>
              </li>
            )
          })}
        </ul>
      )}
    </aside>
  )
}
