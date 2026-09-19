import React from 'react'
import { Navigate, useLocation } from 'react-router-dom'
import { useAuth } from '../../../app/providers/AuthProvider'
import { Button } from '../../../shared/ui/Button/Button'
import { Card } from '../../../shared/ui/Card/Card'
import { Alert } from '../../../shared/ui/Alert/Alert'
import { Spinner } from '../../../shared/ui/Spinner/Spinner'
import styles from './LoginPage.module.css'

export function LoginPage() {
  const {
    user,
    checking,
    authorized,
    authError,
    busy,
    isConfigured,
    signIn,
    signOut,
    retrySession,
  } = useAuth()

  const location = useLocation()
  const from = (location.state as { from?: { pathname: string } })?.from?.pathname || '/app/analises/nova'

  // If already authenticated and authorized by API, redirect straight to intended app route
  if (user && authorized && !checking) {
    return <Navigate to={from} replace />
  }

  return (
    <div className={styles.page}>
      <div className="ambient-glow" style={{ width: '480px', height: '480px', background: 'rgba(99, 102, 241, 0.15)', top: '10%', left: '50%', transform: 'translateX(-50%)' }} />

      <main className={styles.container}>
        <div className={styles.brandHeader}>
          <div className={styles.logoBadge}>
            <span className="material-symbols-outlined" style={{ fontSize: '32px', color: 'var(--color-primary)' }}>
              description
            </span>
          </div>
          <h1 className="font-headline-lg" style={{ color: 'var(--color-text-high)', marginBottom: '0.25rem' }}>
            Resurva
          </h1>
          <p className="font-body-md" style={{ color: 'var(--color-text-muted)' }}>
            Ambiente de análise comparativa e diagnóstico factual de currículos.
          </p>
        </div>

        <Card variant="glass" padding="xl" className={styles.authCard}>
          {!isConfigured ? (
            <Alert variant="error" title="Configuração pendente">
              O serviço de autenticação ainda não foi ativado para este domínio. Verifique as variáveis de ambiente do Firebase.
            </Alert>
          ) : checking ? (
            <div className={styles.checkingState}>
              <Spinner size="md" label="Validando credenciais com a API…" />
            </div>
          ) : user && !authorized ? (
            <div className={styles.unauthorizedState}>
              <div className={styles.userInfo}>
                {user.photoURL ? (
                  <img src={user.photoURL} alt={user.displayName || 'Usuário'} className={styles.avatar} />
                ) : (
                  <div className={styles.avatarPlaceholder}>
                    {(user.displayName || user.email || 'U').charAt(0).toUpperCase()}
                  </div>
                )}
                <div className={styles.userDetails}>
                  <strong className={styles.userName}>{user.displayName || 'Usuário'}</strong>
                  <span className={`${styles.userEmail} font-label-code`}>{user.email}</span>
                </div>
              </div>

              <Alert variant="warning" title="Conta não autorizada">
                {authError || 'Esta conta Google não possui autorização para o ambiente restrito do MVP.'}
              </Alert>

              <p className={styles.instruction}>
                O acesso à API está restrito ao e-mail aprovado para testes. Você pode tentar novamente ou entrar com outra conta autorizada.
              </p>

              <div className={styles.actionsGroup}>
                <Button variant="primary" onClick={retrySession} disabled={busy} icon="refresh">
                  Verificar novamente
                </Button>
                <Button variant="ghost" onClick={signOut} disabled={busy} icon="logout">
                  Trocar de conta Google
                </Button>
              </div>
            </div>
          ) : (
            <div className={styles.loginForm}>
              <div className={styles.loginIntro}>
                <h2 className="font-headline-md" style={{ color: 'var(--color-text-high)', marginBottom: '0.5rem' }}>
                  Acesso Restrito
                </h2>
                <p className="font-body-sm" style={{ color: 'var(--color-text-muted)' }}>
                  Entre com sua conta Google autorizada para iniciar novas análises e acessar seus dados com isolamento e segurança.
                </p>
              </div>

              {authError && (
                <Alert variant="error" style={{ marginBottom: 'var(--space-md)' }}>
                  {authError}
                </Alert>
              )}

              <Button
                variant="primary"
                size="lg"
                onClick={signIn}
                loading={busy}
                icon="login"
                className={styles.googleButton}
              >
                Entrar com Google
              </Button>

              <div className={styles.securityBadge}>
                <span className="material-symbols-outlined" style={{ fontSize: '18px', color: 'var(--color-tertiary)' }}>
                  shield
                </span>
                <span className="font-body-sm" style={{ color: 'var(--color-text-muted)', fontSize: '0.75rem' }}>
                  Autenticação JWT protegida diretamente pela API backend.
                </span>
              </div>
            </div>
          )}
        </Card>

        <footer className={styles.pageFooter}>
          <a href="/" className="font-body-sm" style={{ color: 'var(--color-text-muted)', textDecoration: 'underline' }}>
            ← Voltar para página inicial
          </a>
        </footer>
      </main>
    </div>
  )
}
