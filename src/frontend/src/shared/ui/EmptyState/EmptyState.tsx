import React from 'react'
import styles from './EmptyState.module.css'

export interface EmptyStateProps {
  icon?: string
  title: string
  description?: string
  action?: React.ReactNode
  className?: string
}

export function EmptyState({
  icon = 'inbox',
  title,
  description,
  action,
  className = '',
}: EmptyStateProps) {
  return (
    <div className={`${styles.container} ${className}`}>
      <div className={styles.iconCircle}>
        <span className={`material-symbols-outlined ${styles.icon}`}>
          {icon}
        </span>
      </div>
      <h3 className={styles.title}>{title}</h3>
      {description && <p className={styles.description}>{description}</p>}
      {action && <div className={styles.action}>{action}</div>}
    </div>
  )
}
