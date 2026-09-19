import React from 'react'
import styles from './Button.module.css'

export interface ButtonProps extends React.ButtonHTMLAttributes<HTMLButtonElement> {
  variant?: 'primary' | 'secondary' | 'ghost' | 'danger'
  size?: 'sm' | 'md' | 'lg'
  icon?: string
  loading?: boolean
  children?: React.ReactNode
}

export function Button({
  variant = 'secondary',
  size = 'md',
  icon,
  loading = false,
  disabled,
  children,
  className = '',
  ...props
}: ButtonProps) {
  const rootClass = [
    styles.button,
    styles[variant],
    styles[size],
    loading ? styles.loading : '',
    className,
  ]
    .filter(Boolean)
    .join(' ')

  return (
    <button
      type="button"
      className={rootClass}
      disabled={disabled || loading}
      aria-busy={loading}
      {...props}
    >
      {loading ? (
        <span aria-hidden="true" className={`material-symbols-outlined ${styles.spinnerIcon}`}>
          progress_activity
        </span>
      ) : icon ? (
        <span aria-hidden="true" className={`material-symbols-outlined ${styles.btnIcon}`}>
          {icon}
        </span>
      ) : null}
      {children && <span>{children}</span>}
    </button>
  )
}
