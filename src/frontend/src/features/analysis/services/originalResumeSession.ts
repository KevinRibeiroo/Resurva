// One original in memory only. No browser persistence, URLs, or cross-account reuse.
let owner: string | null = null
let session: object | null = null
let original: { resumeId: string; file: File } | null = null

export function setOriginalResumeOwner(userId: string | null) {
  if (owner !== userId || userId === null) {
    original = null
    session = userId ? {} : null
  }
  owner = userId
}

export function originalResumeOwner() { return session }

export function rememberOriginalResume(capturedSession: object | null, resumeId: string, file: File) {
  if (capturedSession && session === capturedSession) original = file.name.toLowerCase().endsWith('.docx') ? { resumeId, file } : null
}

export function getOriginalResume(resumeId: string): File | null {
  return owner && original?.resumeId === resumeId ? original.file : null
}

export function forgetOriginalResume(resumeId: string) {
  if (original?.resumeId === resumeId) original = null
}
