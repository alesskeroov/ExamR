import { Library } from 'lucide-react'
import { EmptyState } from '../../components/common/EmptyState'
import { PageHeader } from '../../components/common/PageHeader'

export function QuestionBankPage() {
  return (
    <>
      <PageHeader title="Sual bankı" description="Çıxarılmış sualları yoxlayın və redaktə edin." />
      <EmptyState icon={<Library className="size-8" />} title="Tezliklə" description="Sual bankı Phase 4-də əlavə olunacaq." />
    </>
  )
}
