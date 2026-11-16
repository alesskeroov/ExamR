import { useState } from 'react'
import { useMutation } from '@tanstack/react-query'
import { Download, Share2 } from 'lucide-react'
import { Alert } from '../../components/common/Alert'
import { Button } from '../../components/common/Button'
import { EmptyState } from '../../components/common/EmptyState'
import { downloadFile } from '../../lib/api-client'
import { formatDate } from '../../lib/format'
import { useSharedDocuments, type DocumentDto } from './api'
import { useTranslation } from 'react-i18next'

export function SharedDocuments({ onSelect }: { onSelect: (id: string) => void }) {
  const { t } = useTranslation()
  const [page, setPage] = useState(1)
  const documents = useSharedDocuments(page)
  const download = useMutation({ mutationFn: (document: DocumentDto) => downloadFile(`/documents/${document.id}/download`, document.originalFileName) })
  return <section className="rounded-xl border border-slate-200 bg-white p-5">
    <h2 className="mb-4 flex items-center gap-2 font-semibold"><Share2 className="size-4 text-indigo-600" aria-hidden />{t('documents.sharedWithMe')}</h2>
    {documents.isPending && <p role="status" className="text-slate-500">{t('common.loadingDots')}</p>}
    {documents.error && <Alert title={t('documents.listFailed')}>{documents.error.message}</Alert>}
    {download.error && <Alert title={t('documents.fileFailed')}>{download.error.message}</Alert>}
    {documents.data?.items.length === 0 && <EmptyState title={t('documents.sharedEmpty')} description={t('documents.sharedEmptyText')} />}
    <ul className="divide-y divide-slate-100">{documents.data?.items.filter(document => !document.canEdit).map(document => <li key={document.id} className="flex flex-wrap items-center gap-3 py-3">
      <button type="button" onClick={() => onSelect(document.id)} className="min-w-0 flex-1 text-left"><span className="block break-words font-medium text-indigo-600">{document.originalFileName}</span><span className="mt-1 block text-xs text-slate-500">{formatDate(document.createdAt)} · {t('common.questions', { count: document.questionCount })} · {document.uploadedBy.displayName}</span></button>
      <Button variant="secondary" disabled={download.isPending} onClick={() => download.mutate(document)}><Download className="size-4" aria-hidden />{t('documents.download')}</Button>
    </li>)}</ul>
    {documents.data && documents.data.totalCount > documents.data.pageSize && <div className="mt-4 flex items-center justify-between"><Button variant="secondary" disabled={page === 1} onClick={() => setPage(value => value - 1)}>{t('common.previous')}</Button><span className="text-xs text-slate-500">{page} / {Math.ceil(documents.data.totalCount / documents.data.pageSize)}</span><Button variant="secondary" disabled={page * documents.data.pageSize >= documents.data.totalCount} onClick={() => setPage(value => value + 1)}>{t('common.next')}</Button></div>}
  </section>
}
