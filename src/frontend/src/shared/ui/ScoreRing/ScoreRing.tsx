import React from 'react'
import styles from './ScoreRing.module.css'

export interface ScoreRingProps {
  score: number // 0 - 100
  size?: 'sm' | 'md' | 'lg'
  showMax?: boolean
  label?: string
  animated?: boolean
  useGradient?: boolean
  strokeWidth?: number
  className?: string
}

export function ScoreRing({
  score,
  size = 'md',
  showMax = true,
  label,
  animated = true,
  useGradient = false,
  strokeWidth,
  className = '',
}: ScoreRingProps) {
  const clampedScore = Math.max(0, Math.min(100, Math.round(score)))

  // Dimension settings
  const config = {
    sm: { dimension: 80, stroke: 8, radius: 32, fontSize: '1.25rem' },
    md: { dimension: 120, stroke: 10, radius: 48, fontSize: '2rem' },
    lg: { dimension: 160, stroke: 14, radius: 64, fontSize: '2.75rem' },
  }[size]

  const actualStroke = strokeWidth ?? config.stroke
  const radius = config.radius
  const circumference = 2 * Math.PI * radius
  const offset = circumference - (clampedScore / 100) * circumference

  // Semantic color tone
  let strokeColor = 'var(--color-crimson)'
  let colorTone = 'crimson'
  if (clampedScore >= 85) {
    strokeColor = 'var(--color-emerald)'
    colorTone = 'emerald'
  } else if (clampedScore >= 60) {
    strokeColor = 'var(--color-amber)'
    colorTone = 'amber'
  }

  const gradientId = `score-gradient-${size}-${clampedScore}`

  return (
    <div
      className={`${styles.container} ${styles[size]} ${className}`}
      role="progressbar"
      aria-valuenow={clampedScore}
      aria-valuemin={0}
      aria-valuemax={100}
      aria-label={label ?? `Score de ${clampedScore} em 100`}
    >
      <svg
        className={styles.svg}
        viewBox={`0 0 ${config.dimension} ${config.dimension}`}
        width={config.dimension}
        height={config.dimension}
      >
        {useGradient && (
          <defs>
            <linearGradient id={gradientId} x1="0%" y1="0%" x2="100%" y2="100%">
              <stop offset="0%" stopColor="#4cd7f6" />
              <stop offset="50%" stopColor="#8083ff" />
              <stop offset="100%" stopColor="#10b981" />
            </linearGradient>
          </defs>
        )}

        {/* Background track */}
        <circle
          className={styles.track}
          cx={config.dimension / 2}
          cy={config.dimension / 2}
          r={radius}
          strokeWidth={actualStroke}
        />

        {/* Active progress track */}
        <circle
          className={`${styles.indicator} ${animated ? styles.animated : ''}`}
          cx={config.dimension / 2}
          cy={config.dimension / 2}
          r={radius}
          strokeWidth={actualStroke}
          stroke={useGradient ? `url(#${gradientId})` : strokeColor}
          strokeDasharray={circumference}
          strokeDashoffset={offset}
          strokeLinecap="round"
        />
      </svg>

      <div className={styles.centerContent}>
        <span className={`${styles.scoreNumber} ${styles[colorTone]}`}>
          {clampedScore}
        </span>
        {showMax && (
          <span className={styles.maxScore}>
            /100
          </span>
        )}
      </div>
    </div>
  )
}
