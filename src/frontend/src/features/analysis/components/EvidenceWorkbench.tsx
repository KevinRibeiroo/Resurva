import { useState } from 'react'
import type { AnalysisResultModel } from '../models/AnalysisResultModel'
import styles from './EvidenceWorkbench.module.css'

export function EvidenceWorkbench({ analysis }: { analysis: AnalysisResultModel }) {
  const items = [
    ...analysis.matchedSkills.map(item => ({ ...item, found: true, category: 'Competência' })),
    ...analysis.requirementsMet.map(item => ({ ...item, found: true, category: 'Requisito' })),
    ...analysis.missingSkills.map(item => ({ ...item, found: false, category: 'Competência' })),
    ...analysis.requirementsMissing.map(item => ({ ...item, found: false, category: 'Requisito' })),
  ]
  const [selected, setSelected] = useState(0)
  if (!items.length) return <p className={styles.empty}>Esta análise não retornou requisitos para comparar. Consulte as observações abaixo ou faça uma nova análise.</p>

  return <section className={styles.workbench} aria-label="Comparação de requisitos e evidências">
    <div className={styles.listHeader}><h2>O que a vaga pede</h2><p>Selecione um item para conferir a evidência.</p></div>
    <div className={styles.paperHeader}>Trecho de origem <span>• Currículo enviado</span></div>
    {items.map((item, index) => <div className={styles.entry} key={`${item.category}-${index}`}>
      <button type="button" className={`${styles.requirement} ${selected === index ? styles.selected : ''}`}
        aria-expanded={selected === index} aria-controls={`evidence-${index}`} onClick={() => setSelected(index)}>
        <span className={styles.status} aria-hidden="true">{item.found ? '✓' : '○'}</span>
        <span><small>{item.category}</small><strong>{item.text}</strong><span className={styles.statusLabel}>{item.found ? 'Identificado na análise' : 'Não encontrado no currículo'}</span></span>
        <span aria-hidden="true">↗</span>
      </button>
      {selected === index && <section id={`evidence-${index}`} className={styles.paper} style={{gridRow:`2 / span ${items.length}`}} aria-label={`Evidência: ${item.text}`}>
        <p className={styles.eyebrow}>{item.found ? (item.evidence?.trim() ? 'Evidência disponível' : 'Item identificado') : 'Ponto para revisar'}</p>
        <h3>{item.text}</h3>
        {item.evidence?.trim() ? <><p className={styles.caption}>{item.found ? 'Trecho retornado pela análise' : 'Contexto retornado pela análise'}</p><blockquote>{item.evidence}</blockquote></> : <p className={styles.noEvidence}>{item.found ? 'A análise identificou este item, mas não retornou um trecho de apoio.' : 'Nenhuma evidência foi encontrada para este item no currículo enviado.'}</p>}
        <p className={styles.note}>{item.found ? 'Confira o contexto no documento original antes de revisar o currículo.' : 'Isso não significa que você não tenha essa experiência. Só a inclua se puder confirmar que ela é verdadeira.'}</p>
      </section>}
    </div>)}
  </section>
}
