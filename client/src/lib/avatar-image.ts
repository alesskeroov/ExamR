const AVATAR_SIZE = 512

/**
 * Centre-crops a picked photo to a square and scales it down, so a portrait or panorama photo is never
 * stretched into an oval wherever the avatar is shown. EXIF rotation is applied by `createImageBitmap`.
 * Falls back to the original file when the browser cannot decode or re-encode it.
 */
export async function squareAvatar(file: File): Promise<File> {
  try {
    const bitmap = await createImageBitmap(file, { imageOrientation: 'from-image' })
    const side = Math.min(bitmap.width, bitmap.height)
    const target = Math.min(side, AVATAR_SIZE)
    const canvas = document.createElement('canvas')
    canvas.width = canvas.height = target
    const context = canvas.getContext('2d')
    if (!context) { bitmap.close(); return file }
    context.imageSmoothingQuality = 'high'
    context.drawImage(bitmap, (bitmap.width - side) / 2, (bitmap.height - side) / 2, side, side, 0, 0, target, target)
    bitmap.close()
    // Browsers without WebP encoding (older Safari) return PNG instead; both are accepted by the API.
    const blob = await new Promise<Blob | null>(resolve => canvas.toBlob(resolve, 'image/webp', 0.9))
    if (!blob) return file
    const extension = blob.type === 'image/webp' ? 'webp' : 'png'
    return new File([blob], `avatar.${extension}`, { type: blob.type })
  } catch {
    return file
  }
}
