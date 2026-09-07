import { useState } from 'react'
import { applyOptimization, downloadAdaptedResume } from './api'
import type {
  OptimizationPlan,
  OptimizationDecision,
  OptimizationResult,
  OptimizationSuggestion,
  OptimizationSafetyLevel
} from './types'

export function normalizeLevel(level: unknown): OptimizationSafetyLevel {
  if (level === 0 || level === '0' || level === 'Safe' || level === 'safe') return 'Safe'
  if (level === 1 || level === '1' || level === 'NeedsConfirmation' || level === 'needsconfirmation') return 'NeedsConfirmation'
  if (level === 2 || level === '2' || level === 'Forbidden' || level === 'forbidden') return 'Forbidden'
  return 'NeedsConfirmation'
}

interface OptimizationViewProps {
  plan: OptimizationPlan
  onBack?: () => void
}

interface DecisionState {
  accepted: boolean
  confirmed: boolean
  userDeclaration: string
}

export function OptimizationView({ plan, onBack }: OptimizationViewProps) {
  // State for user decisions per suggestion: accepted, confirmed, userDeclaration
  const [decisions, setDecisions] = useState<Record<string, DecisionState>>(() => {
    const initial: Record<string, DecisionState> = {}
    for (const suggestion of plan.suggestions) {
      const level = normalizeLevel(suggestion.level)
      if (level === 'Forbidden') {
        initial[suggestion.id] = { accepted: false, confirmed: false, userDeclaration: '' }
      } else if (level === 'Safe') {
        initial[suggestion.id] = { accepted: true, confirmed: false, userDeclaration: '' }
      } else {
        // NeedsConfirmation is NEVER pre-accepted or pre-confirmed
        initial[suggestion.id] = { accepted: false, confirmed: false, userDeclaration: '' }
      }
    }
    return initial
  })

  const [busy, setBusy] = useState(false)
  const [exportingFormat, setExportingFormat] = useState<'pdf' | 'docx' | null>(null)
  const [error, setError] = useState('')
  const [result, setResult] = useState<OptimizationResult | null>(null)
  const [copied, setCopied] = useState(false)

  function toggleAccept(id: string, accepted: boolean) {
    setDecisions(prev => ({
      ...prev,
      [id]: {
        ...prev[id],
        accepted,
        // Reset confirmation if rejecting
        confirmed: accepted ? prev[id]?.confirmed ?? false : false
      }
    }))
  }

  function toggleConfirm(id: string, confirmed: boolean) {
    setDecisions(prev => ({
      ...prev,
      [id]: {
        ...prev[id],
        confirmed
      }
    }))
  }

  function changeDeclaration(id: string, userDeclaration: string) {
    setDecisions(prev => ({
      ...prev,
      [id]: {
        ...prev[id],
        userDeclaration
      }
    }))
  }

  async function handleApply() {
    setBusy(true)
    setError('')
    try {
      const decisionList: OptimizationDecision[] = Object.entries(decisions).map(([suggestionId, val]) => ({
        suggestionId,
        accepted: val.accepted,
        confirmed: val.confirmed,
        userDeclaration: val.userDeclaration.trim() ? val.userDeclaration.trim() : undefined
      }))

      const res = await applyOptimization(plan.id, plan.version, decisionList)
      setResult(res)
    } catch (caught) {
      setError(caught instanceof Error ? caught.message : 'Erro ao aplicar adaptação.')
    } finally {
      setBusy(false)
    }
  }

  async function handleCopy() {
    if (!result?.adaptedText) return
    try {
      await navigator.clipboard.writeText(result.adaptedText)
      setCopied(true)
      setTimeout(() => setCopied(false), 2500)
    } catch {
      setError('Não foi possível copiar automaticamente para a área de transferência.')
    }
  }

  async function handleExport(format: 'pdf' | 'docx') {
    setExportingFormat(format)
    setError('')
    try {
      await downloadAdaptedResume(plan.id, format)
    } catch (caught) {
      setError(caught instanceof Error ? caught.message : `Erro ao exportar currículo em ${format.toUpperCase()}.`)
    } finally {
      setExportingFormat(null)
    }
  }

  return (
    <section className="optimization-container" aria-label="Adaptação de currículo">
      <div className="optimization-header">
        <div className="header-nav">
          <span className="badge-pill">Fluxo Guiado</span>
          {onBack && (
            <button type="button" className="btn-back" onClick={onBack}>
              ← Voltar à Análise
            </button>
          )}
        </div>
        <h2>Sugestões de Adaptação para a Vaga</h2>
        <p className="muted">
          Revise com atenção as sugestões abaixo. Nenhuma informação será alterada ou inventada sem a sua expressa validação.
        </p>
      </div>

      {error && <p role="alert" className="error">{error}</p>}

      {!result ? (
        <div className="optimization-content">
          <div className="suggestions-list">
            {plan.suggestions.map((item, index) => (
              <SuggestionCard
                key={item.id}
                index={index + 1}
                suggestion={item}
                accepted={decisions[item.id]?.accepted ?? false}
                confirmed={decisions[item.id]?.confirmed ?? false}
                userDeclaration={decisions[item.id]?.userDeclaration ?? ''}
                onToggleAccept={accept => toggleAccept(item.id, accept)}
                onToggleConfirm={confirm => toggleConfirm(item.id, confirm)}
                onChangeDeclaration={decl => changeDeclaration(item.id, decl)}
              />
            ))}
          </div>

          <div className="optimization-actions">
            <button
              type="button"
              className="btn-primary"
              disabled={busy}
              onClick={handleApply}
            >
              {busy ? 'Aplicando alterações…' : 'Aplicar alterações aprovadas'}
            </button>
          </div>
        </div>
      ) : (
        <div className="adapted-result-view">
          <div className="result-banner">
            <h3>Currículo Adaptado Gerado com Sucesso!</h3>
            <p>
              Foram aplicadas <strong>{result.appliedChanges.length}</strong> alterações autorizadas.
              Seu currículo original e análise prévia continuam intactos.
            </p>
          </div>

          <div className="adapted-preview-container">
            <div className="preview-toolbar">
              <label htmlFor="adapted-text-area" className="preview-label">Texto adaptado final:</label>
              <div className="action-button-group">
                <button
                  type="button"
                  className="btn-export btn-export-pdf"
                  disabled={exportingFormat !== null}
                  onClick={() => handleExport('pdf')}
                >
                  {exportingFormat === 'pdf' ? 'Gerando PDF…' : '📄 Baixar PDF'}
                </button>
                <button
                  type="button"
                  className="btn-export btn-export-docx"
                  disabled={exportingFormat !== null}
                  onClick={() => handleExport('docx')}
                >
                  {exportingFormat === 'docx' ? 'Gerando DOCX…' : '📝 Baixar DOCX'}
                </button>
                <button
                  type="button"
                  className="btn-secondary btn-copy"
                  onClick={handleCopy}
                >
                  {copied ? '✓ Copiado!' : 'Copiar texto'}
                </button>
                <button
                  type="button"
                  className="btn-secondary btn-edit-decisions"
                  onClick={() => setResult(null)}
                >
                  ✏️ Editar decisões
                </button>
              </div>
            </div>
            <textarea
              id="adapted-text-area"
              readOnly
              rows={16}
              value={result.adaptedText}
              className="adapted-text-output"
            />
          </div>

          <div className="applied-summary">
            <h4>Alterações aplicadas nesta versão:</h4>
            <ul>
              {result.appliedChanges.map(item => {
                const itemLevel = normalizeLevel(item.level)
                const isUserDeclared = item.informationOrigin === 'DeclaradaPeloUsuario'
                return (
                  <li key={item.suggestionId} className="applied-item">
                    <div className="applied-item-header">
                      <strong>[{itemLevel === 'Safe' ? 'Segura' : 'Confirmada'}]</strong>
                      <span className={`badge ${isUserDeclared ? 'badge-info' : 'badge-safe'}`}>
                        {isUserDeclared ? 'Origem: Declarada pelo Candidato' : 'Origem: Currículo Original'}
                      </span>
                    </div>
                    <p className="applied-item-text">{item.proposedText}</p>
                    {item.userDeclaration && (
                      <p className="user-declaration-snippet">
                        <strong>Contexto informado:</strong> “{item.userDeclaration}”
                      </p>
                    )}
                    <small className="muted">{item.reason}</small>
                  </li>
                )
              })}
            </ul>
          </div>
        </div>
      )}
    </section>
  )
}

