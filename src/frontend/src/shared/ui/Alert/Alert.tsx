import React from 'react'
import styles from './Alert.module.css'

export interface AlertProps extends React.HTMLAttributes<HTMLDivElement> {
  variant?: 'error' | 'warning' | 'info' | 'success'
  title?: string
  action?: React.ReactNode
  children: React.ReactNode
}

export function Alert({
  variant = 'error',
  title,
  action,
  children,
  className = '',
  ...props
}: AlertProps) {
  const iconName = {
    error: 'error',
    warning: 'warning',
    info: 'info',
    success: 'check_circle',
  }[variant]

  return (
    <div
      role="alert"
      className={`${styles.alert} ${styles[variant]} ${className}`}
      {...props}
    >
      <span className={`material-symbols-outlined ${styles.icon}`} aria-hidden="true">
        {iconName}
      </span>
      <div className={styles.content}>
        {title && <strong className={styles.title}>{title}</strong>}
        <div className={styles.message}>{children}</div>
      </div>
      {action && <div className={styles.action}>{action}</div>}
    </div>
  )
}
