import React from 'react'
import styles from './Card.module.css'

export interface CardProps extends React.HTMLAttributes<HTMLDivElement> {
  variant?: 'surface' | 'glass' | 'elevated' | 'glow'
  padding?: 'none' | 'sm' | 'md' | 'lg' | 'xl'
  children?: React.ReactNode
}

export function Card({
  variant = 'surface',
  padding = 'md',
  children,
  className = '',
  ...props
}: CardProps) {
  const rootClass = [
    styles.card,
    styles[variant],
    styles[`pad-${padding}`],
    className,
  ]
    .filter(Boolean)
    .join(' ')

  return (
    <div className={rootClass} {...props}>
      {children}
    </div>
  )
}
