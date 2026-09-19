import { Link, type LinkProps } from 'react-router-dom'
import type { ButtonProps } from './Button'
import styles from './Button.module.css'

type ButtonLinkProps = LinkProps & Pick<ButtonProps, 'variant' | 'size' | 'icon'>

/** Navigation with the shared button appearance and native link semantics. */
export function ButtonLink({ variant = 'secondary', size = 'md', icon, className = '', children, ...props }: ButtonLinkProps) {
  return <Link className={`${styles.button} ${styles[variant]} ${styles[size]} ${className}`} {...props}>
    {icon && <span aria-hidden="true" className={`material-symbols-outlined ${styles.btnIcon}`}>{icon}</span>}
    <span>{children}</span>
  </Link>
}
