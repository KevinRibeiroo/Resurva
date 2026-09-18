import React from 'react'
import { Outlet, Link, useLocation } from 'react-router-dom'
import { useAuth } from '../providers/AuthProvider'
import { Button } from '../../shared/ui/Button/Button'
import styles from './AppLayout.module.css'

export function AppLayout() {
  const { user, signOut, busy } = useAuth()
  const location = useLocation()

  const isNovaAnalise = location.pathname.includes('/analises/nova')

  return (
    <div className={styles.appShell}>
      <header className={styles.topBar}>
        <div className={styles.barInner}>
          <div className={styles.brandArea}>
            <Link to="/" className={styles.logoLink} title="Página Inicial">
              <span className="material-symbols-outlined" style={{ fontSize: '24px', color: 'var(--color-primary)' }}>
                auto_awesome
              </span>
              <span className={styles.brandText}>Resurva</span>
            </Link>

            <div className={styles.divider} />

            <nav className={styles.navLinks}>
              <Link
                to="/app/analises/nova"
                className={`${styles.navItem} ${isNovaAnalise ? styles.navItemActive : ''}`}
              >
                <span className="material-symbols-outlined" style={{ fontSize: '18px' }}>
                  add_circle
                </span>
                <span>Nova Análise</span>
              </Link>
            </nav>
          </div>

          <div className={styles.userArea}>
            {user && (
              <div className={styles.profileBadge}>
                {user.photoURL ? (
                  <img
                    src={user.photoURL}
                    alt={user.displayName || user.email || 'Usuário'}
                    className={styles.userAvatar}
                  />
                ) : (
                  <div className={styles.initialsAvatar}>
                    {(user.displayName || user.email || 'U').charAt(0).toUpperCase()}
                  </div>
                )}
                <div className={styles.userMeta}>
                  <span className={styles.userName}>{user.displayName || 'Usuário Autorizado'}</span>
                  <span className={`${styles.userEmail} font-label-code`}>{user.email}</span>
                </div>
              </div>
            )}

            <Button
              variant="ghost"
              size="sm"
              icon="logout"
              onClick={signOut}
              disabled={busy}
              title="Sair da conta"
            >
              Sair
            </Button>
          </div>
        </div>
      </header>

      <main className={styles.mainContent}>
        <Outlet />
      </main>
    </div>
  )
}
