import { Link } from 'react-router'
import { EmptyState } from '../components/common/EmptyState'

export function NotFoundPage() {
  return (
    <EmptyState
      title="Səhifə tapılmadı"
      description="Axtardığınız səhifə mövcud deyil."
      action={
        <Link to="/" className="font-medium text-indigo-600 hover:text-indigo-700">
          Dashboard-a qayıt
        </Link>
      }
    />
  )
}
