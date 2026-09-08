import React, { useRef, useState } from 'react'
import styles from './ResumeDropzone.module.css'

export const MAX_FILE_SIZE_BYTES = 10 * 1024 * 1024 // 10 MB

export interface ResumeDropzoneProps {
  selectedFile: File | null
  onFileSelect: (file: File | null) => void
  disabled?: boolean
  error?: string | null
}

export function ResumeDropzone({
  selectedFile,
  onFileSelect,
  disabled = false,
  error,
}: ResumeDropzoneProps) {
  const [isDragOver, setIsDragOver] = useState(false)
  const [validationError, setValidationError] = useState<string | null>(null)
  const fileInputRef = useRef<HTMLInputElement>(null)

  function validateAndSelect(file: File) {
    setValidationError(null)

    const validExtensions = ['.pdf', '.docx']
    const hasValidExt = validExtensions.some((ext) =>
      file.name.toLowerCase().endsWith(ext)
    )

    if (!hasValidExt) {
      setValidationError('Formato inválido. Envie apenas arquivos nos formatos PDF ou DOCX.')
      return
    }

    if (file.size > MAX_FILE_SIZE_BYTES) {
      setValidationError('O arquivo excede o limite máximo permitido de 10 MB.')
      return
    }

    if (file.size === 0) {
      setValidationError('O arquivo selecionado está vazio.')
      return
    }

    onFileSelect(file)
  }

  function handleFileChange(event: React.ChangeEvent<HTMLInputElement>) {
    const file = event.target.files?.[0]
    if (file) {
      validateAndSelect(file)
    }
  }

  function handleDragOver(event: React.DragEvent) {
    event.preventDefault()
    if (!disabled) setIsDragOver(true)
  }

  function handleDragLeave(event: React.DragEvent) {
    event.preventDefault()
    setIsDragOver(false)
  }

  function handleDrop(event: React.DragEvent) {
    event.preventDefault()
    setIsDragOver(false)
    if (disabled) return

    const file = event.dataTransfer.files?.[0]
    if (file) {
      validateAndSelect(file)
    }
  }

  function handleRemove() {
    onFileSelect(null)
    setValidationError(null)
    if (fileInputRef.current) {
      fileInputRef.current.value = ''
    }
  }

  const effectiveError = validationError || error
  const formattedSize = selectedFile
    ? (selectedFile.size / (1024 * 1024)).toFixed(1)
    : '0'

  return (
    <div className={styles.wrapper}>
      <input
        ref={fileInputRef}
        type="file"
        accept=".pdf,.docx"
        className={styles.hiddenInput}
        onChange={handleFileChange}
        disabled={disabled}
        id="resume-file-input"
        data-testid="resume-file-input"
      />

      {selectedFile ? (
        <div className={styles.selectedCard}>
          <div className={styles.fileIconBox}>
            <span className="material-symbols-outlined" style={{ fontSize: '28px', color: 'var(--color-primary)' }}>
              description
            </span>
          </div>

          <div className={styles.fileMeta}>
            <span className={styles.fileName} title={selectedFile.name}>
              {selectedFile.name}
            </span>
            <span className={`${styles.fileDetails} font-label-code`}>
              {formattedSize} MB • Selecionado (aguardando envio)
            </span>
          </div>

          <div className={styles.fileActions}>
            <button
              type="button"
              className={styles.actionBtn}
              onClick={() => fileInputRef.current?.click()}
              disabled={disabled}
              title="Trocar arquivo selecionado"
            >
              <span className="material-symbols-outlined" style={{ fontSize: '18px' }}>
                sync_alt
              </span>
              <span>Substituir</span>
            </button>
            <button
              type="button"
              className={`${styles.actionBtn} ${styles.actionBtnRemove}`}
              onClick={handleRemove}
              disabled={disabled}
              title="Remover arquivo"
            >
              <span className="material-symbols-outlined" style={{ fontSize: '18px' }}>
                close
              </span>
            </button>
          </div>
        </div>
      ) : (
        <div
          className={`${styles.dropzone} ${isDragOver ? styles.dragOver : ''} ${disabled ? styles.disabled : ''}`}
          onDragOver={handleDragOver}
          onDragLeave={handleDragLeave}
          onDrop={handleDrop}
          onClick={() => !disabled && fileInputRef.current?.click()}
          role="button"
          tabIndex={disabled ? -1 : 0}
          onKeyDown={(e) => {
            if (e.key === 'Enter' || e.key === ' ') {
              e.preventDefault()
              fileInputRef.current?.click()
            }
          }}
          aria-label="Área de upload do currículo em PDF ou DOCX"
        >
          <div className={styles.uploadIconCircle}>
            <span className="material-symbols-outlined" style={{ fontSize: '32px', color: 'var(--color-primary)' }}>
              cloud_upload
            </span>
          </div>

          <p className={styles.dropPrompt}>
            Arraste seu currículo aqui ou <span className={styles.browseLink}>clique para selecionar</span>
          </p>

          <p className={styles.formatNotice}>
            Formatos suportados: <strong style={{ color: 'var(--color-tertiary)' }}>PDF e DOCX</strong> (máx. 10 MB)
          </p>
        </div>
      )}

      {effectiveError && (
        <p className={styles.errorText} role="alert">
          <span className="material-symbols-outlined" style={{ fontSize: '16px' }}>error</span>
          <span>{effectiveError}</span>
        </p>
      )}
    </div>
  )
}
