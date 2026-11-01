import { useState } from 'react'
import { useMutation } from '@tanstack/react-query'
import { Download, Share2 } from 'lucide-react'
import { Alert } from '../../components/common/Alert'
import { Button } from '../../components/common/Button'
import { EmptyState } from '../../components/common/EmptyState'
import { downloadFile } from '../../lib/api-client'
import { formatDate } from '../../lib/format'
import { useSharedDocuments, type DocumentDto } from './api'

export function SharedDocuments({ onSelect }: { onSelect: (id: string) => void }) {
  const [page, setPage] = useState(1)
  const documents = useSharedDocuments(page)
  const download = useMutation({ mutationFn: (document: DocumentDto) => downloadFile(`/documents/${document.id}/download`, document.originalFileName) })
  return <section className="rounded-xl border border-slate-200 bg-white p-5">
    <h2 className="mb-4 flex items-center gap-2 font-semibold"><Share2 className="size-4 text-indigo-600" aria-hidden />Mənimlə paylaşılanlar</h2>
    {documents.isPending && <p role="status" className="text-slate-500">Yüklənir…</p>}
    {documents.error && <Alert title="Sənədlər yüklənmədi">{documents.error.message}</Alert>}
    {download.error && <Alert title="Fayl yüklənmədi">{download.error.message}</Alert>}
    {documents.data?.items.length === 0 && <EmptyState title="Hələ paylaşılan sənəd yoxdur" description="Adminin sizinlə və ya qrupunuzla paylaşdığı sənədlər burada görünəcək." />}
    <ul className="divide-y divide-slate-100">{documents.data?.items.filter(document => !document.canEdit).map(document => <li key={document.id} className="flex flex-wrap items-center gap-3 py-3">
      <button type="button" onClick={() => onSelect(document.id)} className="min-w-0 flex-1 text-left"><span className="block break-words font-medium text-indigo-600">{document.originalFileName}</span><span className="mt-1 block text-xs text-slate-500">{formatDate(document.createdAt)} · {document.questionCount} sual · {document.uploadedBy.displayName}</span></button>
      <Button variant="secondary" disabled={download.isPending} onClick={() => download.mutate(document)}><Download className="size-4" aria-hidden />Yüklə</Button>
    </li>)}</ul>
    {documents.data && documents.data.totalCount > documents.data.pageSize && <div className="mt-4 flex items-center justify-between"><Button variant="secondary" disabled={page === 1} onClick={() => setPage(value => value - 1)}>Əvvəlki</Button><span className="text-xs text-slate-500">{page} / {Math.ceil(documents.data.totalCount / documents.data.pageSize)}</span><Button variant="secondary" disabled={page * documents.data.pageSize >= documents.data.totalCount} onClick={() => setPage(value => value + 1)}>Növbəti</Button></div>}
  </section>
}
