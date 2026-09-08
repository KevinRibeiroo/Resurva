import React, { useEffect, useState } from 'react'
import { useParams, useLocation, useNavigate, Link } from 'react-router-dom'
import { getAnalysis } from '../services/analysisService'
import { createOptimizationPlan } from '../../optimization/services/optimizationService'
import type { AnalysisResultModel, EvidenceItemModel } from '../models/AnalysisResultModel'
import { normalizeSafetyLevel } from '../../optimization/models/OptimizationPlanModel'
import { Card } from '../../../shared/ui/Card/Card'
import { Button } from '../../../shared/ui/Button/Button'
import { Badge } from '../../../shared/ui/Badge/Badge'
import { ScoreRing } from '../../../shared/ui/ScoreRing/ScoreRing'
import { Spinner } from '../../../shared/ui/Spinner/Spinner'
import { Alert } from '../../../shared/ui/Alert/Alert'
import { EmptyState } from '../../../shared/ui/EmptyState/EmptyState'
import styles from './AnalysisResultPage.module.css'

export function AnalysisResultPage() {
  const { analysisId } = useParams<{ analysisId: string }>()
  const location = useLocation()
  const navigate = useNavigate()

  // State
  const [analysis, setAnalysis] = useState<AnalysisResultModel | null>(() => {
    return (location.state as { analysisResult?: AnalysisResultModel })?.analysisResult || null
  })
  const [loading, setLoading] = useState<boolean>(!analysis)
  const [error, setError] = useState<string | null>(null)

  // Optimization CTA state
  const [optimizing, setOptimizing] = useState<boolean>(false)
  const [optError, setOptError] = useState<string | null>(null)

  // Expanded evidence state for keyboard/touch interaction
  const [expandedEvidences, setExpandedEvidences] = useState<Record<string, boolean>>({})

  useEffect(() => {
    if (!analysisId) return

    // If we already have the analysis matching the URL ID, do not refetch unnecessarily
    if (analysis && analysis.id === analysisId) {
      return
    }

    let isMounted = true
    setLoading(true)
    setError(null)

    getAnalysis(analysisId)
      .then((data) => {
        if (isMounted) {
          setAnalysis(data)
          setLoading(false)
        }
      })
      .catch((err) => {
        if (isMounted) {
          setError(err instanceof Error ? err.message : 'Não foi possível carregar os dados da análise.')
          setLoading(false)
        }
      })

    return () => {
      isMounted = false
    }
  }, [analysisId])

  function toggleEvidence(key: string) {
    setExpandedEvidences((prev) => ({
      ...prev,
      [key]: !prev[key],
    }))
  }

  async function handleStartOptimization() {
    if (!analysis) return
    setOptimizing(true)
    setOptError(null)

    try {
      const plan = await createOptimizationPlan(analysis.id)
      const hasNeedsConfirmation = plan.suggestions.some(
        (s) => normalizeSafetyLevel(s.level) === 'NeedsConfirmation'
      )

      if (hasNeedsConfirmation) {
        navigate(`/app/adaptacoes/${plan.id}/confirmacoes`, { state: { plan, analysis } })
      } else {
        navigate(`/app/adaptacoes/${plan.id}`, { state: { plan, analysis } })
      }
    } catch (err) {
      setOptError(err instanceof Error ? err.message : 'Não foi possível gerar o plano de adaptação.')
      setOptimizing(false)
    }
  }

  if (loading) {
    return (
      <div className={styles.centerContainer}>
        <Spinner size="lg" label="Carregando diagnóstico da análise…" />
      </div>
    )
  }

  if (error || !analysis) {
    return (
      <div className="app-container" style={{ paddingTop: 'var(--space-2xl)' }}>
        <Card variant="glass" padding="xl" style={{ maxWidth: '600px', margin: '0 auto', textAlign: 'center' }}>
          <Alert variant="error" title="Erro ao carregar análise">
            {error || 'Análise não encontrada ou você não possui permissão para acessá-la.'}
          </Alert>
          <div style={{ marginTop: 'var(--space-lg)', display: 'flex', justifyContent: 'center', gap: 'var(--space-sm)' }}>
            <Link to="/app/analises/nova">
              <Button variant="primary" icon="add">
                Fazer Nova Análise
              </Button>
            </Link>
          </div>
        </Card>
      </div>
    )
  }

  const overall = Math.round(analysis.overallScore)
  const isHighMatch = overall >= 85
  const isModerateMatch = overall >= 60 && overall < 85

  return (
    <div className="app-container" style={{ paddingTop: 'var(--space-lg)', paddingBottom: 'var(--space-3xl)' }}>
      {/* Top Action Bar */}
      <div className={styles.topBar}>
        <div className={styles.topInfo}>
          <div className={styles.metaRow}>
            <Badge variant="secondary" size="sm" caps>
              Relatório de Diagnóstico
            </Badge>
            <span className={`${styles.analysisIdBadge} font-label-code`}>
              ID: #{analysis.id.slice(0, 8)}
            </span>
          </div>
          <h1 className="font-headline-lg" style={{ color: 'var(--color-text-high)' }}>
            Resultado da Análise de Aderência
          </h1>
        </div>

        <div className={styles.topActions}>
          <Link to="/app/analises/nova">
            <Button variant="secondary" icon="refresh">
              Nova Análise
            </Button>
          </Link>
          <Button
            variant="primary"
            icon="edit_document"
            onClick={handleStartOptimization}
            loading={optimizing}
          >
            Adaptar currículo para esta vaga
          </Button>
        </div>
      </div>

      {optError && (
        <Alert variant="error" style={{ marginBottom: 'var(--space-md)' }}>
          {optError}
        </Alert>
      )}

      {/* Hero Telemetry Card */}
      <Card variant="glass" padding="xl" className={styles.telemetryCard}>
        <div className={styles.telemetryInner}>
          <div className={styles.gaugeArea}>
            <ScoreRing score={analysis.overallScore} size="lg" useGradient />
          </div>

          <div className={styles.summaryArea}>
            <div className={styles.alignmentBadgeRow}>
              {isHighMatch ? (
                <Badge variant="emerald" size="md" icon="check_circle" dot>
                  Alta Aderência
                </Badge>
              ) : isModerateMatch ? (
                <Badge variant="amber" size="md" icon="warning" dot>
                  Aderência Moderada
                </Badge>
              ) : (
                <Badge variant="crimson" size="md" icon="priority_high" dot>
                  Baixa Aderência
                </Badge>
              )}
              <span className="font-body-sm" style={{ color: 'var(--color-text-muted)' }}>
                Índice Geral de Compatibilidade
              </span>
            </div>

            <h2 className="font-headline-md" style={{ color: 'var(--color-text-high)', margin: 'var(--space-xs) 0' }}>
              {isHighMatch
                ? 'Seu currículo atende aos requisitos essenciais da posição'
                : isModerateMatch
                ? 'Seu perfil possui alinhamento parcial com a vaga'
                : 'Existem lacunas relevantes em relação ao descritivo da vaga'}
            </h2>

            <p className="font-body-md" style={{ color: 'var(--color-text-muted)' }}>
              Pontuação calculada pela ponderação exata de competências, anos de experiência, senioridade declarada, requisitos e formação.
            </p>
          </div>

          {/* Quick Metrics Pills */}
          <div className={styles.metricsPillsGrid}>
            <div className={styles.metricPill}>
              <span className="font-body-sm" style={{ color: 'var(--color-text-muted)' }}>Skills Mapeadas</span>
              <div className={styles.metricValueRow}>
                <span className="font-headline-md" style={{ color: 'var(--color-emerald)' }}>
                  {analysis.matchedSkills.length}
                </span>
                <span className="font-label-code" style={{ color: 'var(--color-text-muted)' }}>
                  / {analysis.matchedSkills.length + analysis.missingSkills.length}
                </span>
              </div>
            </div>

            <div className={styles.metricPill}>
              <span className="font-body-sm" style={{ color: 'var(--color-text-muted)' }}>Requisitos Atendidos</span>
              <div className={styles.metricValueRow}>
                <span className="font-headline-md" style={{ color: 'var(--color-tertiary)' }}>
                  {analysis.requirementsMet.length}
                </span>
                <span className="font-label-code" style={{ color: 'var(--color-text-muted)' }}>
                  / {analysis.requirementsMet.length + analysis.requirementsMissing.length}
                </span>
              </div>
            </div>

            <div className={styles.metricPill}>
              <span className="font-body-sm" style={{ color: 'var(--color-text-muted)' }}>Lacunas (Gaps)</span>
              <div className={styles.metricValueRow}>
                <span className="font-headline-md" style={{ color: 'var(--color-crimson)' }}>
                  {analysis.missingSkills.length + analysis.requirementsMissing.length}
                </span>
                <span className="font-label-code" style={{ color: 'var(--color-text-muted)' }}>
                  não identificados
                </span>
              </div>
            </div>

            <div className={styles.metricPill}>
              <span className="font-body-sm" style={{ color: 'var(--color-text-muted)' }}>Oportunidades</span>
              <div className={styles.metricValueRow}>
                <span className="font-headline-md" style={{ color: 'var(--color-secondary)' }}>
                  {analysis.recommendations.length}
                </span>
                <span className="font-label-code" style={{ color: 'var(--color-text-muted)' }}>
                  recomendações
                </span>
              </div>
            </div>
          </div>
        </div>
      </Card>

      {/* 5 DIMENSÕES DETALHADAS */}
      <section className={styles.dimensionsSection} aria-label="Detalhamento das 5 dimensões de pontuação">
        <h3 className="font-headline-sm" style={{ color: 'var(--color-text-high)', marginBottom: 'var(--space-md)' }}>
          Detalhamento das 5 Dimensões
        </h3>
        <div className={styles.dimensionsGrid}>
          <DimensionScoreCard label="Skills & Competências" score={analysis.skillsScore} weight="40%" icon="psychology" />
          <DimensionScoreCard label="Experiência Prática" score={analysis.experienceScore} weight="30%" icon="work" />
          <DimensionScoreCard label="Senioridade Requerida" score={analysis.seniorityScore} weight="15%" icon="trending_up" />
          <DimensionScoreCard label="Requisitos Formais" score={analysis.requirementsScore} weight="10%" icon="rule" />
          <DimensionScoreCard label="Formação e Educação" score={analysis.educationScore} weight="5%" icon="school" />
        </div>
      </section>

      {/* DETALHAMENTO DE SKILLS & REQUISITOS */}
      <div className={styles.comparisonColumns}>
        {/* Coluna 1: Skills */}
        <Card variant="surface" padding="lg" className={styles.columnCard}>
          <div className={styles.columnHeader}>
            <div className={styles.columnTitleBox}>
              <span className="material-symbols-outlined" style={{ color: 'var(--color-primary)' }}>
                psychology
              </span>
              <div>
                <h3 className="font-headline-sm" style={{ color: 'var(--color-text-high)' }}>
                  Competências (Skills)
                </h3>
                <p className="font-body-sm" style={{ color: 'var(--color-text-muted)' }}>
                  Identificação semântica em relação ao descritivo da oportunidade
                </p>
              </div>
            </div>
          </div>

          <div className={styles.columnBody}>
            {/* Skills Encontradas */}
            <div className={styles.blockGroup}>
              <div className={styles.groupHeading}>
                <Badge variant="emerald" size="sm" icon="check_circle">
                  Identificadas no Currículo ({analysis.matchedSkills.length})
                </Badge>
              </div>
              {analysis.matchedSkills.length === 0 ? (
                <p className="font-body-sm" style={{ color: 'var(--color-text-muted)' }}>
                  Nenhuma skill identificada no currículo.
                </p>
              ) : (
                <div className={styles.evidenceChipsWrap}>
                  {analysis.matchedSkills.map((item, idx) => (
                    <InteractiveEvidenceChip
                      key={`skill-found-${idx}`}
                      id={`skill-found-${idx}`}
                      item={item}
                      variant="emerald"
                      isExpanded={expandedEvidences[`skill-found-${idx}`]}
                      onToggle={() => toggleEvidence(`skill-found-${idx}`)}
                    />
                  ))}
                </div>
              )}
            </div>

            {/* Skills Ausentes */}
            <div className={styles.blockGroup} style={{ marginTop: 'var(--space-md)' }}>
              <div className={styles.groupHeading}>
                <Badge variant="crimson" size="sm" icon="cancel">
                  Não Identificadas no Currículo ({analysis.missingSkills.length})
                </Badge>
              </div>
              {analysis.missingSkills.length === 0 ? (
                <p className="font-body-sm" style={{ color: 'var(--color-text-muted)' }}>
                  Todas as skills requeridas foram identificadas!
                </p>
              ) : (
                <div className={styles.evidenceChipsWrap}>
                  {analysis.missingSkills.map((item, idx) => (
                    <InteractiveEvidenceChip
                      key={`skill-missing-${idx}`}
                      id={`skill-missing-${idx}`}
                      item={item}
                      variant="crimson"
                      isExpanded={expandedEvidences[`skill-missing-${idx}`]}
                      onToggle={() => toggleEvidence(`skill-missing-${idx}`)}
                    />
                  ))}
                </div>
              )}
            </div>
          </div>
        </Card>

        {/* Coluna 2: Requisitos Formais */}
        <Card variant="surface" padding="lg" className={styles.columnCard}>
          <div className={styles.columnHeader}>
            <div className={styles.columnTitleBox}>
              <span className="material-symbols-outlined" style={{ color: 'var(--color-tertiary)' }}>
                rule
              </span>
              <div>
                <h3 className="font-headline-sm" style={{ color: 'var(--color-text-high)' }}>
                  Aderência aos Requisitos
                </h3>
                <p className="font-body-sm" style={{ color: 'var(--color-text-muted)' }}>
                  Cruzamento com exigências detalhadas da vaga
                </p>
              </div>
            </div>
          </div>

          <div className={styles.columnBody}>
            {/* Requisitos Atendidos */}
            <div className={styles.blockGroup}>
              <div className={styles.groupHeading}>
                <Badge variant="emerald" size="sm" icon="check_circle">
                  Requisitos Atendidos ({analysis.requirementsMet.length})
                </Badge>
              </div>
              {analysis.requirementsMet.length === 0 ? (
                <p className="font-body-sm" style={{ color: 'var(--color-text-muted)' }}>
                  Nenhum requisito atendido identificado.
                </p>
              ) : (
                <div className={styles.itemList}>
                  {analysis.requirementsMet.map((item, idx) => (
                    <EvidenceRowItem
                      key={`req-met-${idx}`}
                      item={item}
                      variant="emerald"
                      icon="check"
                    />
                  ))}
                </div>
              )}
            </div>

            {/* Requisitos Não Atendidos */}
            <div className={styles.blockGroup} style={{ marginTop: 'var(--space-md)' }}>
              <div className={styles.groupHeading}>
                <Badge variant="crimson" size="sm" icon="cancel">
                  Requisitos Não Atendidos ({analysis.requirementsMissing.length})
                </Badge>
              </div>
              {analysis.requirementsMissing.length === 0 ? (
                <p className="font-body-sm" style={{ color: 'var(--color-text-muted)' }}>
                  Nenhum requisito pendente!
                </p>
              ) : (
                <div className={styles.itemList}>
                  {analysis.requirementsMissing.map((item, idx) => (
                    <EvidenceRowItem
                      key={`req-missing-${idx}`}
                      item={item}
                      variant="crimson"
                      icon="close"
                    />
                  ))}
                </div>
              )}
            </div>
          </div>
        </Card>
      </div>

      {/* PONTOS FORTES E PONTOS DE ATENÇÃO */}
      <div className={styles.insightsGrid}>
        {/* Pontos Fortes */}
        <Card variant="surface" padding="lg" className={styles.insightCard}>
          <div className={styles.insightHeader}>
            <div className={`${styles.insightIconBox} ${styles.iconStrength}`}>
              <span className="material-symbols-outlined">thumb_up</span>
            </div>
            <div>
              <h3 className="font-headline-sm" style={{ color: 'var(--color-text-high)' }}>
                Pontos Fortes Detectados
              </h3>
              <p className="font-body-sm" style={{ color: 'var(--color-text-muted)' }}>
                Aspectos de maior impacto positivo na análise
              </p>
            </div>
          </div>

          {analysis.strengths.length === 0 ? (
            <p className="font-body-sm" style={{ color: 'var(--color-text-muted)' }}>Nenhum ponto forte destacado.</p>
          ) : (
            <ul className={styles.bulletList}>
              {analysis.strengths.map((item, idx) => (
                <li key={`str-${idx}`} className={styles.bulletItem}>
                  <span className="material-symbols-outlined" style={{ color: 'var(--color-emerald)', fontSize: '18px', flexShrink: 0 }}>
                    verified
                  </span>
                  <div>
                    <strong className="font-body-md" style={{ color: 'var(--color-text-high)', display: 'block' }}>
                      {item.text}
                    </strong>
                    {item.evidence && (
                      <span className="font-body-sm" style={{ color: 'var(--color-text-muted)' }}>
                        Evidência: “{item.evidence}”
                      </span>
                    )}
                  </div>
                </li>
              ))}
            </ul>
          )}
        </Card>

        {/* Pontos de Atenção */}
        <Card variant="surface" padding="lg" className={styles.insightCard}>
          <div className={styles.insightHeader}>
            <div className={`${styles.insightIconBox} ${styles.iconAttention}`}>
              <span className="material-symbols-outlined">warning</span>
            </div>
            <div>
              <h3 className="font-headline-sm" style={{ color: 'var(--color-text-high)' }}>
                Pontos de Atenção e Lacunas
              </h3>
              <p className="font-body-sm" style={{ color: 'var(--color-text-muted)' }}>
                Fatores que enfraquecem a pontuação do currículo
              </p>
            </div>
          </div>

          {analysis.pointsOfAttention.length === 0 ? (
            <p className="font-body-sm" style={{ color: 'var(--color-text-muted)' }}>Nenhum ponto de atenção identificado.</p>
          ) : (
            <ul className={styles.bulletList}>
              {analysis.pointsOfAttention.map((item, idx) => (
                <li key={`att-${idx}`} className={styles.bulletItem}>
                  <span className="material-symbols-outlined" style={{ color: 'var(--color-amber)', fontSize: '18px', flexShrink: 0 }}>
                    report_problem
                  </span>
                  <div>
                    <strong className="font-body-md" style={{ color: 'var(--color-text-high)', display: 'block' }}>
                      {item.text}
                    </strong>
                    {item.evidence && (
                      <span className="font-body-sm" style={{ color: 'var(--color-text-muted)' }}>
                        Evidência: “{item.evidence}”
                      </span>
                    )}
                  </div>
                </li>
              ))}
            </ul>
          )}
        </Card>
      </div>

      {/* SUGESTÕES PRÁTICAS DE MELHORIA / RECOMENDAÇÕES */}
      {analysis.recommendations.length > 0 && (
        <Card variant="surface" padding="lg" className={styles.recommendationsCard}>
          <div className={styles.recHeader}>
            <div className={`${styles.insightIconBox} ${styles.iconRec}`}>
              <span className="material-symbols-outlined">tips_and_updates</span>
            </div>
            <div>
              <h3 className="font-headline-sm" style={{ color: 'var(--color-text-high)' }}>
                Sugestões Práticas de Melhoria
              </h3>
              <p className="font-body-sm" style={{ color: 'var(--color-text-muted)' }}>
                Recomendações objetivas para aumentar sua relevância e valorizar sua trajetória
              </p>
            </div>
          </div>

          <div className={styles.recommendationsGrid}>
            {analysis.recommendations.map((item, idx) => (
              <div key={`rec-${idx}`} className={styles.recBox}>
                <div className={styles.recBoxHeader}>
                  <span className="material-symbols-outlined" style={{ fontSize: '18px', color: 'var(--color-primary)' }}>
                    auto_fix_high
                  </span>
                  <span className="font-body-sm" style={{ fontWeight: 700, color: 'var(--color-primary)' }}>
                    Sugestão {idx + 1}
                  </span>
                </div>
                <p className="font-body-md" style={{ color: 'var(--color-text-high)', marginBottom: 'var(--space-xs)' }}>
                  {item.text}
                </p>
                {item.evidence && (
                  <div className={styles.recEvidenceCallout}>
                    <span className="font-label-code" style={{ color: 'var(--color-text-muted)' }}>
                      Referência: “{item.evidence}”
                    </span>
                  </div>
                )}
              </div>
            ))}
          </div>
        </Card>
      )}

      {/* Bottom Master Action Dock */}
      <Card variant="glow" padding="xl" className={styles.bottomDock}>
        <div className={styles.dockText}>
          <span className="font-label-caps" style={{ color: 'var(--color-primary)' }}>
            Próximo Passo Recomendado
          </span>
          <h3 className="font-headline-md" style={{ color: 'var(--color-text-high)', margin: '0.25rem 0' }}>
            Deseja adaptar seu currículo para esta oportunidade?
          </h3>
          <p className="font-body-md" style={{ color: 'var(--color-text-muted)' }}>
            Gere uma versão alinhada com melhorias na redação e confirmação expressa de experiências não explícitas.
          </p>
        </div>

        <div className={styles.dockActions}>
          <Link to="/app/analises/nova">
            <Button variant="secondary" icon="refresh">
              Fazer nova análise
            </Button>
          </Link>
          <Button
            variant="primary"
            size="lg"
            icon="edit_document"
            onClick={handleStartOptimization}
            loading={optimizing}
          >
            Adaptar currículo para esta vaga
          </Button>
        </div>
      </Card>
    </div>
  )
}

