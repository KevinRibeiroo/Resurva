import React from 'react'
import styles from './Spinner.module.css'

export interface SpinnerProps {
  size?: 'sm' | 'md' | 'lg'
  label?: string
  className?: string
}

export function Spinner({
  size = 'md',
  label = 'Carregando…',
  className = '',
}: SpinnerProps) {
  return (
    <div
      className={`${styles.wrapper} ${styles[size]} ${className}`}
      role="status"
      aria-label={label}
    >
      <div className={styles.spinner}>
        <div className={styles.pulseBg} />
        <svg className={styles.svg} viewBox="0 0 50 50">
          <circle
            className={styles.track}
            cx="25"
            cy="25"
            r="20"
            fill="none"
            strokeWidth="4"
          />
          <circle
            className={styles.indicator}
            cx="25"
            cy="25"
            r="20"
            fill="none"
            strokeWidth="4"
            strokeLinecap="round"
          />
        </svg>
      </div>
      {label && <span className={styles.label}>{label}</span>}
    </div>
  )
}
