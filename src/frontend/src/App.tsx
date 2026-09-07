import { FormEvent, useState } from 'react'
import { compareResume, uploadResume, createOptimizationPlan } from './api'
import type { AnalysisResult, EvidenceItem, OptimizationPlan } from './types'
import { OptimizationView } from './OptimizationView'

function Score({ label, value }: { label: string; value: number }) {
  return <div className="score"><span>{label}</span><strong>{Math.round(value)}</strong><progress max="100" value={value} /></div>
}

function Items({ title, items }: { title: string; items: EvidenceItem[] }) {
  return <section><h3>{title}</h3>{items.length === 0 ? <p className="muted">Nenhum item identificado.</p> :
    <ul>{items.map((item, index) => <li key={`${item.text}-${index}`}><span>{item.text}</span>{item.evidence && <small>Evidência: “{item.evidence}”</small>}</li>)}</ul>}</section>
}

export function App() {
  const [file, setFile] = useState<File>()
  const [description, setDescription] = useState('')
  const [result, setResult] = useState<AnalysisResult>()
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')

  const [optimizationPlan, setOptimizationPlan] = useState<OptimizationPlan | null>(null)
  const [optBusy, setOptBusy] = useState(false)
  const [optError, setOptError] = useState('')

  async function submit(event: FormEvent) {
    event.preventDefault()
    if (!file) return setError('Selecione um currículo em PDF ou DOCX.')
    setBusy(true); setError(''); setResult(undefined); setOptimizationPlan(null); setOptError('')
    try {
      const resumeId = await uploadResume(file)
      setResult(await compareResume(resumeId, description))
    } catch (caught) { setError(caught instanceof Error ? caught.message : 'Erro inesperado.') }
    finally { setBusy(false) }
  }

  async function startOptimization() {
    if (!result) return
    setOptBusy(true)
    setOptError('')
    try {
      const plan = await createOptimizationPlan(result.id)
      setOptimizationPlan(plan)
    } catch (caught) {
      setOptError(caught instanceof Error ? caught.message : 'Não foi possível gerar as sugestões de adaptação.')
    } finally {
      setOptBusy(false)
    }
  }

  return <main>
    <header><p className="eyebrow">MVP seguro</p><h1>ResumeMatcher</h1><p>Compare seu currículo com uma vaga sem inventar experiências ou competências.</p></header>
    <form onSubmit={submit}>
      <label>Currículo (PDF ou DOCX)<input type="file" accept=".pdf,.docx" onChange={e => setFile(e.target.files?.[0])} /></label>
      <label>Descrição da vaga<textarea rows={10} required value={description} onChange={e => setDescription(e.target.value)} placeholder="Cole aqui os requisitos e responsabilidades da vaga." /></label>
      <button disabled={busy}>{busy ? 'Analisando…' : 'Comparar currículo com vaga'}</button>
      {error && <p role="alert" className="error">{error}</p>}
    </form>
    {result && <div className="results">
      <div className="overall"><span>Aderência geral</span><strong>{Math.round(result.overallScore)}</strong><small>/ 100</small></div>
      <div className="breakdown"><Score label="Skills" value={result.skillsScore} /><Score label="Experiência" value={result.experienceScore} /><Score label="Senioridade" value={result.seniorityScore} /><Score label="Requisitos" value={result.requirementsScore} /><Score label="Formação" value={result.educationScore} /></div>
      <div className="grid"><Items title="Skills encontradas" items={result.matchedSkills} /><Items title="Skills ausentes" items={result.missingSkills} /><Items title="Requisitos atendidos" items={result.requirementsMet} /><Items title="Requisitos não atendidos" items={result.requirementsMissing} /><Items title="Pontos fortes" items={result.strengths} /><Items title="Pontos de atenção" items={result.pointsOfAttention} /><Items title="Recomendações" items={result.recommendations} /></div>

      {!optimizationPlan ? (
        <div className="optimization-cta">
          <button
            type="button"
            className="btn-optimize"
            disabled={optBusy}
            onClick={startOptimization}
          >
            {optBusy ? 'Gerando sugestões de adaptação…' : 'Adaptar currículo para esta vaga'}
          </button>
          {optError && <p role="alert" className="error">{optError}</p>}
        </div>
      ) : (
        <OptimizationView plan={optimizationPlan} onBack={() => setOptimizationPlan(null)} />
      )}
    </div>}
  </main>
}
