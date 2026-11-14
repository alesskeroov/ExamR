import { FileSearch, Search } from 'lucide-react'
import { useId, useState, type FormEvent } from 'react'
import { Alert } from '../../components/common/Alert'
import { EmptyState } from '../../components/common/EmptyState'
import { useDocumentSearch, type CardDeck, type SearchHit } from './api'
import { placeLabel } from './place-label'
import { SelectionToCard } from './SelectionToCard'
import { Frame, Skeleton } from './TutorShared'

interface Props {
  chatId: string
  deck: CardDeck
  documentId: string
  format: string
  onOpen: (hit: SearchHit) => void
}

const maxQuery = 500

/** "Axtar": the student types a question or a few words and sees the matching passages of the material, without AI. */
export function GuideSearch({ chatId, deck, documentId, format, onOpen }: Props) {
  const [term, setTerm] = useState('')
  const [query, setQuery] = useState('')
  const search = useDocumentSearch(documentId, query)
  const inputId = useId()

  const submit = (event: FormEvent) => {
    event.preventDefault()
    setQuery(term.trim().slice(0, maxQuery))
  }

  return (
    <Frame>
      <form onSubmit={submit} role="search" className="flex flex-col gap-2">
        <label htmlFor={inputId} className="text-sm font-semibold text-slate-700">Materialda axtar</label>
        <div className="flex gap-2">
          <div className="tutor-border flex h-11 min-w-0 flex-1 items-center gap-2 rounded-xl border bg-white px-3">
            <Search className="size-4 shrink-0 text-slate-400" aria-hidden />
            <input id={inputId} type="search" value={term} onChange={e => setTerm(e.target.value)} maxLength={maxQuery}
              placeholder="Məsələn: capex nədir?" className="min-w-0 flex-1 bg-transparent text-base text-slate-900 outline-none placeholder:text-slate-400 sm:text-sm" />
          </div>
          <button type="submit" disabled={term.trim().length === 0} className="btn-primary h-11 shrink-0 rounded-xl px-4 text-sm font-semibold disabled:opacity-60">Axtar</button>
        </div>
        <p className="text-xs text-slate-500">Sözlərə görə ən uyğun hissələr göstərilir. Vacib hissəni seçib öz kartınızı yarada bilərsiniz.</p>
      </form>

      {search.isFetching && <Skeleton label="Axtarılır…" />}
      {search.isError && <Alert title="Axtarış alınmadı">{search.error.message}</Alert>}
      {search.data && !search.isFetching && (search.data.chunks.length === 0 ? (
        <EmptyState icon={<FileSearch className="size-8" aria-hidden />} title="Uyğun hissə tapılmadı"
          description="Başqa sözlərlə və ya materialdakı terminlərlə yenidən yoxlayın." />
      ) : (
        <SelectionToCard chatId={chatId} deck={deck}>
          <ol className="flex flex-col gap-3" aria-label="Axtarış nəticələri">
            {search.data.chunks.map((hit, i) => {
              const place = placeLabel(format, hit.pageFrom, hit.pageTo)
              return (
                <li key={hit.ordinal} className="tutor-border rounded-2xl border bg-white p-4">
                  <div className="flex flex-wrap items-center gap-x-2 gap-y-1 text-xs text-slate-500">
                    <span className="font-semibold text-slate-700">{i + 1}.</span>
                    {hit.section && <span className="font-medium text-slate-700">{hit.section}</span>}
                    {place && <span>{place}</span>}
                    <button type="button" onClick={() => onOpen(hit)} className="ml-auto font-semibold text-indigo-700 underline-offset-2 hover:underline">Mənbəni aç</button>
                  </div>
                  <p className="mt-2 text-[15px] leading-relaxed whitespace-pre-line break-words text-slate-800 sm:text-sm">{hit.text}</p>
                </li>
              )
            })}
          </ol>
        </SelectionToCard>
      ))}
    </Frame>
  )
}
