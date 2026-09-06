import { initializeApp } from 'firebase/app'
import { getAuth, GoogleAuthProvider, signInWithPopup, signOut } from 'firebase/auth'

const config = {
  apiKey: import.meta.env.VITE_FIREBASE_API_KEY,
  authDomain: import.meta.env.VITE_FIREBASE_AUTH_DOMAIN,
  projectId: import.meta.env.VITE_FIREBASE_PROJECT_ID,
  appId: import.meta.env.VITE_FIREBASE_APP_ID,
}

export const firebaseAuth = Object.values(config).every(value => Boolean(value?.trim()))
  ? getAuth(initializeApp(config))
  : null

export async function loginWithGoogle() {
  if (!firebaseAuth) throw new Error('O login ainda não está configurado neste ambiente.')
  const provider = new GoogleAuthProvider()
  provider.setCustomParameters({ prompt: 'select_account' })
  await signInWithPopup(firebaseAuth, provider)
}

export async function logout() {
  if (firebaseAuth) await signOut(firebaseAuth)
}

export async function getCurrentIdToken(): Promise<string> {
  const user = firebaseAuth?.currentUser
  if (!user) throw new Error('Entre com sua conta Google para continuar.')
  // Firebase renews expired ID tokens; credentials are never embedded in the build.
  return user.getIdToken()
}

export function loginErrorMessage(error: unknown): string {
  const code = error && typeof error === 'object' && 'code' in error ? error.code : ''
  switch (code) {
    case 'auth/popup-closed-by-user': return 'Login cancelado. Você pode tentar novamente.'
    case 'auth/popup-blocked': return 'Permita a abertura da janela de login no navegador.'
    case 'auth/unauthorized-domain': return 'Este domínio ainda não está autorizado para login.'
    case 'auth/configuration-not-found':
    case 'auth/operation-not-allowed': return 'O login com Google ainda não foi habilitado neste ambiente.'
    case 'auth/network-request-failed': return 'Não foi possível conectar ao login. Verifique sua conexão.'
    default: return 'Não foi possível entrar. Tente novamente.'
  }
}
