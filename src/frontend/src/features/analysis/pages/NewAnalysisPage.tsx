import React, { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { Card } from '../../../shared/ui/Card/Card'
import { Button } from '../../../shared/ui/Button/Button'
import { Alert } from '../../../shared/ui/Alert/Alert'
import { ResumeDropzone } from '../components/ResumeDropzone'
import { JobDescriptionInput, MAX_JOB_DESCRIPTION_LENGTH } from '../components/JobDescriptionInput'
import { ProcessingState, type ProcessingStep } from '../components/ProcessingState'
import { uploadResume, compareResume } from '../services/analysisService'
import styles from './NewAnalysisPage.module.css'

export function NewAnalysisPage() {
  const navigate = useNavigate()

  const [selectedFile, setSelectedFile] = useState<File | null>(null)
  const [jobDescription, setJobDescription] = useState<string>('')

  // Validation errors
  const [fileError, setFileError] = useState<string | null>(null)
  const [jobError, setJobError] = useState<string | null>(null)
  const [generalError, setGeneralError] = useState<string | null>(null)

  // Execution state
  const [isProcessing, setIsProcessing] = useState<boolean>(false)
  const [currentStep, setCurrentStep] = useState<ProcessingStep>('uploading')
  const [uploadedResumeId, setUploadedResumeId] = useState<string | null>(null)
  const [lastUploadedFile, setLastUploadedFile] = useState<File | null>(null)

  function handleFileSelect(file: File | null) {
    setSelectedFile(file)
    setFileError(null)
    setGeneralError(null)
    // If the file changed, invalidate previously cached uploadedResumeId
    if (file !== lastUploadedFile) {
      setUploadedResumeId(null)
    }
  }

  function handleJobChange(text: string) {
    setJobDescription(text)
    setJobError(null)
    setGeneralError(null)
  }

  async function executeAnalysis() {
    // 1. Validation
    let hasError = false
    setFileError(null)
    setJobError(null)
    setGeneralError(null)

    if (!selectedFile) {
      setFileError('Por favor, selecione ou arraste um arquivo de currículo em PDF ou DOCX.')
      hasError = true
    }

    const trimmedJob = jobDescription.trim()
    if (!trimmedJob) {
      setJobError('Por favor, cole a descrição da vaga com os requisitos e qualificações.')
      hasError = true
    } else if (trimmedJob.length > MAX_JOB_DESCRIPTION_LENGTH) {
      setJobError(`A descrição da vaga não pode exceder ${MAX_JOB_DESCRIPTION_LENGTH.toLocaleString('pt-BR')} caracteres.`)
      hasError = true
    }

    if (hasError) return

    setIsProcessing(true)

    try {
      // 2. Upload (or reuse resumeId if file hasn't changed)
      let resumeId = uploadedResumeId
      if (!resumeId || selectedFile !== lastUploadedFile) {
        setCurrentStep('uploading')
        resumeId = await uploadResume(selectedFile!)
        setUploadedResumeId(resumeId)
        setLastUploadedFile(selectedFile)
      }

      // 3. Compare with Job Description
      setCurrentStep('comparing')
      const result = await compareResume(resumeId, trimmedJob)

      setCurrentStep('done')
      // Navigate to results page with result state and ID
      navigate(`/app/analises/${result.id}`, { state: { analysisResult: result } })
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Falha inesperada durante a análise.'
      setGeneralError(message)
    }
  }

  function handleRetry() {
    setGeneralError(null)
    executeAnalysis()
  }

  function handleCancelProcessing() {
    setIsProcessing(false)
    setGeneralError(null)
  }

  if (isProcessing) {
    return (
      <div className="app-container" style={{ paddingTop: 'var(--space-xl)', paddingBottom: 'var(--space-3xl)' }}>
        <ProcessingState
          step={currentStep}
          error={generalError}
          onRetry={handleRetry}
          onCancel={handleCancelProcessing}
        />
      </div>
    )
  }

  return (
    <div className="app-container" style={{ paddingTop: 'var(--space-xl)', paddingBottom: 'var(--space-3xl)' }}>
      {/* Header */}
      <header className={styles.pageHeader}>
        <h1 className="font-headline-lg" style={{ color: 'var(--color-text-high)', marginBottom: 'var(--space-xs)' }}>
          Nova Análise de Compatibilidade
        </h1>
        <p className="font-body-md" style={{ color: 'var(--color-text-muted)', maxWidth: '640px' }}>
          Compare seu currículo com os requisitos da vaga para descobrir pontos fortes, lacunas e oportunidades de melhoria factual.
        </p>
      </header>

      {generalError && (
        <div style={{ maxWidth: '1080px', margin: '0 auto var(--space-lg) auto' }}>
          <Alert variant="error" title="Não foi possível concluir a análise">
            {generalError}
          </Alert>
        </div>
      )}

      {/* Two Pillars Grid */}
      <div className={styles.pillarsGrid}>
        {/* Pillar 1: Currículo */}
        <Card variant="surface" padding="lg" className={styles.pillarCard}>
          <div className={styles.pillarHeader}>
            <div className={styles.pillarTitleGroup}>
              <span className={styles.pillarBadge}>1</span>
              <h2 className="font-headline-sm" style={{ color: 'var(--color-text-high)' }}>
                Seu Currículo
              </h2>
            </div>
            <span className="font-label-caps" style={{ color: 'var(--color-text-dim)' }}>
              Entrada de Perfil
            </span>
          </div>

          <div className={styles.pillarBody}>
            <ResumeDropzone
              selectedFile={selectedFile}
              onFileSelect={handleFileSelect}
              disabled={isProcessing}
              error={fileError}
            />
          </div>
        </Card>

        {/* Pillar 2: Descrição da Vaga */}
        <Card variant="surface" padding="lg" className={styles.pillarCard}>
          <div className={styles.pillarHeader}>
            <div className={styles.pillarTitleGroup}>
              <span className={styles.pillarBadge}>2</span>
              <h2 className="font-headline-sm" style={{ color: 'var(--color-text-high)' }}>
                Descrição da Vaga
              </h2>
            </div>
            <span className="font-label-caps" style={{ color: 'var(--color-text-dim)' }}>
              Requisitos &amp; Escopo
            </span>
          </div>

          <div className={styles.pillarBody}>
            <JobDescriptionInput
              value={jobDescription}
              onChange={handleJobChange}
              disabled={isProcessing}
              error={jobError}
            />
          </div>
        </Card>
      </div>

      {/* Central Action Trigger */}
      <div className={styles.actionSection}>
        <Button
          variant="primary"
          size="lg"
          icon="description"
          onClick={executeAnalysis}
          disabled={isProcessing}
          className={styles.submitBtn}
          id="btn-analyze"
        >
          Comparar currículo com vaga
        </Button>

        <div className={styles.securityTrustRow}>
          <span className="material-symbols-outlined" style={{ fontSize: '18px', color: 'var(--color-tertiary)' }}>
            verified_user
          </span>
          <span className="font-body-sm" style={{ color: 'var(--color-text-muted)', fontSize: '0.8125rem' }}>
            Privacidade respeitada. Seus dados são processados com isolamento seguro na sua conta.
          </span>
        </div>
      </div>
    </div>
  )
}
