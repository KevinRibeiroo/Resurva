# Integração contínua

Workflow: `.github/workflows/ci.yml`. Valida o backend (.NET 10) e frontend (React 19.2/Vite 8), e automatiza a publicação do frontend no Firebase Hosting ao realizar merge na `develop`. O deploy do backend pelo Cloud Build continua independente.

## Execução

- `pull_request` para `develop` e `main`: valida a integração antes do merge (não publica o frontend).
- `push` na `develop`: executa os testes/builds e, se aprovados, executa o job `deploy-frontend` para o Firebase Hosting no canal live.
- `push` na `main`: executa verificação de integração.
- `workflow_dispatch`: execução manual.
- Execuções anteriores do mesmo PR/branch são canceladas quando substituídas.
- Token limitado a `contents: read`; checkout não persiste credenciais; Actions fixadas por SHA/versão.

| Check | Conteúdo |
| --- | --- |
| Backend build and tests | .NET 10, restore, build Release, testes unitários/HTTP e PostgreSQL descartável 18 |
| Frontend typecheck and build | Node 24, pnpm 12.3.4, lockfile congelado, TypeScript e build Vite |
| Deploy frontend to Firebase Hosting | Executado exclusivamente em `push` na `develop` após aprovação dos checks anteriores |

Não há suíte de comportamento do frontend nesta etapa. Typecheck/build não substituem testes de login/upload/resultados no navegador. Nenhum limite arbitrário de cobertura foi introduzido.

O banco do CI tem credenciais fictícias exclusivas do runner, sem GitHub Secrets. `RESUMEMATCHER_TEST_POSTGRES` é fornecida pelo workflow. O teste aceita somente loopback, cria um banco `resume_matcher_tests_<GUID>` e remove apenas esse banco ao terminar. Localmente, esse teste é explicitamente ignorado sem a variável; no CI ela deve permanecer obrigatoriamente configurada.

```powershell
# Apenas instância local DESCARTÁVEL, com permissão para criar bancos.
$env:RESUMEMATCHER_TEST_POSTGRES = "Host=127.0.0.1;Port=5432;Database=postgres;Username=postgres;Password=SENHA_DO_BANCO_DESCARTAVEL"
dotnet test tests/ResumeMatcher.Tests/ResumeMatcher.Tests.csproj
```

## Ativação no GitHub

Criar YAML **não bloqueia merges sozinho**. Após enviar a branch e abrir PR:

1. Conferir os dois checks em Actions.
2. Criar ruleset/proteção da `develop` (e da `main` quando utilizada), exigindo PR.
3. Tornar `Backend build and tests` e `Frontend typecheck and build` obrigatórios.
4. Exigir branch atualizada antes do merge e restringir push direto/bypass, inclusive de administradores conforme o processo aprovado.
5. Conferir disponibilidade dessas proteções no plano GitHub do repositório privado.

As configurações remotas não foram alteradas. Não foi definida quantidade de aprovações humanas: o projeto atualmente tem um responsável.

Merge aprovado gera push na `develop`, acionando o gatilho existente do Cloud Build. O CI de `push` não bloqueia esse gatilho: se alguém contornar a proteção e fizer push direto, o deploy poderá começar antes dos testes. Para este desenho, manter a proteção efetiva é obrigatório. Um gate também após o merge exige evolução explícita da coordenação do deploy, não um segundo pipeline concorrente.

## Configuração do deploy automático do Frontend (Firebase)

Para que o job `deploy-frontend` publique no Firebase Hosting, é necessário cadastrar o segredo `FIREBASE_SERVICE_ACCOUNT_RESUME_MATCHER_F61DF` no GitHub:

1. **Via Firebase CLI (automático):**
   ```powershell
   cd src/frontend
   npx -y firebase-tools init hosting:github
   ```
   - Responda `KevinRibeiroo/ResumeMatcher` para o repositório.
   - Responda `N` para rodar script de build antes de cada deploy (já gerenciado pelo workflow).
   - Responda `Y` para deploy automático no canal live ao mergear.
   - Indique a branch `develop`. O CLI cria a Service Account na GCP e salva o secret automaticamente no GitHub.

2. **Manualmente pelo Console da GCP:**
   - Acesse o Console da GCP no projeto `resume-matcher-f61df` ➔ **IAM e Administração** ➔ **Contas de serviço**.
   - Crie uma conta de serviço com a role `Administrador do Firebase Hosting` (`roles/firebasehosting.admin`).
   - Crie uma chave no formato JSON e faça o download.
   - No GitHub, acesse **Settings** ➔ **Secrets and variables** ➔ **Actions** ➔ **New repository secret**:
     - Nome: `FIREBASE_SERVICE_ACCOUNT_RESUME_MATCHER_F61DF`
     - Valor: Conteúdo integral do arquivo JSON da chave.

Referências: [build/test .NET](https://docs.github.com/en/actions/tutorials/build-and-test-code/net), [PostgreSQL no CI](https://docs.github.com/en/actions/tutorials/use-containerized-services/create-postgresql-service-containers), [checks obrigatórios](https://docs.github.com/en/pull-requests/reference/status-checks) e [action-hosting-deploy](https://github.com/FirebaseExtended/action-hosting-deploy).
