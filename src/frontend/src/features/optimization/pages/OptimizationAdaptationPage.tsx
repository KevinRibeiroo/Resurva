import React, { useEffect, useLayoutEffect, useRef, useState } from 'react'
import { useParams, useLocation, useNavigate, Link } from 'react-router-dom'
import { getOptimizationPlan, applyOptimization, exportAdaptedResume } from '../services/optimizationService'
import {
  normalizeSafetyLevel,
  type OptimizationPlanModel,
  type OptimizationDecisionModel,
  type OptimizationResultModel,
  type OptimizationSuggestionModel,
} from '../models/OptimizationPlanModel'
import { Card } from '../../../shared/ui/Card/Card'
import { Button } from '../../../shared/ui/Button/Button'
import { Badge } from '../../../shared/ui/Badge/Badge'
import { Alert } from '../../../shared/ui/Alert/Alert'
import { Spinner } from '../../../shared/ui/Spinner/Spinner'
import { ApiError } from '../../../shared/api/httpClient'
import styles from './OptimizationAdaptationPage.module.css'
import { LayoutExportPanel } from '../components/LayoutExportPanel'

export function OptimizationAdaptationPage() {
  const { optimizationId } = useParams<{ optimizationId: string }>()
  const location = useLocation()
  const navigate = useNavigate()

  // Navigation state passed from previous step
  const navState = location.state as {
    plan?: OptimizationPlanModel
    decisions?: OptimizationDecisionModel[]
  } | undefined

  const [plan, setPlan] = useState<OptimizationPlanModel | null>(navState?.plan || null)
  const [loading, setLoading] = useState<boolean>(!plan)
  const [error, setError] = useState<string | null>(null)
  const [isConflict, setIsConflict] = useState<boolean>(false)

  // Decisions map: suggestionId -> OptimizationDecisionModel
  const [decisions, setDecisions] = useState<Record<string, OptimizationDecisionModel>>(() => {
    const initial: Record<string, OptimizationDecisionModel> = {}

    // If decisions were passed from confirmation step, use them
    if (navState?.decisions) {
      for (const d of navState.decisions) {
        initial[d.suggestionId] = d
      }
    }

    return initial
  })

  // Applied result state
  const [result, setResult] = useState<OptimizationResultModel | null>(null)

  // Operations loading states
  const [applying, setApplying] = useState<boolean>(false)
  const [exportingPdf, setExportingPdf] = useState<boolean>(false)
  const [exportingDocx, setExportingDocx] = useState<boolean>(false)
  const [copySuccess, setCopySuccess] = useState<boolean>(false)
  const dockRef = useRef<HTMLDivElement>(null)
  const layoutExportRef = useRef<HTMLElement>(null)
  const [dockHeight, setDockHeight] = useState(0)

  // Fetch plan if accessed directly by URL
  useEffect(() => {
    if (!optimizationId) return
    if (plan && plan.id === optimizationId) return

    let isMounted = true
    setLoading(true)
    setError(null)
    setIsConflict(false)

    getOptimizationPlan(optimizationId)
      .then((data) => {
        if (isMounted) {
          setPlan(data)
          setLoading(false)
        }
      })
      .catch((err) => {
        if (isMounted) {
          setError(err instanceof Error ? err.message : 'Não foi possível carregar o plano de adaptação.')
          setLoading(false)
        }
      })

    return () => {
      isMounted = false
    }
  }, [optimizationId])

  // Initialize decisions if not passed via location state
  useEffect(() => {
    if (!plan) return
    if (Object.keys(decisions).length > 0) return

    const initial: Record<string, OptimizationDecisionModel> = {}
    if (plan.status === 'Applied' && plan.appliedDecisions && plan.appliedDecisions.length > 0) {
      for (const d of plan.appliedDecisions) {
        initial[d.suggestionId] = d
      }
    } else {
      for (const s of plan.suggestions) {
        const level = normalizeSafetyLevel(s.level)
        if (level === 'Safe') {
          initial[s.id] = { suggestionId: s.id, accepted: true, confirmed: false }
        } else {
          // NeedsConfirmation and Forbidden start unaccepted
          initial[s.id] = { suggestionId: s.id, accepted: false, confirmed: false }
        }
      }
    }
    setDecisions(initial)
  }, [plan])

  // If already applied (loaded from GET or freshly applied)
  const isAlreadyApplied = plan?.status === 'Applied' && Boolean(plan?.adaptedText)
  const displayedAdaptedText = result?.adaptedText || (isAlreadyApplied ? plan?.adaptedText : null)
  const displayedAppliedChanges = result?.appliedChanges || (isAlreadyApplied ? plan?.appliedChanges : null) || []

  // Reserve the actual wrapped dock height so it cannot hide export controls on narrow screens.
  useLayoutEffect(() => {
    const dock = dockRef.current
    if (!dock) return
    const measure = () => setDockHeight(dock.getBoundingClientRect().height)
    measure()
    const observer = typeof ResizeObserver !== 'undefined' ? new ResizeObserver(measure) : null
    observer?.observe(dock)
    window.addEventListener('resize', measure)
    return () => { observer?.disconnect(); window.removeEventListener('resize', measure) }
  }, [loading, plan?.id, Boolean(displayedAdaptedText)])

  function toggleSuggestionAccepted(id: string, accepted: boolean) {
    if (isAlreadyApplied) return
    setDecisions((prev) => ({
      ...prev,
      [id]: {
        ...prev[id],
        suggestionId: id,
        accepted,
      },
    }))
  }

  async function handleApplyChanges() {
    if (!plan) return
    setApplying(true)
    setError(null)
    setIsConflict(false)

    try {
      const decisionList = Object.values(decisions)
      const res = await applyOptimization(plan.id, plan.version, decisionList)
      setResult(res)
      // Update plan status locally
      setPlan((prev) => (prev ? { ...prev, status: 'Applied', adaptedText: res.adaptedText, version: prev.version + 1 } : null))
    } catch (err) {
      if (err instanceof ApiError && err.isConflict) {
        setIsConflict(true)
        setError('O plano foi atualizado em outra sessão. Recarregue para obter a versão mais recente.')
      } else {
        setError(err instanceof Error ? err.message : 'Falha ao aplicar alterações de adaptação.')
      }
    } finally {
      setApplying(false)
    }
  }

  async function handleReloadPlan() {
    if (!optimizationId) return
    setLoading(true)
    setError(null)
    setIsConflict(false)
    try {
      const freshPlan = await getOptimizationPlan(optimizationId)
      setPlan(freshPlan)
      setResult(null)
      // Reset decisions
      const initial: Record<string, OptimizationDecisionModel> = {}
      for (const s of freshPlan.suggestions) {
        const level = normalizeSafetyLevel(s.level)
        initial[s.id] = { suggestionId: s.id, accepted: level === 'Safe', confirmed: false }
      }
      setDecisions(initial)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Não foi possível recarregar o plano.')
    } finally {
      setLoading(false)
    }
  }

  async function handleCopyText() {
    if (!displayedAdaptedText) return
    try {
      await navigator.clipboard.writeText(displayedAdaptedText)
      setCopySuccess(true)
      setTimeout(() => setCopySuccess(false), 2500)
    } catch {
      setError('Não foi possível copiar o texto automaticamente.')
    }
  }

  async function handleDownload(format: 'pdf' | 'docx') {
    if (!plan) return
    if (format === 'pdf') setExportingPdf(true)
    else setExportingDocx(true)
    setError(null)

    try {
      await exportAdaptedResume(plan.id, format)
    } catch (err) {
      setError(err instanceof Error ? err.message : `Falha ao baixar o currículo em ${format.toUpperCase()}.`)
    } finally {
      if (format === 'pdf') setExportingPdf(false)
      else setExportingDocx(false)
    }
  }

  if (loading) {
    return (
      <div className={styles.centerContainer}>
        <Spinner size="lg" label="Carregando versão de adaptação…" />
      </div>
    )
  }

  if (error && !plan) {
    return (
      <div className="app-container" style={{ paddingTop: 'var(--space-2xl)' }}>
        <Card variant="glass" padding="xl" style={{ maxWidth: '600px', margin: '0 auto', textAlign: 'center' }}>
          <Alert variant="error" title="Plano não encontrado">
            {error}
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

  return (
    <div className="app-container" style={{ paddingTop: 'var(--space-xl)', paddingBottom: `calc(var(--space-xl) + ${dockHeight}px)` }}>
      {/* Top Header */}
      <div className={styles.topHeader}>
        <div className={styles.titleCol}>
          <div className={styles.metaRow}>
            <span className="font-label-caps" style={{ color: 'var(--color-secondary)' }}>
              Ajuste de Redação e Relevância
            </span>
            <span style={{ color: 'var(--color-text-dim)' }}>•</span>
            <span className="font-label-code" style={{ color: 'var(--color-text-muted)' }}>
              Versão: #{plan!.version}
            </span>
            {plan!.status === 'Applied' && (
              <Badge variant="emerald" size="sm" icon="check_circle">
                Status: Aplicado
              </Badge>
            )}
          </div>
          <h1 className="font-headline-lg" style={{ color: 'var(--color-text-high)', margin: 'var(--space-2xs) 0' }}>
            Adaptar Currículo para a Vaga
          </h1>
          <p className="font-body-md" style={{ color: 'var(--color-text-muted)' }}>
            Revise as melhorias de redação e o realce de competências antes de salvar ou exportar seu currículo oficial.
          </p>
        </div>

        <div className={styles.transparencyPill}>
          <span className="material-symbols-outlined" style={{ fontSize: '20px', color: 'var(--color-tertiary)' }}>
            verified_user
          </span>
          <div>
            <strong className="font-body-sm" style={{ color: 'var(--color-text-high)', display: 'block', fontSize: '0.75rem' }}>
              Baseado no currículo e nas suas confirmações
            </strong>
            <span className="font-body-sm" style={{ color: 'var(--color-text-dim)', fontSize: '0.6875rem' }}>
              Nenhum dado inserido sem seu aval prévio
            </span>
          </div>
        </div>
      </div>

      {/* Ethical Commitment Notice */}
      <Card variant="surface" padding="md" className={styles.noticeCard}>
        <span className="material-symbols-outlined" style={{ color: 'var(--color-tertiary)', fontSize: '20px', flexShrink: 0 }}>
          info
        </span>
        <p className="font-body-sm" style={{ color: 'var(--color-text-muted)' }}>
          <strong style={{ color: 'var(--color-text-high)' }}>Compromisso com a verdade:</strong> O ResumeMatcher não adiciona experiências, títulos ou competências que você não tenha declarado ou previamente confirmado. O foco é refinamento gramatical e alinhamento com a linguagem valorizada na oportunidade.
        </p>
      </Card>

      {/* Conflict or Error Alert */}
      {error && (
        <Alert
          variant={isConflict ? 'warning' : 'error'}
          title={isConflict ? 'Conflito de Versão (409)' : 'Erro'}
          action={
            isConflict ? (
              <Button variant="primary" size="sm" icon="refresh" onClick={handleReloadPlan}>
                Recarregar versão mais recente
              </Button>
            ) : undefined
          }
          style={{ marginBottom: 'var(--space-lg)' }}
        >
          {error}
        </Alert>
      )}

      {/* Side-by-Side Comparative Layout */}
      <div className={styles.comparativeGrid}>
        {/* Left Column: Original Resume (Read-Only) */}
        <Card variant="surface" padding="lg" className={styles.columnCard}>
          <div className={styles.columnHeader}>
            <div className={styles.columnTitleWrap}>
              <span className="material-symbols-outlined" style={{ color: 'var(--color-text-muted)', fontSize: '22px' }}>
                description
              </span>
              <h2 className="font-headline-sm" style={{ color: 'var(--color-text-high)' }}>
                Currículo Original
              </h2>
            </div>
            <Badge variant="neutral" size="sm">
              Somente Leitura
            </Badge>
          </div>

          <div className={styles.columnBody}>
            <div className={styles.documentViewer}>
              <pre className={styles.rawTextDisplay}>
                {plan!.originalText}
              </pre>
            </div>
            <div className={styles.columnFooterNotice}>
              <span className="material-symbols-outlined" style={{ fontSize: '16px' }}>
                lock
              </span>
              <span>Texto original preservado intacto como referência documental.</span>
            </div>
          </div>
        </Card>

        {/* Right Column: Suggested vs Applied Version */}
        <Card variant="surface" padding="lg" className={styles.columnCard}>
          <div className={styles.columnHeader}>
            <div className={styles.columnTitleWrap}>
              <span className="material-symbols-outlined" style={{ color: 'var(--color-primary)', fontSize: '22px' }}>
                {displayedAdaptedText ? 'verified' : 'magic_button'}
              </span>
              <h2 className="font-headline-sm" style={{ color: 'var(--color-text-high)' }}>
                {displayedAdaptedText ? 'Versão Adaptada Oficial' : 'Sugestões de Adaptação'}
              </h2>
            </div>
            <Badge variant={displayedAdaptedText ? 'emerald' : 'primary'} size="sm">
              {displayedAdaptedText ? 'Pronta para Download' : 'Revisão de Sugestões'}
            </Badge>
          </div>

          <div className={styles.columnBody}>
            {displayedAdaptedText ? (
              /* Display server-returned adapted text */
              <div className={styles.appliedView}>
                <div className={styles.appliedSuccessBanner}>
                  <span className="material-symbols-outlined" style={{ color: 'var(--color-emerald)', fontSize: '22px' }}>
                    check_circle
                  </span>
                  <div>
                    <strong className="font-body-sm" style={{ color: 'var(--color-text-high)', display: 'block' }}>
                      Adaptação Aplicada com Sucesso no Servidor
                    </strong>
                    <span className="font-body-sm" style={{ color: 'var(--color-text-muted)', fontSize: '0.75rem' }}>
                      O texto abaixo é a versão oficial gerada pelo backend pronta para cópia ou exportação.
                    </span>
                  </div>
                </div>

                <textarea
                  readOnly
                  rows={14}
                  value={displayedAdaptedText}
                  className={styles.adaptedTextarea}
                  aria-label="Texto do currículo adaptado"
                  id="adapted-text-output"
                  data-testid="adapted-text-output"
                />

                {displayedAppliedChanges.length > 0 && (
                  <div className={styles.changesSummaryBlock}>
                    <span className="font-label-caps" style={{ color: 'var(--color-text-muted)', display: 'block', marginBottom: 'var(--space-xs)' }}>
                      Alterações incorporadas nesta versão ({displayedAppliedChanges.length}):
                    </span>
                    <ul className={styles.appliedChangesList}>
                      {displayedAppliedChanges.map((change) => {
                        const level = normalizeSafetyLevel(change.level)
                        const isUserDeclared = change.informationOrigin === 'DeclaradaPeloUsuario'
                        return (
                          <li key={change.suggestionId} className={styles.appliedChangeItem}>
                            <div className={styles.changeHeader}>
                              <Badge variant={isUserDeclared ? 'primary' : 'emerald'} size="sm">
                                {isUserDeclared ? 'Declarado pelo Usuário' : 'Baseado no Currículo'}
                              </Badge>
                              <span className="font-label-code" style={{ color: 'var(--color-text-dim)', fontSize: '0.6875rem' }}>
                                [{level}]
                              </span>
                            </div>
                            <p className="font-body-sm" style={{ color: 'var(--color-text-high)', margin: '0.25rem 0' }}>
                              {change.proposedText}
                            </p>
                            {change.userDeclaration && (
                              <p className="font-body-sm" style={{ color: 'var(--color-tertiary)', fontSize: '0.75rem', fontStyle: 'italic' }}>
                                Declaração informada: “{change.userDeclaration}”
                              </p>
                            )}
                            <span className="font-body-sm" style={{ color: 'var(--color-text-muted)', fontSize: '0.75rem' }}>
                              {change.reason}
                            </span>
                          </li>
                        )
                      })}
                    </ul>
                  </div>
                )}
              </div>
            ) : (
              /* Before application: show cards with original/proposed, reason, and toggles */
              <div className={styles.suggestionsList}>
                {plan!.suggestions.map((suggestion, idx) => {
                  const level = normalizeSafetyLevel(suggestion.level)
                  const isForbidden = level === 'Forbidden'
                  const decision = decisions[suggestion.id]
                  const isAccepted = decision?.accepted ?? (level === 'Safe')

                  return (
                    <div
                      key={suggestion.id}
                      className={`${styles.suggestionCard} ${
                        isForbidden
                          ? styles.suggestionForbidden
                          : isAccepted
                          ? styles.suggestionAccepted
                          : styles.suggestionRejected
                      }`}
                    >
                      <div className={styles.cardTopRow}>
                        <div className={styles.cardHeaderLeft}>
                          <span className={`${styles.cardIndex} font-label-code`}>
                            #{idx + 1}
                          </span>
                          <Badge
                            variant={
                              isForbidden ? 'crimson' : level === 'Safe' ? 'emerald' : 'amber'
                            }
                            size="sm"
                            caps
                          >
                            {isForbidden
                              ? 'Bloqueada (Invenção)'
                              : level === 'Safe'
                              ? 'Segura (Evidenciada)'
                              : 'Requer Validação'}
                          </Badge>
                        </div>

                        {/* Decision Toggle */}
                        {isForbidden ? (
                          <Badge variant="crimson" size="sm">
                            Bloqueado
                          </Badge>
                        ) : (
                          <div className={styles.toggleButtonsGroup}>
                            <button
                              type="button"
                              className={`${styles.toggleBtn} ${
                                isAccepted ? styles.toggleBtnActiveAccept : ''
                              }`}
                              onClick={() => toggleSuggestionAccepted(suggestion.id, true)}
                              disabled={applying || isAlreadyApplied}
                            >
                              Aceitar
                            </button>
                            <button
                              type="button"
                              className={`${styles.toggleBtn} ${
                                !isAccepted ? styles.toggleBtnActiveReject : ''
                              }`}
                              onClick={() => toggleSuggestionAccepted(suggestion.id, false)}
                              disabled={applying || isAlreadyApplied}
                            >
                              Rejeitar
                            </button>
                          </div>
                        )}
                      </div>

                      {/* Snippet comparison */}
                      {suggestion.originalText && (
                        <div className={styles.snippetOriginal}>
                          <span className={styles.snippetLabel}>Trecho original:</span>
                          <blockquote className={styles.quoteBlock}>
                            “{suggestion.originalText}”
                          </blockquote>
                        </div>
                      )}

                      <div className={styles.snippetProposed}>
                        <span className={styles.snippetLabel}>Texto proposto:</span>
                        <blockquote className={styles.quoteBlock}>
                          “{suggestion.proposedText}”
                        </blockquote>
                      </div>

                      {suggestion.evidence && (
                        <div className={styles.evidenceSnippet}>
                          <span className="material-symbols-outlined" style={{ fontSize: '14px', color: 'var(--color-tertiary)' }}>
                            check
                          </span>
                          <span className="font-body-sm" style={{ color: 'var(--color-text-muted)', fontSize: '0.75rem' }}>
                            Evidência: “{suggestion.evidence}”
                          </span>
                        </div>
                      )}

                      <p className="font-body-sm" style={{ color: 'var(--color-text-muted)', marginTop: '0.25rem' }}>
                        {suggestion.reason}
                      </p>

                      {isForbidden && (
                        <div className={styles.forbiddenWarning}>
                          <span className="material-symbols-outlined" style={{ fontSize: '16px' }}>warning</span>
                          <span>Esta sugestão foi bloqueada pelas regras de segurança ética do backend para impedir alegações inventadas.</span>
                        </div>
                      )}
                    </div>
                  )
                })}
              </div>
            )}
          </div>
        </Card>
      </div>

      {/* Floating Bottom Master Action Bar */}
      {import.meta.env.DEV && displayedAdaptedText && plan && (
        <section ref={layoutExportRef} tabIndex={-1} aria-label="Exportar DOCX com layout original">
          <LayoutExportPanel key={`${plan.id}:${plan.version}`} optimizationId={plan.id} resumeId={plan.resumeId} />
        </section>
      )}

      <div className={styles.masterDock} ref={dockRef}>
        <div className={styles.dockInfoCol}>
          <span className="material-symbols-outlined" style={{ color: 'var(--color-tertiary)', fontSize: '20px' }}>
            {displayedAdaptedText ? 'verified' : 'tune'}
          </span>
          <span className="font-body-sm" style={{ color: 'var(--color-text-high)', fontSize: '0.875rem' }}>
            {displayedAdaptedText
              ? import.meta.env.DEV
                ? 'Use DOCX — layout original para manter a formatação do arquivo enviado.'
                : 'Currículo adaptado pronto para cópia ou download direto.'
              : 'Revise as decisões acima e aplique para gerar a versão adaptada oficial.'}
          </span>
        </div>

        <div className={styles.dockButtonsCol}>
          {displayedAdaptedText ? (
            <>
              <Link to="/app/analises/nova">
                <Button variant="ghost" icon="add" title="Iniciar uma nova análise de currículo">
                  Nova Análise
                </Button>
              </Link>

              <Button
                variant="secondary"
                icon={copySuccess ? 'check' : 'content_copy'}
                onClick={handleCopyText}
                title="Copiar texto adaptado para a área de transferência"
                id="copy-text-btn"
              >
                {copySuccess ? 'Copiado!' : 'Copiar Texto Completo'}
              </Button>

              <Button
                variant="secondary"
                icon="article"
                onClick={() => handleDownload('docx')}
                loading={exportingDocx}
                disabled={exportingPdf}
                title="Baixar DOCX com o modelo padrão, sem preservar a formatação original"
                id="download-docx-template-btn"
              >
                DOCX — modelo padrão
              </Button>

              {import.meta.env.DEV && (
                <Button
                  variant="primary"
                  icon="description"
                  aria-label="DOCX — layout original"
                  onClick={() => {
                    layoutExportRef.current?.focus({ preventScroll: true })
                    layoutExportRef.current?.scrollIntoView({ block: 'start', behavior: 'instant' })
                  }}
                  title="Revisar os destinos das alterações e baixar usando o DOCX original"
                  id="download-docx-btn"
                >
                  DOCX — layout original
                </Button>
              )}

              <Button
                variant={import.meta.env.DEV ? 'secondary' : 'primary'}
                icon="picture_as_pdf"
                onClick={() => handleDownload('pdf')}
                loading={exportingPdf}
                disabled={exportingDocx}
                title="Baixar currículo formatado em PDF"
                id="download-pdf-btn"
              >
                PDF — modelo padrão
              </Button>
            </>
          ) : (
            <>
              <Link to={`/app/adaptacoes/${plan!.id}/confirmacoes`}>
                <Button variant="ghost" icon="checklist">
                  Revisar confirmações éticas
                </Button>
              </Link>
              <Button
                variant="primary"
                icon="check_circle"
                onClick={handleApplyChanges}
                loading={applying}
                id="apply-changes-btn"
              >
                Aplicar alterações aprovadas
              </Button>
            </>
          )}
        </div>
      </div>
    </div>
  )
}
