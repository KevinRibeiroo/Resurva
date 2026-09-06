# Frontend no Firebase Hosting

O frontend React/Vite está configurado para Firebase Hosting estático, com `dist` como diretório público e fallback para `/index.html` nas rotas da SPA. Não requer o SDK JavaScript do Firebase para hospedar os arquivos.

## Projeto e configuração

- `src/frontend/.firebaserc`: projeto padrão `resume-matcher-f61df`, selecionado no painel Firebase mostrado durante o setup. É diferente do projeto GCP da API (`project-1404ad16-3da8-499b-90a`).
- `src/frontend/firebase.json`: configuração de Hosting; o hook `predeploy` executa `pnpm run build` antes da publicação.
- `src/frontend/.env.production`: URL pública do backend Cloud Run em `VITE_API_BASE_URL`, sem `/api` no final.
- No desenvolvimento, sem essa variável, as chamadas continuam em `/api` pelo proxy local do Vite.

As variáveis `VITE_*` são incorporadas ao JavaScript público durante o build. Nunca coloque nelas ID tokens, chaves Gemini ou connection strings. Para sobrescrever a URL localmente, use `.env.production.local` (ignorado pelo Git) ou uma variável de ambiente antes do build.

## Preparar e publicar

Pré-requisitos: Node.js compatível com `package.json`, pnpm e Firebase CLI instalada (`npm install -g firebase-tools`).

Execute a partir de `src/frontend`:

```powershell
firebase login
pnpm install --frozen-lockfile
pnpm build
firebase deploy --only hosting --project resume-matcher-f61df
```

O build manual permite validar antecipadamente; o deploy recompila pelo hook. Não é necessário executar novamente `firebase init`. Se o assistente ainda estiver aberto, encerre-o com Ctrl+C para não sobrescrever esses arquivos.

O comando de deploy publica a interface em um site acessível publicamente. Configurar os arquivos ou executar o build local não publica nada. O deploy automatizado via GitHub não está configurado neste setup.

## Integração com a API: pendência de autenticação

A URL da API já entra no build, mas a API do Cloud Run continua exigindo IAM. O frontend atual não implementa login ou obtenção de tokens aceitos pelo Cloud Run. Portanto, publicar a interface não torna upload e comparação funcionais de ponta a ponta.

Antes de testar o fluxo pelo navegador, é necessário definir a estratégia de autenticação e autorizar a origem efetiva do site no CORS do backend (`Cors__Origins__0`, etc.). Um ID token do Firebase Authentication não substitui automaticamente a autenticação IAM do Cloud Run. CORS também não concede autorização IAM.

Esta configuração não adiciona proxy autenticado, não muda permissões do Cloud Run e não grava tokens no frontend. Não libere acesso anônimo à API apenas para contornar essa pendência.

## Referências

- [Configuração do Firebase Hosting](https://firebase.google.com/docs/hosting/full-config)
- [Variáveis e modos do Vite](https://vite.dev/guide/env-and-mode)
