import React from 'react'
import { Card } from '../../../shared/ui/Card/Card'
import { Spinner } from '../../../shared/ui/Spinner/Spinner'
import { Button } from '../../../shared/ui/Button/Button'
import { Alert } from '../../../shared/ui/Alert/Alert'
import styles from './ProcessingState.module.css'

export type ProcessingStep = 'uploading' | 'comparing' | 'done'

export interface ProcessingStateProps {
  step: ProcessingStep
  error?: string | null
  onRetry?: () => void
  onCancel?: () => void
}

export function ProcessingState({
  step,
  error,
  onRetry,
  onCancel,
}: ProcessingStateProps) {
  const isUploading = step === 'uploading'
  const isComparing = step === 'comparing'
  const isDone = step === 'done'

  return (
    <div className={styles.wrapper}>
      <div className="ambient-glow" style={{ width: '500px', height: '500px', background: 'rgba(99, 102, 241, 0.15)', top: '50%', left: '50%', transform: 'translate(-50%, -50%)' }} />

      <Card variant="glass" padding="xl" className={styles.processingCard}>
        {/* Animated Visual Loader */}
        <div className={styles.loaderArea}>
          {error ? (
            <div className={styles.errorIconCircle}>
              <span className="material-symbols-outlined" style={{ fontSize: '48px', color: 'var(--color-crimson)' }}>
                error
              </span>
            </div>
          ) : (
            <div className={styles.spinnerWrapper}>
              <Spinner size="lg" label="" />
            </div>
          )}
        </div>

        {/* Title & Description */}
        <div className={styles.headerInfo}>
          <h2 className="font-headline-md" style={{ color: 'var(--color-text-high)', marginBottom: '0.25rem' }}>
            {error
              ? 'Falha no processamento da análise'
              : isComparing
              ? 'Comparando currículo com a vaga…'
              : 'Enviando e estruturando documento…'}
          </h2>
          <p className="font-body-md" style={{ color: 'var(--color-text-muted)', maxWidth: '440px' }}>
            {error
              ? 'Ocorreu um erro durante o envio ou a comparação estruturada. Você pode tentar novamente mantendo os dados inseridos.'
              : 'Estamos processando os dados e cruzando as qualificações com os requisitos da oportunidade.'}
          </p>
        </div>

        {/* Real Observable Steps */}
        <div className={styles.stepsList}>
          {/* Step 1: Upload */}
          <div
            className={`${styles.stepItem} ${
              isDone || isComparing
                ? styles.stepCompleted
                : isUploading && !error
                ? styles.stepActive
                : ''
            }`}
          >
            <div className={styles.stepIconBox}>
              {isDone || isComparing ? (
                <span className="material-symbols-outlined" style={{ fontSize: '20px', color: 'var(--color-emerald)' }}>
                  check_circle
                </span>
              ) : isUploading && !error ? (
                <span className="material-symbols-outlined" style={{ fontSize: '20px', color: 'var(--color-primary)' }}>
                  sync
                </span>
              ) : (
                <span className="material-symbols-outlined" style={{ fontSize: '20px', color: 'var(--color-text-muted)' }}>
                  radio_button_unchecked
                </span>
              )}
            </div>
            <div className={styles.stepContent}>
              <div className={styles.stepTitleRow}>
                <span className={styles.stepTitle}>Leitura e envio do currículo</span>
                <span className={`${styles.stepStatusBadge} font-label-caps`}>
                  {isDone || isComparing
                    ? 'Concluído'
                    : isUploading && !error
                    ? 'Em andamento'
                    : 'Pendente'}
                </span>
              </div>
              <span className={styles.stepDesc}>Extração de texto em PDF/DOCX e persistência no banco</span>
            </div>
          </div>

          {/* Step 2: Compare */}
          <div
            className={`${styles.stepItem} ${
              isDone
                ? styles.stepCompleted
                : isComparing && !error
                ? styles.stepActive
                : ''
            }`}
          >
            <div className={styles.stepIconBox}>
              {isDone ? (
                <span className="material-symbols-outlined" style={{ fontSize: '20px', color: 'var(--color-emerald)' }}>
                  check_circle
                </span>
              ) : isComparing && !error ? (
                <span className="material-symbols-outlined" style={{ fontSize: '20px', color: 'var(--color-primary)' }}>
                  sync
                </span>
              ) : (
                <span className="material-symbols-outlined" style={{ fontSize: '20px', color: 'var(--color-text-muted)' }}>
                  radio_button_unchecked
                </span>
              )}
            </div>
            <div className={styles.stepContent}>
              <div className={styles.stepTitleRow}>
                <span className={styles.stepTitle}>Comparação e cálculo em 5 dimensões</span>
                <span className={`${styles.stepStatusBadge} font-label-caps`}>
                  {isDone
                    ? 'Concluído'
                    : isComparing && !error
                    ? 'Em andamento'
                    : 'Aguardando'}
                </span>
              </div>
              <span className={styles.stepDesc}>
                Avaliação semântica de skills, experiência, senioridade, requisitos e formação
              </span>
            </div>
          </div>
        </div>

        {/* Error Alert & Actions */}
        {error ? (
          <div className={styles.errorArea}>
            <Alert variant="error" style={{ marginBottom: 'var(--space-md)' }}>
              {error}
            </Alert>
            <div className={styles.actionButtons}>
              {onRetry && (
                <Button variant="primary" icon="refresh" onClick={onRetry}>
                  Tentar novamente
                </Button>
              )}
              {onCancel && (
                <Button variant="ghost" onClick={onCancel}>
                  Voltar ao formulário
                </Button>
              )}
            </div>
          </div>
        ) : (
          <div className={styles.trustFooter}>
            <span className="material-symbols-outlined" style={{ fontSize: '18px', color: 'var(--color-tertiary)' }}>
              verified_user
            </span>
            <span className="font-body-sm" style={{ color: 'var(--color-text-muted)' }}>
              O Resurva analisa estritamente fatos presentes no seu documento. Nenhuma experiência fictícia é gerada.
            </span>
          </div>
        )}
      </Card>
    </div>
  )
}
