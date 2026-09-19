import type { AnalysisResultModel, EvidenceItemModel } from '../models/AnalysisResultModel'
import { Link } from 'react-router-dom'
import { Button } from '../../../shared/ui/Button/Button'
import { Alert } from '../../../shared/ui/Alert/Alert'
import { EvidenceWorkbench } from './EvidenceWorkbench'
import styles from './AnalysisResultView.module.css'

interface Props { analysis: AnalysisResultModel; optimizing: boolean; optError: string | null; onOptimize: () => void }

export function AnalysisResultView({ analysis, optimizing, optError, onOptimize }: Props) {
  const dimensions = [
    ['Skills & Competências', analysis.skillsScore], ['Experiência Prática', analysis.experienceScore],
    ['Senioridade Requerida', analysis.seniorityScore], ['Requisitos Formais', analysis.requirementsScore],
    ['Formação e Educação', analysis.educationScore],
  ] as const
  const notes: [string, EvidenceItemModel[]][] = [['Pontos fortes', analysis.strengths], ['Pontos de atenção', analysis.pointsOfAttention], ['Sugestões para revisar', analysis.recommendations]]

  return <div className={`app-container ${styles.page}`}>
    <header className={styles.heading}>
      <div><p className={styles.eyebrow}>Seu currículo, em contexto <span>• Análise {analysis.id.slice(0, 8)}</span></p>
        <h1>Seu currículo e a vaga</h1>
        <p className={styles.intro}>O que o seu currículo já demonstra — e o que merece uma segunda leitura.</p>
      </div>
      <div className={styles.score}><p>Índice Geral de Compatibilidade</p><div><strong>{Math.round(analysis.overallScore)}</strong><span>/100</span></div><small>Aderência ao texto da vaga.<br />Não é uma probabilidade de contratação.</small></div>
    </header>
    <nav className={styles.sectionNav} aria-label="Seções da análise"><a href="#comparacao">Comparação</a><a href="#detalhes">Pontuação e observações</a><Link to="/app/analises/nova">Nova análise <span aria-hidden="true">↗</span></Link></nav>
    <div id="comparacao" className={styles.anchor}><EvidenceWorkbench key={analysis.id} analysis={analysis} /></div>
    <div className={styles.actions}><div><h2>Uma revisão com fundamento.</h2><p>Veja as sugestões e confirme cada informação antes de aplicar.</p></div><Button variant="primary" onClick={onOptimize} loading={optimizing} icon="edit_document">Adaptar currículo para esta vaga</Button></div>
    {optError && <Alert variant="error">{optError}</Alert>}
    <section id="detalhes" className={styles.details} aria-label="Pontuação e observações">
      <h2>Por trás da pontuação</h2><p className={styles.explanation}>O resultado combina cinco dimensões. Use os trechos e as observações para entender as diferenças entre o currículo e a vaga.</p>
      <div className={styles.dimensions}>{dimensions.map(([label, value]) => <div key={label}><p>{label}</p><strong>{Math.round(value)}</strong><span> /100</span><meter min="0" max="100" value={value} aria-label={label} /></div>)}</div>
      <div className={styles.notes}>{notes.map(([title, items]) => <section key={title}><h3>{title}</h3>{items.length ? <ul>{items.map((item, i) => <li key={i}><p>{item.text}</p>{item.evidence && <blockquote>{item.evidence}</blockquote>}</li>)}</ul> : <p className={styles.explanation}>Nenhum item retornado nesta análise.</p>}</section>)}</div>
    </section>
  </div>
}
