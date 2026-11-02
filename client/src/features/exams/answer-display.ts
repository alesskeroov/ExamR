/** Removes a PDF answer marker only for display; the stored answer text stays intact. */
export function answerDisplayText(text: string) {
  return text.replace(/^[ \t]*[A-Fa-f][).][ \t]+(?=\S)/, '')
}
