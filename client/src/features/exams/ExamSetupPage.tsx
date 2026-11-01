import { ListChecks } from 'lucide-react'
import { EmptyState } from '../../components/common/EmptyState'
import { PageHeader } from '../../components/common/PageHeader'

export function ExamSetupPage() {
  return (
    <>
      <PageHeader title="İmtahana başla" description="30 və ya 50 suallıq təsadüfi imtahan." />
      <EmptyState icon={<ListChecks className="size-8" />} title="Tezliklə" description="İmtahan sistemi Phase 5-də əlavə olunacaq." />
    </>
  )
}
