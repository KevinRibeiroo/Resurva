# Login Google no ambiente privado

O frontend usa Firebase Authentication para login Google. A API .NET valida o ID token e permite somente um e-mail Google verificado, informado em configuração. A comparação como visitante fica no roadmap; não há acesso anônimo aos dados nem quotas para visitantes nesta implementação.

## Contrato de segurança

- `Authorization: Bearer <ID_TOKEN_FIREBASE>` em todas as chamadas, inclusive `/health`.
- `GET /api/auth/session`: `204` para uma conta autorizada, sem retornar dados pessoais.
- Sem token ou com assinatura, emissor, audiência, validade ou claims temporais inválidos: `401`.
- Token válido de outra conta, e-mail não verificado ou login por outro provider: `403`.
- Assinatura RS256 com as chaves oficiais do Firebase, emissor `https://securetoken.google.com/<ProjectId>` e audiência igual a `ProjectId`.
- A política de fallback protege inclusive novos endpoints que não tenham um atributo explícito. Não adicione `AllowAnonymous` aos endpoints de dados.
- A API não inicia se o projeto ou o e-mail autorizado estiverem ausentes/inválidos.
- CORS permite somente as origens cadastradas e é executado antes da autenticação; o preflight `OPTIONS` não precisa de token. CORS não substitui autorização.

O SDK Firebase gerencia a sessão no navegador e renova o ID token ao chamar `getIdToken()`. A aplicação não grava tokens fixos em arquivos de configuração ou no bundle. A configuração pública Firebase (`apiKey`, `authDomain`, `projectId`, `appId`) identifica o app e pode estar no frontend; ela não é a chave Gemini, uma conta de serviço ou a senha do banco.

## Ativar no Firebase

No projeto `resume-matcher-f61df`:

1. Abra **Authentication** e inicie a configuração caso ainda não exista.
2. Em **Sign-in method / Método de login**, habilite **Google** e informe o e-mail de suporte solicitado pelo painel.
3. Em **Settings / Configurações → Authorized domains**, confira `resume-matcher-f61df.web.app` e `resume-matcher-f61df.firebaseapp.com`. Adicione `localhost` se for testar localmente; projetos novos podem não incluí-lo automaticamente.
4. Use sua própria conta Google para entrar. Criar uma conta Firebase pelo login não libera acesso aos currículos: a API valida o e-mail autorizado em cada chamada.

Na verificação de 06/09/2026, a configuração de Authentication e o provider Google retornaram `CONFIGURATION_NOT_FOUND`; a ativação pelo painel permanecia pendente. Atualize este registro quando validar o login real.

## Configurar o backend

No Cloud Run, adicione variáveis comuns (os dois segredos de banco/Gemini permanecem como antes):

| Variável | Valor |
| --- | --- |
| `Authentication__Firebase__ProjectId` | `resume-matcher-f61df` |
| `Authentication__Firebase__AllowedEmail` | E-mail da única conta Google autorizada |
| `Cors__Origins__0` | `https://resume-matcher-f61df.web.app` |
| `Cors__Origins__1` | `https://resume-matcher-f61df.firebaseapp.com` |

O projeto Firebase é diferente do projeto GCP onde roda a API; use o projeto **Firebase emissor do token**, não o ID do Cloud Run. Se já existem variáveis CORS no serviço, confira todas as entradas porque elas têm precedência sobre `appsettings.json`.

Localmente, na raiz do repositório:

```powershell
dotnet user-secrets set "Authentication:Firebase:AllowedEmail" "SEU_EMAIL_GOOGLE" --project src/backend/ResumeMatcher.Api
dotnet run --project src/backend/ResumeMatcher.Api
```

O `ProjectId` já está configurado em `appsettings.json`. Banco e Gemini continuam vindo dos seus segredos. Não há chave privada Firebase a adicionar: a API consulta os metadados/chaves públicos via HTTPS.

## Configurar o frontend

`.env.production` já contém a configuração pública do Firebase e a URL do Cloud Run. Para desenvolvimento local, copie `src/frontend/.env.example` para `.env.local`, preservando os valores Firebase e deixando `VITE_API_BASE_URL` vazio para usar o proxy `/api`. O domínio local deve estar autorizado no Firebase.

Variáveis `VITE_*` são resolvidas no build. Reinicie o Vite depois de mudar `.env.local` e refaça o deploy se mudar as variáveis do build publicado.

## Ordem da publicação

1. Configure o e-mail autorizado e o projeto Firebase no Cloud Run antes de publicar a nova imagem. A versão anterior ignora essas novas variáveis.
2. Publique o backend com esta proteção, mantendo inicialmente o IAM exigido. Confirme que a nova revisão ficou pronta. Nenhuma migration nova é necessária.
3. Habilite o login Google no Firebase e publique o frontend atualizado.
4. Para validar a nova API enquanto o IAM continua ativo, envie o ID token Firebase em `Authorization` e um ID token IAM autorizado em `X-Serverless-Authorization`. Assim o Google valida o acesso à infraestrutura e a aplicação valida a identidade Firebase. Um token `gcloud auth print-identity-token` sozinho não autoriza mais os endpoints da aplicação.
5. Após confirmar a proteção da nova revisão, altere a configuração do Cloud Run para permitir requisições sem autenticação IAM na borda. A API continua exigindo autenticação JWT e bloqueando qualquer conta fora da configuração. Isso permite que o preflight do navegador alcance o middleware CORS.
6. Confirme pelo site: login da conta autorizada libera o formulário; outra conta recebe acesso negado; sem token, os endpoints retornam `401`.

Não retire o IAM de uma revisão antiga, que ainda não tenha a autenticação na aplicação. Ao fazer rollback, restaure a barreira IAM **antes** de voltar para uma versão anterior a esta proteção.

## Testes e limites

Os testes HTTP usam tokens sintéticos assinados com RSA e chaves de teste em memória, sem credenciais Firebase reais. Cobrem acesso sem token em todos os endpoints, expiração, assinatura, emissor, audiência, claims, conta autorizada, provider Google e preflight CORS. O pipeline funcional de comparação/consulta/exclusão também roda autenticado.

Não há consulta de revogação/desativação Firebase em cada requisição. Tokens já emitidos podem continuar aceitos até expirar; a restrição ao e-mail configurado é verificada em todas as requisições. Propriedade de currículos/análises e cache isolado por UID estão implementados; a abertura multiusuário ainda depende de fluxo de contas, atribuição dos dados legados e implantação dos controles de [privacidade](DATA_PRIVACY.md).

A checagem `/health` passou a exigir token Firebase. Se configurar uma probe HTTP de infraestrutura, não a aponte para esse endpoint protegido; mantenha a probe TCP de inicialização até definir uma probe de liveness apropriada.

## Referências

- [Login Google com Firebase](https://firebase.google.com/docs/auth/web/google-signin)
- [Validação de ID tokens](https://firebase.google.com/docs/auth/admin/verify-id-tokens)
- [Chaves públicas de configuração Firebase](https://firebase.google.com/docs/projects/api-keys)
- [Autenticação de usuários no Cloud Run e CORS](https://cloud.google.com/run/docs/authenticating/end-users)
- [Autenticação entre serviços e X-Serverless-Authorization](https://cloud.google.com/run/docs/authenticating/service-to-service)
