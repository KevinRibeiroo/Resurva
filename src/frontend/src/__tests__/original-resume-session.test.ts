import { afterEach, expect, it } from 'vitest'
import { setOriginalResumeOwner, originalResumeOwner, rememberOriginalResume, getOriginalResume } from '../features/analysis/services/originalResumeSession'

afterEach(() => setOriginalResumeOwner(null))

it('clears original across logout/owner change and rejects late uploads from an older session', () => {
  setOriginalResumeOwner('owner-a')
  const oldSession = originalResumeOwner()
  const file = new File(['synthetic'], 'synthetic.docx')
  rememberOriginalResume(oldSession, 'resume-a', file)
  expect(getOriginalResume('resume-a')).toBe(file)
  expect(getOriginalResume('different-resume')).toBeNull()
  setOriginalResumeOwner(null)
  expect(getOriginalResume('resume-a')).toBeNull()
  setOriginalResumeOwner('owner-a')
  rememberOriginalResume(oldSession, 'resume-a', file)
  expect(getOriginalResume('resume-a')).toBeNull()
  rememberOriginalResume(originalResumeOwner(), 'resume-a', file)
  setOriginalResumeOwner('owner-b')
  expect(getOriginalResume('resume-a')).toBeNull()
})
