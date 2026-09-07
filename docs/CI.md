# Integração contínua

Workflow: `.github/workflows/ci.yml`. Não publica aplicações, altera a GCP nem acessa segredos reais. O deploy do Cloud Build continua independente.

## Execução

- `pull_request` para `develop` e `main`: valida a integração antes do merge.
- `push` nessas branches: verifica o estado integrado.
- `workflow_dispatch`: execução manual.
- Execuções anteriores do mesmo PR/branch são canceladas quando substituídas.
- Token limitado a `contents: read`; checkout não persiste credenciais; Actions fixadas por SHA.

| Check | Conteúdo |
| --- | --- |
| Backend build and tests | .NET 10, restore, build Release, testes unitários/HTTP e PostgreSQL descartável 18 |
| Frontend typecheck and build | Node 24, pnpm 11.19.0, lockfile congelado, TypeScript e build Vite |

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

Referências: [build/test .NET](https://docs.github.com/en/actions/tutorials/build-and-test-code/net), [PostgreSQL no CI](https://docs.github.com/en/actions/tutorials/use-containerized-services/create-postgresql-service-containers) e [checks obrigatórios](https://docs.github.com/en/pull-requests/reference/status-checks).