interface SuggestionCardProps {
  index: number
  suggestion: OptimizationSuggestion
  accepted: boolean
  confirmed: boolean
  userDeclaration: string
  onToggleAccept: (accepted: boolean) => void
  onToggleConfirm: (confirmed: boolean) => void
  onChangeDeclaration: (declaration: string) => void
}

function SuggestionCard({
  index,
  suggestion,
  accepted,
  confirmed,
  userDeclaration,
  onToggleAccept,
  onToggleConfirm,
  onChangeDeclaration
}: SuggestionCardProps) {
  const level = normalizeLevel(suggestion.level)
  const isForbidden = level === 'Forbidden'
  const isNeedsConfirmation = level === 'NeedsConfirmation'

  return (
    <article className={`suggestion-card level-${level.toLowerCase()}`}>
      <div className="card-header">
        <span className="card-index">#{index}</span>
        <LevelBadge level={level} />
      </div>

      <div className="card-body">
        {suggestion.originalText && (
          <div className="diff-block original-block">
            <span className="block-tag">Trecho original do currículo:</span>
            <blockquote>“{suggestion.originalText}”</blockquote>
          </div>
        )}

        <div className="diff-block proposed-block">
          <span className="block-tag">Texto proposto:</span>
          <blockquote>“{suggestion.proposedText}”</blockquote>
        </div>

        {suggestion.evidence && (
          <div className="card-evidence">
            <small><strong>Evidência documental no currículo:</strong> “{suggestion.evidence}”</small>
          </div>
        )}

        <div className="card-reason">
          <small><strong>Justificativa:</strong> {suggestion.reason}</small>
        </div>

        {isForbidden && (
          <div className="forbidden-notice" role="alert">
            <p>
              ⚠️ <strong>Sugestão Bloqueada:</strong> Esta recomendação implica alegações sem respaldo factual ou inventadas.
              Por razões de integridade ética e segurança, não é permitido incorporar este item.
            </p>
          </div>
        )}

        {!isForbidden && isNeedsConfirmation && accepted && (
          <div className="confirmation-box">
            <div className="confirmation-question-box">
              <span className="question-title">Confirmação de Experiência:</span>
              <p className="question-text">
                {suggestion.confirmationQuestion ?? 'Você confirma que possui vivência real com esta competência para incluí-la no currículo?'}
              </p>
            </div>

            <label className="checkbox-label">
              <input
                type="checkbox"
                checked={confirmed}
                onChange={e => onToggleConfirm(e.target.checked)}
              />
              <span>
                Confirmo expressamente que esta informação é verdadeira e vivenciada por mim.
              </span>
            </label>

            <div className="declaration-field">
              <label htmlFor={`decl-${suggestion.id}`} className="declaration-label">
                Detalhes ou contexto adicional da sua experiência (opcional):
              </label>
              <input
                id={`decl-${suggestion.id}`}
                type="text"
                className="declaration-input"
                placeholder="Ex: Atuação em microsserviços por 2 anos com alta volumetria..."
                value={userDeclaration}
                onChange={e => onChangeDeclaration(e.target.value)}
              />
            </div>

            {!confirmed && (
              <p className="warning-text">
                * Para que esta inclusão seja aplicada, marque a caixa confirmando a veracidade da informação.
              </p>
            )}
          </div>
        )}
      </div>

      <div className="card-actions">
        {isForbidden ? (
          <button type="button" disabled className="btn-blocked">Bloqueado</button>
        ) : (
          <div className="decision-toggle">
            <button
              type="button"
              className={`toggle-btn ${accepted ? 'active-accept' : ''}`}
              onClick={() => onToggleAccept(true)}
            >
              Aceitar sugestão
            </button>
            <button
              type="button"
              className={`toggle-btn ${!accepted ? 'active-reject' : ''}`}
              onClick={() => onToggleAccept(false)}
            >
              Rejeitar
            </button>
          </div>
        )}
      </div>
    </article>
  )
}

function LevelBadge({ level }: { level: unknown }) {
  const normalized = normalizeLevel(level)
  switch (normalized) {
    case 'Safe':
      return <span className="badge badge-safe">Segura (Evidenciada)</span>
    case 'NeedsConfirmation':
      return <span className="badge badge-warning">Requer Confirmação</span>
    case 'Forbidden':
      return <span className="badge badge-danger">Bloqueada (Invenção)</span>
    default:
      return <span className="badge">{String(level)}</span>
  }
}
