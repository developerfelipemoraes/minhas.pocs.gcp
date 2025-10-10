import React, { useState, useRef } from 'react'
import { createSignedUrl, listFiles, getSignedDownloadUrl } from '../utils/api'

const DEFAULT_API = 'https://localhost:62630/'

export default function App() {
  const [apiBase, setApiBase] = useState(DEFAULT_API)
  const [prefix, setPrefix] = useState('uploads/')
  const [chunkMiB, setChunkMiB] = useState(16)
  const [objectName, setObjectName] = useState('')
  const [status, setStatus] = useState('')
  const [progress, setProgress] = useState(0)
  const [uploaded, setUploaded] = useState<string | null>(null)
  const [objects, setObjects] = useState<string[]>([])
  const fileRef = useRef<HTMLInputElement>(null)

  async function handleUpload() {
    const file = fileRef.current?.files?.[0]
    if (!file) { alert('Selecione um arquivo.'); return }
    const on = objectName?.trim() || `${prefix}${Date.now()}-${file.name}`

    setStatus('Solicitando Signed URL...')
    const signed = await createSignedUrl(apiBase, { objectName: on, ttlMinutes: 15, contentType: file.type || 'application/octet-stream' })

    setStatus('Iniciando sessão resumable...')
    const initResp = await fetch(signed.uploadUrl, { method: 'POST', headers: { 'x-goog-resumable': 'start' } })
    if (!initResp.ok) { setStatus('Falha ao iniciar sessão'); return }
    const sessionUri = initResp.headers.get('location')
    if (!sessionUri) { setStatus('Sem Location na resposta'); return }

    const chunkSize = chunkMiB * 1024 * 1024;
    const total = file.size;
    let offset = 0;
    let part = 0;

    while (offset < total) {
      const end = Math.min(offset + chunkSize, total)
      const chunk = file.slice(offset, end)
      const contentRange = `bytes ${offset}-${end - 1}/${total}`

      setStatus(`Enviando parte ${++part}...`)
      const put = await fetch(sessionUri, {
        method: 'PUT',
        headers: {
          'Content-Length': String(chunk.size),
          'Content-Range': contentRange,
          'Content-Type': file.type || 'application/octet-stream'
        },
        body: chunk
      })

      if (put.status === 308) {
        // Incomplete - continue
        const range = put.headers.get('Range')
        if (range) {
          const m = /bytes=\d+-(\d+)/.exec(range)
          offset = m ? (parseInt(m[1], 10) + 1) : end
        } else {
          offset = end
        }
      } else if (put.ok) {
        offset = end
      } else {
        setStatus(`Erro HTTP ${put.status}`)
        return
      }
      setProgress(Math.round((end / total) * 100))
    }

    setUploaded(on)
    setStatus('Upload concluído')
    await refreshList()
  }

  async function refreshList() {
    const items = await listFiles(apiBase, prefix)
    setObjects(items)
  }

  async function handleSignedDownload(name: string) {
    const res = await getSignedDownloadUrl(apiBase, name)
    window.open(res.downloadUrl, '_blank')
  }

  return (
    <div style={{fontFamily:'system-ui, Segoe UI, Roboto, sans-serif', maxWidth: 900, margin: '2rem auto', padding: '1rem'}}>
      <h1>GCS Signed URL — SPA</h1>

      <section style={{marginBottom: '1rem'}}>
        <label>API Base: </label>
        <input value={apiBase} onChange={e=>setApiBase(e.target.value)} style={{width:'60%'}} />
      </section>

      <section style={{border:'1px solid #ddd', padding:'1rem', borderRadius:8}}>
        <h3>Upload (resumable)</h3>
        <div style={{display:'grid', gap: '0.5rem'}}>
          <div>
            <label>Prefixo: </label>
            <input value={prefix} onChange={e=>setPrefix(e.target.value)} />
          </div>
          <div>
            <label>Object name (opcional): </label>
            <input placeholder="uploads/2025-.../meu-arquivo.bin" value={objectName} onChange={e=>setObjectName(e.target.value)} style={{width:'100%'}} />
          </div>
          <div>
            <label>Chunk (MiB): </label>
            <input type="number" value={chunkMiB} onChange={e=>setChunkMiB(parseInt(e.target.value||'16'))} />
          </div>
          <div>
            <input type="file" ref={fileRef} />
          </div>
          <div>
            <button onClick={handleUpload}>Enviar</button>
          </div>
          <div>
            <progress value={progress} max={100} style={{width:'100%'}}></progress>
            <div>{progress}%</div>
            <div style={{color:'#555'}}>{status}</div>
            {uploaded && <div>Objeto: <code>{uploaded}</code></div>}
          </div>
        </div>
      </section>

      <section style={{marginTop: '1rem', border:'1px solid #ddd', padding:'1rem', borderRadius:8}}>
        <h3>Arquivos</h3>
        <div>
          <button onClick={refreshList}>Recarregar lista</button>
        </div>
        <ul>
          {objects.map(name => (
            <li key={name} style={{display:'flex', alignItems:'center', gap:'1rem'}}>
              <code style={{flex:1}}>{name}</code>
              <button onClick={() => handleSignedDownload(name)}>Signed Download</button>
              <a href={`${apiBase}/api/files/proxy?objectName=${encodeURIComponent(name)}`}>Download via API</a>
            </li>
          ))}
        </ul>
      </section>
    </div>
  )
}
