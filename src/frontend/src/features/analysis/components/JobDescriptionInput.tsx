import React, { useRef } from 'react'
import styles from './JobDescriptionInput.module.css'

export const MAX_JOB_DESCRIPTION_LENGTH = 75000

export interface JobDescriptionInputProps {
  value: string
  onChange: (value: string) => void
  disabled?: boolean
  error?: string | null
}

export function JobDescriptionInput({
  value,
  onChange,
  disabled = false,
  error,
}: JobDescriptionInputProps) {
  const textareaRef = useRef<HTMLTextAreaElement>(null)
  const length = value.length
  const isOverLimit = length > MAX_JOB_DESCRIPTION_LENGTH

  async function handlePasteClick() {
    try {
      const text = await navigator.clipboard.readText()
      if (text) {
        onChange(text)
      }
      textareaRef.current?.focus()
    } catch {
      // If clipboard permission is rejected, focus textarea so the user can use Ctrl+V
      textareaRef.current?.focus()
    }
  }

  return (
    <div className={styles.wrapper}>
      <div className={styles.metaRow}>
        <span className={`${styles.counter} font-label-code ${isOverLimit ? styles.overLimit : ''}`}>
          {length.toLocaleString('pt-BR')} / {MAX_JOB_DESCRIPTION_LENGTH.toLocaleString('pt-BR')} caracteres
        </span>
      </div>

      <div className={styles.textareaContainer}>
        <textarea
          ref={textareaRef}
          className={`${styles.textarea} ${isOverLimit ? styles.textareaError : ''}`}
          rows={10}
          value={value}
          onChange={(e) => onChange(e.target.value)}
          placeholder="Cole aqui a descrição completa da vaga (requisitos, qualificações, responsabilidades e diferenciais)..."
          disabled={disabled}
          id="job-description-input"
          data-testid="job-description-input"
        />

        <div className={styles.floatingActions}>
          <button
            type="button"
            className={styles.pasteButton}
            onClick={handlePasteClick}
            disabled={disabled}
            title="Colar texto da área de transferência"
          >
            <span className="material-symbols-outlined" style={{ fontSize: '16px' }}>
              content_paste
            </span>
            <span>Colar</span>
          </button>
        </div>
      </div>

      {(error || isOverLimit) && (
        <p className={styles.errorText} role="alert">
          <span className="material-symbols-outlined" style={{ fontSize: '16px' }}>error</span>
          <span>
            {error ||
              `A descrição excede o limite máximo de ${MAX_JOB_DESCRIPTION_LENGTH.toLocaleString('pt-BR')} caracteres.`}
          </span>
        </p>
      )}
    </div>
  )
}
