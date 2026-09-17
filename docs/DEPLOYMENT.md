# Publicação privada do backend

Este guia descreve o primeiro ambiente de testes pessoais. Ele não substitui uma arquitetura de produção com autenticação de usuários, política de retenção, backup e observabilidade completa.

## Requisitos do ambiente

- PostgreSQL acessível pela API.
- Imagem do backend construída pelo `src/backend/ResumeMatcher.Api/Dockerfile`.
- Porta HTTP `8080` exposta pela plataforma.
- Endpoint `/health` para verificação autenticada da API e do banco. Não use esse endpoint protegido como probe HTTP anônima.
- Acesso aos dados restrito à conta autorizada por JWT Firebase; mantenha a barreira IAM até concluir a transição descrita em `AUTHENTICATION.md`.

## Configurações obrigatórias

Forneça os valores abaixo por variáveis de ambiente ou Secret Manager. Nunca grave os valores reais no repositório ou na imagem:

```text
ConnectionStrings__ResumeMatcher
LLM__Provider
LLM__Model
LLM__ApiKey
Cors__Origins__0
Authentication__Firebase__ProjectId
Authentication__Firebase__AllowedEmail
```

Para o PostgreSQL do Supabase, use os parâmetros de Session pooler mostrados no painel:

```text
Host=HOST_DO_POOLER;Port=5432;Database=postgres;Username=USUARIO_DO_PAINEL;Password=SENHA;SSL Mode=Require
```

## Construção local

Na raiz do repositório:

```powershell
docker build -f src/backend/ResumeMatcher.Api/Dockerfile -t resumematcher-api .
```

O daemon Docker precisa estar em execução. A construção também pode ser feita pelo serviço de build da plataforma usando o mesmo Dockerfile e a raiz do repositório como contexto.

## Primeira publicação na GCP

Para um ambiente pessoal no Cloud Run:

1. Envie a imagem para o Artifact Registry.
2. Implante inicialmente com IAM exigido. Para acesso pelo frontend Firebase, siga [Autenticação e transição](AUTHENTICATION.md) antes de permitir chamadas na borda.
3. Conceda ao serviço acesso apenas aos segredos necessários. A conexão ao Supabase usa sua connection string, sem integração Cloud SQL.
4. Mantenha uma única instância durante os testes iniciais.
5. Verifique `/health` com autenticação Firebase. Mantenha probe TCP de inicialização; não configure probe HTTP anônima nesse endpoint protegido.
6. Defina orçamento, alertas e cotas do Gemini antes de habilitar o provider real.

A API aplica migrations ao iniciar por padrão, aceitável para o ambiente privado de instância única. Para produção ou múltiplas réplicas, separe a execução com o comando `--migrate-only` executado no pipeline de deploy e configure `Database__AutoMigrate=false` no Cloud Run.

## Purga periódica de retenção (30 dias)

Para automatizar a execução periódica do comando de limpeza física (`--purge-expired`), utilize os scripts versionados de provisionamento:
- PowerShell: `scripts/setup-retention-purge.ps1`
- Bash: `scripts/setup-retention-purge.sh`

Eles configuram um **Cloud Run Job** reutilizando a imagem da API e um **Cloud Scheduler** diário (03:00 UTC) chamando o Job com autenticação OIDC via Service Account dedicada.

## Publicação do Frontend (Firebase Hosting)

A publicação do frontend no canal live (`resume-matcher-f61df.web.app`) é automatizada pelo GitHub Actions ao realizar o merge de um Pull Request na branch `develop` (job `deploy-frontend`). A publicação só é acionada após a aprovação de todos os testes e builds. Consulte [CI](CI.md) para a configuração do segredo `FIREBASE_SERVICE_ACCOUNT_RESUME_MATCHER_F61DF`. Para publicação manual sob demanda, execute `cd src/frontend && npx -y firebase-tools deploy --only hosting`.

## Verificação

Depois da publicação:

1. Confirme que `/health` retorna `200 Healthy` para uma chamada autenticada.
2. Envie apenas um currículo fictício.
3. Compare duas vezes com a mesma vaga e confirme que o mesmo identificador é retornado.
4. Verifique nos logs um cache miss seguido de cache hit.
5. Exclua o currículo com `DELETE /api/resumes/{id}` e confirme que a análise deixou de existir.
6. Confira o painel de faturamento antes de utilizar dados reais.

## Limites desta fase

- O login Google, autorização de uma conta e isolamento por UID estão implementados; cadastro multiusuário e acesso como visitante não estão disponíveis.
- Não remova o IAM antes de publicar e validar a revisão que exige JWT Firebase na aplicação.
- Não use dados de terceiros sem consentimento.
- Não existe transferência automática de dados SQLite legados.
- Expiração de 30 dias e comando de limpeza implementados; agendamento, backups e auditoria operacional pendentes. Leia [Privacidade](DATA_PRIVACY.md).

## Antes de publicar ownership e retenção

A migration `AddDataOwnershipAndRetention` torna registros antigos inacessíveis até atribuir seu dono. Como a API aplica migrations ao iniciar, planeje a janela e a [atribuição dos dados](DATA_PRIVACY.md) antes do merge que aciona deploy. Não execute limpeza antes dessa revisão e não reverta para uma API antiga com consultas globais.

O [CI](CI.md) não modifica o gatilho Cloud Build. Ative checks obrigatórios e restrinja push direto/bypass na develop para impedir publicação de alterações não validadas.
