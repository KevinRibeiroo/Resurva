import { ButtonLink } from '../../../shared/ui/Button/ButtonLink'
import React, { useEffect, useState } from 'react'
import { useParams, useLocation, useNavigate } from 'react-router-dom'
import { getAnalysis } from '../services/analysisService'
import { createOptimizationPlan } from '../../optimization/services/optimizationService'
import type { AnalysisResultModel } from '../models/AnalysisResultModel'
import { normalizeSafetyLevel } from '../../optimization/models/OptimizationPlanModel'
import { Card } from '../../../shared/ui/Card/Card'
import { Spinner } from '../../../shared/ui/Spinner/Spinner'
import { Alert } from '../../../shared/ui/Alert/Alert'
import styles from './AnalysisResultPage.module.css'
import { AnalysisResultView } from '../components/AnalysisResultView'

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
            <ButtonLink to="/app/analises/nova" variant="primary" icon="add">
                Fazer Nova Análise
              </ButtonLink>
          </div>
        </Card>
      </div>
    )
  }

  return <AnalysisResultView key={analysis.id} analysis={analysis} optimizing={optimizing} optError={optError} onOptimize={handleStartOptimization} />
}
