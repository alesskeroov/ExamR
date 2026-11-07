import { useState } from 'react'
import { cn } from '../../lib/utils'
import { DimBankAdmin } from './DimBankAdmin'
import { DimTestsAdmin } from './DimTestsAdmin'

/** Admin panel section for DİM tests: question bank and test builder. */
export function DimAdminSection() {
  const [tab, setTab] = useState<'bank' | 'tests'>('bank')
  return (
    <div>
      <div role="tablist" aria-label="DİM idarəetməsi" className="mb-5 inline-flex rounded-lg border border-slate-200 bg-white p-1">
        {([['bank', 'Sual bankı'], ['tests', 'Sınaqlar']] as const).map(([value, label]) => (
          <button key={value} type="button" role="tab" aria-selected={tab === value} onClick={() => setTab(value)}
            className={cn('rounded-md px-4 py-1.5 text-sm font-medium', tab === value ? 'bg-indigo-600 text-white' : 'text-slate-600 hover:bg-slate-100')}>
            {label}
          </button>
        ))}
      </div>
      {tab === 'bank' ? <DimBankAdmin /> : <DimTestsAdmin />}
    </div>
  )
}
