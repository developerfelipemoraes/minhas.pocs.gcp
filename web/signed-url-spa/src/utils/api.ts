export type CreateSignedUrlRequest = {
  objectName?: string
  ttlMinutes?: number
  contentType?: string
}

export async function createSignedUrl(apiBase: string, body: CreateSignedUrlRequest) {
  const r = await fetch(`${apiBase}/api/uploads/`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body)
  })

  if (!r.ok) throw new Error('Falha ao gerar Signed URL')
  
    return r.json() as Promise<{ uploadUrl: string, objectName: string, expiresAt: string }>
}

export async function listFiles(apiBase: string, prefix: string) {
  const r = await fetch(`${apiBase}/api/files/list?prefix=${encodeURIComponent(prefix)}`)
  if (!r.ok) throw new Error('Falha ao listar arquivos')
  return r.json() as Promise<string[]>
}

export async function getSignedDownloadUrl(apiBase: string, objectName: string) {
  const r = await fetch(`${apiBase}/api/files/signed-download?objectName=${encodeURIComponent(objectName)}`)
  if (!r.ok) throw new Error('Falha ao gerar Signed Download URL')
  return r.json() as Promise<{ downloadUrl: string, expiresAt: string }>
}
