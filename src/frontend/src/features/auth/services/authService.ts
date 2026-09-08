import { initializeApp } from 'firebase/app'
import { getAuth, GoogleAuthProvider, signInWithPopup, signOut, type Auth } from 'firebase/auth'

const config = {
  apiKey: import.meta.env.VITE_FIREBASE_API_KEY,
  authDomain: import.meta.env.VITE_FIREBASE_AUTH_DOMAIN,
  projectId: import.meta.env.VITE_FIREBASE_PROJECT_ID,
  appId: import.meta.env.VITE_FIREBASE_APP_ID,
}

export const isFirebaseConfigured = Object.values(config).every(value => Boolean(value?.trim()))

export const firebaseAuth: Auth | null = isFirebaseConfigured
  ? getAuth(initializeApp(config))
  : null

export async function loginWithGoogle(): Promise<void> {
  if (!firebaseAuth) {
    throw new Error('O login ainda não está configurado neste ambiente.')
  }
  const provider = new GoogleAuthProvider()
  provider.setCustomParameters({ prompt: 'select_account' })
  await signInWithPopup(firebaseAuth, provider)
}

export async function logout(): Promise<void> {
  if (firebaseAuth) {
    await signOut(firebaseAuth)
  }
}

export async function getCurrentIdToken(): Promise<string> {
  const user = firebaseAuth?.currentUser
  if (!user) {
    throw new Error('Entre com sua conta Google para continuar.')
  }
  return user.getIdToken()
}

export async function verifyApiSession(): Promise<void> {
  const apiBaseUrl = (import.meta.env.VITE_API_BASE_URL ?? '').replace(/\/+$/, '')
  const token = await getCurrentIdToken()
  const response = await fetch(`${apiBaseUrl}/api/auth/session`, {
    headers: {
      Authorization: `Bearer ${token}`,
    },
  })

  if (response.status === 204) {
    return
  }

  if (response.status === 403) {
    throw new Error('Esta conta não tem acesso ao ambiente de testes.')
  }
  if (response.status === 401) {
    throw new Error('Sua sessão não foi aceita. Saia e entre novamente.')
  }

  throw new Error('A API não confirmou o acesso. Confira a versão publicada.')
}

export function loginErrorMessage(error: unknown): string {
  const code = error && typeof error === 'object' && 'code' in error ? (error as { code: string }).code : ''
  switch (code) {
    case 'auth/popup-closed-by-user':
      return 'Login cancelado. Você pode tentar novamente.'
    case 'auth/popup-blocked':
      return 'Permita a abertura da janela de login no navegador.'
    case 'auth/unauthorized-domain':
      return 'Este domínio ainda não está autorizado para login.'
    case 'auth/configuration-not-found':
    case 'auth/operation-not-allowed':
      return 'O login com Google ainda não foi habilitado neste ambiente.'
    case 'auth/network-request-failed':
      return 'Não foi possível conectar ao login. Verifique sua conexão de internet.'
    default:
      return error instanceof Error ? error.message : 'Não foi possível entrar. Tente novamente.'
  }
}
