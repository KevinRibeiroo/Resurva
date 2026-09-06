import { useEffect, useState, type ReactNode } from 'react'
import { onAuthStateChanged, type User } from 'firebase/auth'
import { firebaseAuth, loginErrorMessage, loginWithGoogle, logout } from './auth'
import { verifySession } from './api'

export function AuthGate({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<User | null>(null)
  const [checking, setChecking] = useState(Boolean(firebaseAuth))
  const [authorized, setAuthorized] = useState(false)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  const [attempt, setAttempt] = useState(0)

  useEffect(() => {
    if (!firebaseAuth) return
    let version = 0
    const unsubscribe = onAuthStateChanged(firebaseAuth, async current => {
      const currentVersion = ++version
      setUser(current)
      setAuthorized(false)
      setError('')
      setChecking(Boolean(current))
      if (!current) return
      try {
        await verifySession()
        if (currentVersion === version) setAuthorized(true)
      } catch (caught) {
        if (currentVersion === version) setError(caught instanceof Error ? caught.message : 'Não foi possível verificar o acesso.')
      } finally {
        if (currentVersion === version) setChecking(false)
      }
    })
    return () => { version++; unsubscribe() }
  }, [attempt])

  async function signIn() {
    setBusy(true); setError('')
    try { await loginWithGoogle() }
    catch (caught) { setError(loginErrorMessage(caught)) }
    finally { setBusy(false) }
  }

  async function signOut() {
    setBusy(true); setError('')
    try { await logout() }
    catch { setError('Não foi possível sair. Tente novamente.') }
    finally { setBusy(false) }
  }

  if (authorized && user) return <>
    <div className="session-bar"><span>{user.email}</span><button type="button" disabled={busy} onClick={signOut}>Sair</button></div>
    {error && <p role="alert" className="error session-error">{error}</p>}
    {children}
  </>

  return <main>
    <header><p className="eyebrow">Acesso privado</p><h1>ResumeMatcher</h1><p>Compare seu currículo com uma vaga sem inventar experiências ou competências.</p></header>
    <section className="login-panel" aria-busy={checking || busy}>
      {!firebaseAuth ? <p role="alert">O login ainda não está configurado neste ambiente.</p>
        : checking ? <p role="status">Verificando seu acesso…</p>
        : user ? <>
          <p>Conta conectada: {user.email}</p>
          <p>O acesso ao ambiente de testes precisa ser autorizado.</p>
          <div className="login-actions"><button type="button" disabled={busy} onClick={() => setAttempt(value => value + 1)}>Verificar novamente</button><button type="button" disabled={busy} onClick={signOut}>Sair e trocar de conta</button></div>
        </> : <>
          <h2>Entre para comparar seu currículo</h2>
          <p>Este ambiente de testes está disponível apenas para a conta autorizada.</p>
          <button type="button" disabled={busy} onClick={signIn}>{busy ? 'Entrando…' : 'Entrar com Google'}</button>
        </>}
      {error && <p role="alert" className="error">{error}</p>}
    </section>
  </main>
}
