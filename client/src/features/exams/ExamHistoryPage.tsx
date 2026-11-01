import { History } from 'lucide-react'
import { EmptyState } from '../../components/common/EmptyState'
import { PageHeader } from '../../components/common/PageHeader'

export function ExamHistoryPage() {
  return (
    <>
      <PageHeader title="Tarixçə" description="Keçmiş imtahanlarınız və nəticələriniz." />
      <EmptyState icon={<History className="size-8" />} title="Hələ imtahan yoxdur" description="İmtahan tarixçəsi Phase 5-də əlavə olunacaq." />
    </>
  )
}
