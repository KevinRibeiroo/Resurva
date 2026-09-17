import { useEffect, useRef, useState } from 'react'
import { Card } from '../../../shared/ui/Card/Card'
import { Button } from '../../../shared/ui/Button/Button'
import { Alert } from '../../../shared/ui/Alert/Alert'
import { getOriginalResume } from '../../analysis/services/originalResumeSession'
import { inspectOriginalLayout, exportOriginalLayout } from '../services/layoutExportService'
import type { LayoutInspectionModel } from '../models/LayoutInspectionModel'
import styles from './LayoutExportPanel.module.css'

export function LayoutExportPanel({ optimizationId, resumeId }: { optimizationId: string; resumeId: string }) {
  const [file, setFile] = useState<File | null>(() => getOriginalResume(resumeId))
  const [inspection, setInspection] = useState<LayoutInspectionModel | null>(null)
  const [targets, setTargets] = useState<Record<string, string>>({})
  const [busy, setBusy] = useState<'inspect' | 'export' | null>(null)
  const [error, setError] = useState('')
  const [notice, setNotice] = useState('')
  const pending = useRef<AbortController | null>(null)
  useEffect(() => () => { pending.current?.abort(); pending.current = null }, [])

  function cancel() {
    pending.current?.abort()
    pending.current = null
    setBusy(null)
  }

  function selectFile(next: File | null) {
    cancel()
    setInspection(null)
    setTargets({})
    setNotice('')
    const valid = !next || next.name.toLowerCase().endsWith('.docx') && next.size > 0 && next.size <= 10 * 1024 * 1024
    setFile(valid ? next : null)
    setError(valid ? '' : 'Selecione um DOCX de até 10 MiB. PDF não preserva o layout neste protótipo.')
  }

  async function run(action: 'inspect' | 'export') {
    if (!file || pending.current) return
    const controller = new AbortController()
    pending.current = controller
    setBusy(action)
    setError('')
    setNotice('')
    try {
      if (action === 'inspect') {
        setInspection(null)
        const result = await inspectOriginalLayout(optimizationId, file, controller.signal)
        if (pending.current !== controller) return
        setInspection(result)
        setTargets(Object.fromEntries(result.changes.map(change => [change.suggestionId,
          change.kind === 'replace_text' && change.candidates.length === 1 ? change.candidates[0].id : ''])))
      } else if (inspection) {
        const blob = await exportOriginalLayout(optimizationId, file, inspection, targets, controller.signal)
        if (pending.current !== controller) return
        const url = URL.createObjectURL(blob)
        const link = document.createElement('a')
        link.href = url
        link.download = 'curriculo-adaptado-layout-original.docx'
        document.body.appendChild(link)
        link.click()
        link.remove()
        // Allow the browser to consume the download before releasing its object URL.
        window.setTimeout(() => URL.revokeObjectURL(url), 1000)
        setNotice('Download iniciado. Abra o DOCX no Word e confira quebras de linha e páginas antes de enviar.')
      }
    } catch (err) {
      if (pending.current === controller && !controller.signal.aborted)
        setError(err instanceof Error ? err.message : 'Não foi possível gerar o arquivo. Tente novamente.')
    } finally {
      if (pending.current === controller) { pending.current = null; setBusy(null) }
    }
  }

  const ready = inspection && inspection.changes.length > 0 && inspection.changes.every(change =>
    !change.blockedReason && change.candidates.some(block => block.id === targets[change.suggestionId]))

  return (
    <Card className={styles.panel} padding="lg">
      <h2 className="font-headline-sm">DOCX com layout original — teste local</h2>
      <p>Use o mesmo DOCX enviado na análise. O original fica apenas na memória desta sessão; após recarregar, selecione-o novamente.</p>
      <p id="layout-file-help">Até 10 MiB. Este protótipo aceita documentos simples de uma coluna. Não converte PDF nem garante a mesma paginação.</p>
      <label htmlFor="layout-original-file">DOCX original</label>
      <input id="layout-original-file" className={styles.field} type="file" accept=".docx"
        aria-describedby="layout-file-help" onChange={event => selectFile(event.target.files?.[0] ?? null)} />
      {file && <p className={styles.filename}>Selecionado: {file.name}</p>}
      <div className={styles.actions}>
        <Button onClick={() => run('inspect')} disabled={!file || Boolean(busy)} loading={busy === 'inspect'}>Inspecionar DOCX</Button>
        {busy && <Button variant="ghost" onClick={cancel}>Cancelar</Button>}
      </div>
      {error && <Alert>{error}</Alert>}
      {inspection && <>
        <p>Confira o destino de cada alteração aprovada. Inclusões são acrescentadas ao fim do trecho escolhido; skills entram na categoria selecionada. Nenhuma alteração será omitida silenciosamente.</p>
        {inspection.changes.map(change => (
          <div className={styles.change} key={change.suggestionId}>
            <strong>{change.proposedText}</strong>
            {change.blockedReason ? <Alert variant="warning">{change.blockedReason}</Alert> : <>
              <label htmlFor={`layout-target-${change.suggestionId}`}>Destino de {change.proposedText}</label>
              <select id={`layout-target-${change.suggestionId}`} className={styles.field} disabled={Boolean(busy)}
                value={targets[change.suggestionId] ?? ''}
                onChange={event => { setTargets(previous => ({ ...previous, [change.suggestionId]: event.target.value })); setNotice('') }}>
                <option value="">Selecione o trecho ou categoria</option>
                {change.candidates.map(block => <option key={block.id} value={block.id}>{block.section} — {block.text}</option>)}
              </select>
              {targets[change.suggestionId] && <p>{change.candidates.find(block => block.id === targets[change.suggestionId])?.text}</p>}
            </>}
          </div>
        ))}
        {!ready && <p>Resolva os destinos pendentes para baixar. Se houver bloqueios, revise a adaptação ou use a exportação em modelo padrão.</p>}
        <Button variant="primary" onClick={() => run('export')} disabled={!ready || Boolean(busy)} loading={busy === 'export'}>
          Baixar DOCX com layout original
        </Button>
      </>}
      <div role="status" aria-live="polite" className={styles.status}>{notice || (busy ? 'Processando o DOCX…' : '')}</div>
    </Card>
  )
}
