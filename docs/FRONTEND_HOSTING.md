# Frontend no Firebase Hosting

O frontend React/Vite está configurado para Firebase Hosting estático, com `dist` como diretório público e fallback para `/index.html` nas rotas da SPA. O SDK JavaScript do Firebase é usado para login Google; a hospedagem em si permanece estática.

## Projeto e configuração

- `src/frontend/.firebaserc`: projeto padrão `resume-matcher-f61df`, selecionado no painel Firebase mostrado durante o setup. É diferente do projeto GCP da API (`project-1404ad16-3da8-499b-90a`).
- `src/frontend/firebase.json`: configuração de Hosting; o hook `predeploy` executa `npm run build` antes da publicação. Isso executa o mesmo script do projeto sem exigir pnpm no PATH do terminal do Firebase; não instala dependências nem altera o lockfile.
- `src/frontend/.env.production`: URL pública do backend Cloud Run em `VITE_API_BASE_URL`, sem `/api` no final.
- No desenvolvimento, sem essa variável, as chamadas continuam em `/api` pelo proxy local do Vite.

As variáveis `VITE_*` são incorporadas ao JavaScript público durante o build. Nunca coloque nelas ID tokens, chaves Gemini ou connection strings. As variáveis `VITE_FIREBASE_*` contêm apenas a configuração pública do app Firebase. Para sobrescrever a URL localmente, use `.env.production.local` (ignorado pelo Git) ou uma variável de ambiente antes do build. Para desenvolvimento, copie `.env.example` para `.env.local` e habilite `localhost` nos domínios autorizados do Firebase.

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

## Integração autenticada com a API

O frontend possui login Google e envia ID tokens Firebase em `Authorization`. Antes de exibir o formulário, consulta `/api/auth/session`; a API exige que o token pertença à única conta autorizada por configuração. O backend valida a autenticação em todos os endpoints, mesmo se alguém contornar a tela de login.

Antes de testar o fluxo pelo navegador, habilite o provider Google no Firebase, configure o e-mail autorizado e publique a API protegida. Só depois faça a transição da autenticação IAM da infraestrutura para a validação de usuário na API. Veja a ordem e as variáveis em [Autenticação](AUTHENTICATION.md).

Um ID token Firebase não satisfaz automaticamente o IAM do Cloud Run. A barreira IAM ainda ativa pode bloquear `OPTIONS` antes do CORS. Os dados continuam privados pela autorização da API após a transição. Não publique nem volte para uma revisão antiga sem proteção JWT enquanto o IAM estiver desabilitado.

## Referências

- [Configuração do Firebase Hosting](https://firebase.google.com/docs/hosting/full-config)
- [Variáveis e modos do Vite](https://vite.dev/guide/env-and-mode)
