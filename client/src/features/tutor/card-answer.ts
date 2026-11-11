/** Keep the source answer intact; only its initial display is shortened at a sentence boundary. */
export function splitCardAnswer(text: string): { preview: string; rest: string } {
  const segments = new Intl.Segmenter('az', { granularity: 'sentence' }).segment(text)
  let count = 0
  for (const part of segments) {
    if (!part.segment.trim()) continue
    if (++count === 6) {
      const end = part.index + part.segment.length
      return { preview: text.slice(0, end).trimEnd(), rest: text.slice(end).trimStart() }
    }
  }
  return { preview: text, rest: '' }
}
