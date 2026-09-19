// Isolated visual fixture. No authentication, API requests, or real resume data.
// This entry is not included in the production build.
import { createRoot } from 'react-dom/client'
import { MemoryRouter } from 'react-router-dom'
import { AnalysisResultView } from '../../src/features/analysis/components/AnalysisResultView'
import { NewAnalysisPage } from '../../src/features/analysis/pages/NewAnalysisPage'
import '../../src/shared/styles/global.css'
import header from '../../src/app/layouts/AppLayout.module.css'
import type { AnalysisResultModel } from '../../src/features/analysis/models/AnalysisResultModel'

const analysis: AnalysisResultModel = {
  id:'exemplo-ficticio',resumeId:'exemplo',overallScore:78,skillsScore:85,experienceScore:72,seniorityScore:75,requirementsScore:80,educationScore:70,
  matchedSkills:[{text:'React e TypeScript',evidence:'Desenvolvimento de interfaces com React e TypeScript, com foco em acessibilidade e organização de componentes.'},{text:'Integração com APIs REST',evidence:'Integração de interfaces com serviços REST e tratamento de estados de carregamento e erro.'}],
  requirementsMet:[{text:'Versionamento com Git',evidence:'Uso de Git para versionamento e revisão de alterações em projetos colaborativos.'}],
  missingSkills:[{text:'Testes automatizados de interface',evidence:null}],requirementsMissing:[],
  strengths:[{text:'Experiência descrita com as tecnologias da vaga',evidence:'React e TypeScript aparecem no relato dos projetos.'}],
  pointsOfAttention:[{text:'A descrição não traz evidências de testes automatizados.'}],recommendations:[{text:'Detalhar o contexto dos projetos e confirmar experiências antes de acrescentá-las.'}],
}
const upload = new URLSearchParams(location.search).has('upload')
createRoot(document.getElementById('root')!).render(<MemoryRouter><header className={header.topBar}><div className={header.barInner} style={{flexWrap:'wrap'}}><a className={header.brandText} href="/tests/visual/index.html">Resurva</a><nav className={header.navLinks}><a className={header.navItem} href="/tests/visual/index.html">Comparação</a><a className={header.navItem} href="?upload">Nova análise</a></nav><span style={{color:'white',fontSize:12}}>Prévia fictícia</span></div></header><main>{upload ? <NewAnalysisPage /> : <AnalysisResultView analysis={analysis} optimizing={false} optError={null} onOptimize={() => alert('Prévia visual: nenhuma alteração será aplicada.')} />}</main></MemoryRouter>)
