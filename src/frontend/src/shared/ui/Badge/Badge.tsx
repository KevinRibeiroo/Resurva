import React from 'react'
import styles from './Badge.module.css'

export interface BadgeProps extends React.HTMLAttributes<HTMLSpanElement> {
  variant?: 'emerald' | 'amber' | 'crimson' | 'primary' | 'secondary' | 'neutral'
  size?: 'sm' | 'md'
  icon?: string
  dot?: boolean
  caps?: boolean
  children?: React.ReactNode
}

export function Badge({
  variant = 'neutral',
  size = 'md',
  icon,
  dot = false,
  caps = false,
  children,
  className = '',
  ...props
}: BadgeProps) {
  const rootClass = [
    styles.badge,
    styles[variant],
    styles[size],
    caps ? styles.caps : '',
    className,
  ]
    .filter(Boolean)
    .join(' ')

  return (
    <span className={rootClass} {...props}>
      {dot && <span className={styles.dot} aria-hidden="true" />}
      {icon && (
        <span className={`material-symbols-outlined ${styles.badgeIcon}`} aria-hidden="true">
          {icon}
        </span>
      )}
      <span>{children}</span>
    </span>
  )
}
