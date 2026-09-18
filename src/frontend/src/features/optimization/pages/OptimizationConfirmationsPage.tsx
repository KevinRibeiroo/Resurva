import React, { useEffect, useState } from 'react'
import { useParams, useLocation, useNavigate, Link } from 'react-router-dom'
import { getOptimizationPlan } from '../services/optimizationService'
import {
  normalizeSafetyLevel,
  type OptimizationPlanModel,
  type OptimizationSuggestionModel,
  type OptimizationDecisionModel,
} from '../models/OptimizationPlanModel'
import { Card } from '../../../shared/ui/Card/Card'
import { Button } from '../../../shared/ui/Button/Button'
import { Badge } from '../../../shared/ui/Badge/Badge'
import { Alert } from '../../../shared/ui/Alert/Alert'
import { Spinner } from '../../../shared/ui/Spinner/Spinner'
import styles from './OptimizationConfirmationsPage.module.css'

interface ItemConfirmationState {
  choice: 'unselected' | 'practical' | 'theoretical' | 'none'
  confirmed: boolean
  userDeclaration: string
}

export function OptimizationConfirmationsPage() {
  const { optimizationId } = useParams<{ optimizationId: string }>()
  const location = useLocation()
  const navigate = useNavigate()

  const [plan, setPlan] = useState<OptimizationPlanModel | null>(() => {
    return (location.state as { plan?: OptimizationPlanModel })?.plan || null
  })
  const [loading, setLoading] = useState<boolean>(!plan)
  const [error, setError] = useState<string | null>(null)

  // NeedsConfirmation decisions state
  const [confirmations, setConfirmations] = useState<Record<string, ItemConfirmationState>>({})

  // Fetch plan if accessed directly by URL
  useEffect(() => {
    if (!optimizationId) return
    if (plan && plan.id === optimizationId) return

    let isMounted = true
    setLoading(true)
    setError(null)

    getOptimizationPlan(optimizationId)
      .then((data) => {
        if (isMounted) {
          setPlan(data)
          setLoading(false)
        }
      })
      .catch((err) => {
        if (isMounted) {
          setError(err instanceof Error ? err.message : 'Não foi possível carregar o plano de otimização.')
          setLoading(false)
        }
      })

    return () => {
      isMounted = false
    }
  }, [optimizationId])

  // Initialize confirmations when plan is loaded
  useEffect(() => {
    if (!plan) return

    const initial: Record<string, ItemConfirmationState> = {}
    for (const item of plan.suggestions) {
      const level = normalizeSafetyLevel(item.level)
      if (level === 'NeedsConfirmation') {
        initial[item.id] = {
          choice: 'unselected',
          confirmed: false,
          userDeclaration: '',
        }
      }
    }
    setConfirmations(initial)
  }, [plan])

  if (loading) {
    return (
      <div className={styles.centerContainer}>
        <Spinner size="lg" label="Carregando itens para confirmação ética…" />
      </div>
    )
  }

  if (error || !plan) {
    return (
      <div className="app-container" style={{ paddingTop: 'var(--space-2xl)' }}>
        <Card variant="glass" padding="xl" style={{ maxWidth: '600px', margin: '0 auto', textAlign: 'center' }}>
          <Alert variant="error" title="Erro ao carregar confirmações">
            {error || 'Plano de otimização não encontrado.'}
          </Alert>
          <div style={{ marginTop: 'var(--space-lg)', display: 'flex', justifyContent: 'center' }}>
            <Link to="/app/analises/nova">
              <Button variant="primary" icon="refresh">
                Nova Análise
              </Button>
            </Link>
          </div>
        </Card>
      </div>
    )
  }

  const needsConfirmationItems = plan.suggestions.filter(
    (s) => normalizeSafetyLevel(s.level) === 'NeedsConfirmation'
  )

  function handleChoiceChange(id: string, choice: 'practical' | 'theoretical' | 'none') {
    setConfirmations((prev) => {
      const current = prev[id] || { choice: 'unselected', confirmed: false, userDeclaration: '' }
      if (choice === 'practical') {
        return {
          ...prev,
          [id]: {
            ...current,
            choice: 'practical',
            // Practical experience automatically satisfies confirmation when selected
            confirmed: true,
          },
        }
      } else if (choice === 'theoretical') {
        return {
          ...prev,
          [id]: {
            ...current,
            choice: 'theoretical',
            // If theoretical, require declaration review and checkbox
            confirmed: false,
            userDeclaration: current.userDeclaration || '',
          },
        }
      } else {
        // 'none' -> rejected
        return {
          ...prev,
          [id]: {
            ...current,
            choice: 'none',
            confirmed: false,
            userDeclaration: '',
          },
        }
      }
    })
  }

  function handleDeclarationTextChange(id: string, text: string) {
    // Critical Rule: Editing declaration text invalidates existing confirmation and requires re-check
    setConfirmations((prev) => ({
      ...prev,
      [id]: {
        ...prev[id],
        userDeclaration: text,
        confirmed: false,
      },
    }))
  }

  function handleDeclarationConfirmToggle(id: string, checked: boolean) {
    setConfirmations((prev) => ({
      ...prev,
      [id]: {
        ...prev[id],
        confirmed: checked,
      },
    }))
  }

  function buildDecisions(): OptimizationDecisionModel[] {
    const decisions: OptimizationDecisionModel[] = []

    for (const suggestion of plan!.suggestions) {
      const level = normalizeSafetyLevel(suggestion.level)

      if (level === 'Forbidden') {
        decisions.push({
          suggestionId: suggestion.id,
          accepted: false,
          confirmed: false,
        })
      } else if (level === 'Safe') {
        decisions.push({
          suggestionId: suggestion.id,
          accepted: true,
          confirmed: false,
        })
      } else {
        const itemState = confirmations[suggestion.id]
        if (!itemState) {
          decisions.push({
            suggestionId: suggestion.id,
            accepted: false,
            confirmed: false,
          })
          continue
        }

        if (itemState.choice === 'practical' && itemState.confirmed) {
          decisions.push({
            suggestionId: suggestion.id,
            accepted: true,
            confirmed: true,
            userDeclaration: null,
          })
        } else if (itemState.choice === 'theoretical' && itemState.confirmed && itemState.userDeclaration.trim()) {
          decisions.push({
            suggestionId: suggestion.id,
            accepted: true,
            confirmed: true,
            userDeclaration: itemState.userDeclaration.trim(),
          })
        } else {
          decisions.push({
            suggestionId: suggestion.id,
            accepted: false,
            confirmed: false,
          })
        }
      }
    }

    return decisions
  }

  function handleProceed() {
    const decisions = buildDecisions()
    navigate(`/app/adaptacoes/${plan!.id}`, {
      state: {
        plan,
        decisions,
      },
    })
  }

  function handleSkip() {
    // Skipping confirmations: keeps Safe items accepted, sets all NeedsConfirmation to rejected
    const decisions: OptimizationDecisionModel[] = plan!.suggestions.map((s) => {
      const level = normalizeSafetyLevel(s.level)
      return {
        suggestionId: s.id,
        accepted: level === 'Safe',
        confirmed: false,
      }
    })

    navigate(`/app/adaptacoes/${plan!.id}`, {
      state: {
        plan,
        decisions,
      },
    })
  }

  // Count reviewed items
  const reviewedCount = Object.values(confirmations).filter(
    (item) => item.choice !== 'unselected'
  ).length
  const totalNeeds = needsConfirmationItems.length

  return (
    <div className="app-container" style={{ paddingTop: 'var(--space-xl)', paddingBottom: 'var(--space-3xl)' }}>
      {/* Header Block */}
      <div className={styles.headerBlock}>
        <div className={styles.stepIndicator}>
          <span className="material-symbols-outlined" style={{ fontSize: '18px', color: 'var(--color-primary)' }}>
            verified_user
          </span>
          <span className="font-label-caps" style={{ color: 'var(--color-primary)' }}>
            Revisão Humana Ética • Etapa de Confirmação
          </span>
        </div>

        <h1 className="font-headline-lg" style={{ color: 'var(--color-text-high)', margin: 'var(--space-xs) 0' }}>
          Confirmação de Informações
        </h1>

        <p className="font-body-md" style={{ color: 'var(--color-text-muted)', maxWidth: '680px' }}>
          Identificamos competências sugeridas que não constam com clareza documental no seu currículo. Confirme apenas o que você realmente domina para mantermos 100% de autenticidade.
        </p>
      </div>

      {/* Ethical Commitment Banner */}
      <Card variant="surface" padding="md" className={styles.ethicalBanner}>
        <div className={styles.shieldIconBox}>
          <span className="material-symbols-outlined" style={{ color: 'var(--color-tertiary)' }}>
            shield
          </span>
        </div>
        <div>
          <strong className="font-body-md" style={{ color: 'var(--color-text-high)', display: 'block' }}>
            Compromisso Ético de Precisão
          </strong>
          <span className="font-body-sm" style={{ color: 'var(--color-text-muted)' }}>
            O Resurva nunca inventa experiências ou competências. Informações não explícitas nunca são incluídas sem sua expressa validação.
          </span>
        </div>
      </Card>

      {/* Confirmation Items */}
      {needsConfirmationItems.length === 0 ? (
        <Card variant="glass" padding="xl" style={{ textAlign: 'center', marginBottom: 'var(--space-xl)' }}>
          <p className="font-body-md" style={{ color: 'var(--color-text-high)' }}>
            Nenhuma sugestão deste plano requer confirmação adicional. Todas as melhorias identificadas possuem respaldo factual prévio.
          </p>
          <div style={{ marginTop: 'var(--space-md)' }}>
            <Button variant="primary" onClick={handleProceed} icon="arrow_forward">
              Avançar para Revisão da Adaptação
            </Button>
          </div>
        </Card>
      ) : (
        <div className={styles.cardsList}>
          {needsConfirmationItems.map((item, idx) => {
            const itemState = confirmations[item.id] || {
              choice: 'unselected',
              confirmed: false,
              userDeclaration: '',
            }

            return (
              <Card key={item.id} variant="surface" padding="lg" className={styles.itemCard}>
                <div className={styles.itemCardHeader}>
                  <div className={styles.indexTitleBox}>
                    <span className={`${styles.itemIndex} font-label-code`}>
                      {String(idx + 1).padStart(2, '0')}
                    </span>
                    <h2 className="font-headline-sm" style={{ color: 'var(--color-text-high)' }}>
                      {item.proposedText}
                    </h2>
                  </div>
                  <Badge variant="primary" size="sm" caps>
                    Requer Validação
                  </Badge>
                </div>

                {/* Context Block */}
                <div className={styles.contextGrid}>
                  <div className={styles.contextCol}>
                    <span className="font-label-caps" style={{ color: 'var(--color-text-muted)' }}>
                      Texto no Currículo
                    </span>
                    <p className="font-body-sm" style={{ color: 'var(--color-text-high)', marginTop: '0.25rem' }}>
                      {item.originalText ? `“${item.originalText}”` : 'Não identificado explicitamente'}
                    </p>
                  </div>

                  <div className={styles.contextCol}>
                    <span className="font-label-caps" style={{ color: 'var(--color-text-muted)' }}>
                      Justificativa da Sugestão
                    </span>
                    <p className="font-body-sm" style={{ color: 'var(--color-text-muted)', marginTop: '0.25rem' }}>
                      {item.reason}
                    </p>
                  </div>
                </div>

                {/* Question and Choices */}
                <div className={styles.questionSection}>
                  <p className="font-body-md" style={{ fontWeight: 600, color: 'var(--color-text-high)', marginBottom: 'var(--space-sm)' }}>
                    {item.confirmationQuestion || 'Você possui vivência ou conhecimento nesta competência?'}
                  </p>

                  <div className={styles.choicesList}>
                    {/* Option 1: Practical Experience */}
                    <label
                      className={`${styles.choiceOption} ${
                        itemState.choice === 'practical' ? styles.choiceSelected : ''
                      }`}
                    >
                      <input
                        type="radio"
                        name={`choice-${item.id}`}
                        checked={itemState.choice === 'practical'}
                        onChange={() => handleChoiceChange(item.id, 'practical')}
                        className={styles.radioInput}
                      />
                      <div className={styles.choiceTextCol}>
                        <strong className="font-body-md" style={{ color: 'var(--color-text-high)' }}>
                          Tenho experiência prática comprovada
                        </strong>
                        <span className="font-body-sm" style={{ color: 'var(--color-text-muted)' }}>
                          Incorpora o termo técnico sugerido no contexto das suas atividades profissionais.
                        </span>
                      </div>
                      <span className="material-symbols-outlined" style={{ color: 'var(--color-emerald)' }}>
                        check_circle
                      </span>
                    </label>

                    {/* Option 2: Knowledge / Theoretical */}
                    <label
                      className={`${styles.choiceOption} ${
                        itemState.choice === 'theoretical' ? styles.choiceSelected : ''
                      }`}
                    >
                      <input
                        type="radio"
                        name={`choice-${item.id}`}
                        checked={itemState.choice === 'theoretical'}
                        onChange={() => handleChoiceChange(item.id, 'theoretical')}
                        className={styles.radioInput}
                      />
                      <div className={styles.choiceTextCol}>
                        <strong className="font-body-md" style={{ color: 'var(--color-text-high)' }}>
                          Tenho conhecimento / estudo, mas não experiência profissional sênior
                        </strong>
                        <span className="font-body-sm" style={{ color: 'var(--color-text-muted)' }}>
                          Permite redigir uma declaração verdadeira em suas próprias palavras para ser incorporada como declaração sua.
                        </span>
                      </div>
                      <span className="material-symbols-outlined" style={{ color: 'var(--color-primary)' }}>
                        school
                      </span>
                    </label>

                    {/* Theoretical Declaration Sub-panel */}
                    {itemState.choice === 'theoretical' && (
                      <div className={styles.declarationPanel}>
                        <label
                          htmlFor={`decl-text-${item.id}`}
                          className="font-body-sm"
                          style={{ fontWeight: 600, color: 'var(--color-text-high)', display: 'block', marginBottom: '0.25rem' }}
                        >
                          Redija o texto exato que será incorporado ao currículo:
                        </label>
                        <p className="font-body-sm" style={{ color: 'var(--color-text-muted)', fontSize: '0.75rem', marginBottom: 'var(--space-xs)' }}>
                          * Nota de transparência: Este texto exato substituirá a sugestão e será marcado como “Declarado pelo usuário”.
                        </p>
                        <textarea
                          id={`decl-text-${item.id}`}
                          className={styles.declTextarea}
                          rows={3}
                          placeholder="Ex: Possuo conhecimento teórico e cursos concluídos sobre esta metodologia, com aplicação em projetos acadêmicos..."
                          value={itemState.userDeclaration}
                          onChange={(e) => handleDeclarationTextChange(item.id, e.target.value)}
                        />

                        <label className={styles.confirmCheckboxLabel}>
                          <input
                            type="checkbox"
                            checked={itemState.confirmed}
                            onChange={(e) => handleDeclarationConfirmToggle(item.id, e.target.checked)}
                            disabled={!itemState.userDeclaration.trim()}
                          />
                          <span className="font-body-sm" style={{ color: 'var(--color-text-high)' }}>
                            Confirmo que a declaração acima é verídica e reflete exatamente minha formação.
                          </span>
                        </label>
                        {!itemState.confirmed && (
                          <span className={styles.checkboxReminder}>
                            Marque a confirmação para que sua declaração seja incluída na adaptação.
                          </span>
                        )}
                      </div>
                    )}

                    {/* Option 3: None / Skip */}
                    <label
                      className={`${styles.choiceOption} ${
                        itemState.choice === 'none' ? styles.choiceSelected : ''
                      }`}
                    >
                      <input
                        type="radio"
                        name={`choice-${item.id}`}
                        checked={itemState.choice === 'none'}
                        onChange={() => handleChoiceChange(item.id, 'none')}
                        className={styles.radioInput}
                      />
                      <div className={styles.choiceTextCol}>
                        <strong className="font-body-md" style={{ color: 'var(--color-text-high)' }}>
                          Não possuo experiência ou conhecimento
                        </strong>
                        <span className="font-body-sm" style={{ color: 'var(--color-text-muted)' }}>
                          O item será descartado e o texto original do seu currículo será rigorosamente mantido.
                        </span>
                      </div>
                      <span className="material-symbols-outlined" style={{ color: 'var(--color-crimson)' }}>
                        block
                      </span>
                    </label>
                  </div>
                </div>
              </Card>
            )
          })}
        </div>
      )}

      {/* Sticky Bottom Confirmation Dock */}
      <aside className={styles.stickyDock}>
        <div className={styles.dockProgressCol}>
          <div className={styles.ringMini}>
            <span className={`${styles.ringCount} font-label-code`}>
              {reviewedCount}/{totalNeeds}
            </span>
          </div>
          <div>
            <span className="font-body-md" style={{ fontWeight: 600, color: 'var(--color-text-high)' }}>
              {reviewedCount === totalNeeds
                ? 'Todos os itens revisados'
                : `${totalNeeds - reviewedCount} itens pendentes de revisão`}
            </span>
            <span className="font-body-sm" style={{ color: 'var(--color-text-muted)', display: 'block', fontSize: '0.75rem' }}>
              Sua validação garante total integridade factual.
            </span>
          </div>
        </div>

        <div className={styles.dockActions}>
          <Button variant="ghost" onClick={handleSkip} title="Manter apenas dados confirmados e originais">
            Pular confirmações e manter dados originais
          </Button>
          <Button variant="primary" icon="arrow_forward" onClick={handleProceed}>
            Confirmar e Gerar Versão Adaptada
          </Button>
        </div>
      </aside>
    </div>
  )
}