/* Subcomponents */

function DimensionScoreCard({
  label,
  score,
  weight,
  icon,
}: {
  label: string
  score: number
  weight: string
  icon: string
}) {
  const rounded = Math.round(score)
  let color = 'var(--color-crimson)'
  if (rounded >= 85) color = 'var(--color-emerald)'
  else if (rounded >= 60) color = 'var(--color-amber)'

  return (
    <div className={styles.dimensionCard}>
      <div className={styles.dimTopRow}>
        <span className="material-symbols-outlined" style={{ fontSize: '20px', color: 'var(--color-primary)' }}>
          {icon}
        </span>
        <span className={`${styles.dimWeight} font-label-code`}>Peso {weight}</span>
      </div>
      <span className={styles.dimLabel}>{label}</span>
      <div className={styles.dimScoreRow}>
        <strong className="font-headline-md" style={{ color }}>{rounded}</strong>
        <span className="font-label-code" style={{ color: 'var(--color-text-muted)', fontSize: '0.75rem' }}>/ 100</span>
      </div>
      <div className={styles.dimProgressBarTrack}>
        <div className={styles.dimProgressBarFill} style={{ width: `${rounded}%`, backgroundColor: color }} />
      </div>
    </div>
  )
}

function InteractiveEvidenceChip({
  id,
  item,
  variant,
  isExpanded,
  onToggle,
}: {
  id: string
  item: EvidenceItemModel
  variant: 'emerald' | 'crimson'
  isExpanded?: boolean
  onToggle: () => void
}) {
  const hasEvidence = Boolean(item.evidence)

  return (
    <div className={styles.chipWrapper}>
      <button
        type="button"
        className={`${styles.chipButton} ${styles[`chip-${variant}`]}`}
        onClick={hasEvidence ? onToggle : undefined}
        title={hasEvidence ? 'Clique para ver evidência no currículo' : undefined}
        aria-expanded={hasEvidence ? isExpanded : undefined}
        aria-controls={hasEvidence ? `${id}-evidence` : undefined}
      >
        <span className="material-symbols-outlined" style={{ fontSize: '14px' }}>
          {variant === 'emerald' ? 'check' : 'close'}
        </span>
        <span>{item.text}</span>
        {hasEvidence && (
          <span className="material-symbols-outlined" style={{ fontSize: '14px', marginLeft: '2px', opacity: 0.8 }}>
            {isExpanded ? 'expand_less' : 'info'}
          </span>
        )}
      </button>

      {hasEvidence && isExpanded && (
        <div id={`${id}-evidence`} className={styles.chipEvidenceBubble} role="region">
          <span className="font-label-code" style={{ fontSize: '0.6875rem', color: 'var(--color-text-muted)' }}>
            Evidência documental:
          </span>
          <p className="font-body-sm" style={{ color: 'var(--color-text-high)', fontStyle: 'italic', marginTop: '0.125rem' }}>
            “{item.evidence}”
          </p>
        </div>
      )}
    </div>
  )
}

function EvidenceRowItem({
  item,
  variant,
  icon,
}: {
  item: EvidenceItemModel
  variant: 'emerald' | 'crimson'
  icon: string
}) {
  return (
    <div className={`${styles.rowItem} ${styles[`row-${variant}`]}`}>
      <div className={`${styles.rowIconBox} ${styles[`icon-${variant}`]}`}>
        <span className="material-symbols-outlined" style={{ fontSize: '16px' }}>{icon}</span>
      </div>
      <div className={styles.rowContent}>
        <span className={styles.rowTitle}>{item.text}</span>
        <span className={styles.rowEvidence}>
          {item.evidence
            ? `Evidência: “${item.evidence}”`
            : 'Não identificado explicitamente no currículo'}
        </span>
      </div>
    </div>
  )
}
