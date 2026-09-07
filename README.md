# ResumeMatcher

Aplicação web para comparar um currículo com uma descrição de vaga e apresentar uma análise estruturada de aderência. O objetivo é ajudar candidatos a identificar competências encontradas, lacunas e pontos de atenção sem inventar experiências ou qualificações.

> **Estado atual:** MVP funcional. O `Mock` continua sendo o provider padrão para desenvolvimento local, e o Gemini pode ser habilitado por configuração. A pontuação final permanece determinística no backend.

## Funcionalidades

- Upload de currículos em PDF e DOCX, com limite de 10 MB.
- Extração local do texto dos documentos.
- Comparação com a descrição de uma vaga.
- Pontuação geral e por dimensão: skills, experiência, senioridade, requisitos e formação.
- Evidências textuais para itens encontrados no currículo.
- Persistência de currículos e resultados em PostgreSQL.
- Reutilização persistida de análises idênticas, sem uma nova chamada ao provider de IA.
- Exclusão de currículo com remoção das análises relacionadas.
- Health check do banco, limite de requisições e limites de entrada.
- Interface web para upload e visualização da análise.
- Login Google via Firebase Authentication, com acesso restrito a uma conta autorizada na API.
- Isolamento de currículos, análises e cache pelo UID autenticado.
- Expiração de acesso após 30 dias desde a última atualização e comando de limpeza física (agendamento externo pendente).
- Workflow CI de build/testes do backend e typecheck/build do frontend (proteção das branches depende de ativação no GitHub).
- Validação de segurança para impedir sugestões que adicionem informações não confirmadas.

## Tecnologias

| Área | Tecnologias |
| --- | --- |
| Backend | C# 14, .NET 10, ASP.NET Core Web API |
| Persistência | Entity Framework Core 10, Npgsql e PostgreSQL |
| Extração de documentos | PdfPig e Open XML SDK |
| Frontend | React 19.2, TypeScript 5.9 e Vite 8 |
| Testes | xUnit, Microsoft.NET.Test.Sdk e coverlet |
| IA | Abstração `ILLMProvider`; providers `MockLLMProvider` e `GeminiLLMProvider` |

O `MockLLMProvider` não envia conteúdo a serviços externos. Quando `LLM:Provider` é configurado como `Gemini`, o texto do currículo e a descrição da vaga são enviados ao serviço Google configurado; use essa opção somente com os controles de privacidade adequados.

## Arquitetura

O backend segue uma separação em camadas inspirada em Clean Architecture:

```text
Frontend React
      |
      v
ASP.NET Core API
      |
      v
Application  --->  Domain
      ^              ^
      |              |
Infrastructure -----+
```

- **Domain:** entidades e modelos centrais, sem dependências de infraestrutura.
- **Application:** contratos, serviços, regras de pontuação e casos de uso.
- **Infrastructure:** PostgreSQL, repositórios, migrations, extração de PDF/DOCX e providers de comparação.
- **Api:** composição da aplicação, middleware HTTP e controllers.
- **Frontend:** experiência de upload, comparação e apresentação dos resultados.

Leia [Arquitetura](docs/ARCHITECTURE.md) para detalhes do fluxo, dependências e decisões técnicas.

## Estrutura do repositório

```text
ResumeProject/
|-- src/
|   |-- backend/
|   |   |-- ResumeMatcher.Api/
|   |   |-- ResumeMatcher.Application/
|   |   |-- ResumeMatcher.Domain/
|   |   `-- ResumeMatcher.Infrastructure/
|   `-- frontend/
|-- tests/
|   `-- ResumeMatcher.Tests/
|-- docs/
|-- AGENTS.md
|-- README.md
`-- ResumeMatcher.slnx
```

## Pré-requisitos

- [.NET SDK 10](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Node.js 20.19 ou superior](https://nodejs.org/)
- [pnpm](https://pnpm.io/installation)
- [PostgreSQL](https://www.postgresql.org/download/) com um banco chamado `resumematcher`

## Executar localmente

### 1. Backend

Na raiz do repositório:

```powershell
$env:ConnectionStrings__ResumeMatcher = "Host=localhost;Port=5432;Database=resumematcher;Username=postgres;Password=SUA_SENHA"
dotnet restore ResumeMatcher.slnx
dotnet run --project src/backend/ResumeMatcher.Api
```

A API inicia em `http://localhost:5080` usando o perfil HTTP do projeto. As migrations do Entity Framework são aplicadas automaticamente na inicialização. A configuração versionada não contém senha; informe credenciais por variável de ambiente ou Secret Manager.

Antes de iniciar, configure também `Authentication:Firebase:AllowedEmail` com sua conta Google verificada. A API exige autenticação inclusive localmente. Consulte [Autenticação e ativação](docs/AUTHENTICATION.md) para configurar o login no frontend, testar e ativar no Cloud Run.

Essa mudança cria o esquema no PostgreSQL, mas não copia automaticamente dados de um arquivo SQLite antigo. Se houver dados locais que precisem ser preservados, faça uma migração de dados antes de remover o banco anterior.

### 2. Frontend

Em outro terminal:

```powershell
cd src/frontend
pnpm install
pnpm dev
```

Abra `http://localhost:5173`. Durante o desenvolvimento, o Vite encaminha chamadas `/api` para o backend em `http://localhost:5080`.

## API

| Método | Endpoint | Descrição |
| --- | --- | --- |
| `POST` | `/api/resumes/upload` | Recebe `multipart/form-data` com o campo `file` em PDF ou DOCX. |
| `DELETE` | `/api/resumes/{id}` | Exclui o currículo e suas análises. |
| `POST` | `/api/analysis/compare` | Compara o currículo enviado com uma descrição de vaga. |
| `GET` | `/api/analysis/{id}` | Recupera uma análise persistida pelo identificador. |
| `GET` | `/health` | Verifica a disponibilidade da API e do PostgreSQL. |
| `GET` | `/api/auth/session` | Valida o acesso da conta conectada; retorna `204` se autorizada. |

Os endpoints exigem `Authorization: Bearer <ID_TOKEN_FIREBASE>`. Token ausente ou inválido retorna `401`; uma identidade válida que não pertence à conta autorizada retorna `403`.

Exemplo do corpo para comparação:

```json
{
  "resumeId": "00000000-0000-0000-0000-000000000000",
  "jobDescription": "Descrição e requisitos da vaga"
}
```

Exemplos executáveis estão em [`Requests.http`](src/backend/ResumeMatcher.Api/Requests.http).

## Configuração

As configurações principais ficam em `src/backend/ResumeMatcher.Api/appsettings.json`:

- `ConnectionStrings:ResumeMatcher`: conexão com o PostgreSQL.
- `LLM:Provider`: `Mock` por padrão ou `Gemini`.
- `LLM:Model`: identificador do modelo, usado também na chave do cache de análises.
- `LLM:TimeoutSeconds`: tempo máximo de cada tentativa no Gemini.
- `LLM:MaxRetries`: quantidade limitada de novas tentativas para falhas temporárias.
- `RateLimiting`: limite de requisições por janela nos controllers da API.
- `Scoring`: pesos das cinco dimensões; a soma deve ser igual a `1`.
- `Cors:Origins`: origens autorizadas a acessar a API.
- `Authentication:Firebase:ProjectId`: projeto emissor dos tokens Firebase.
- `Authentication:Firebase:AllowedEmail`: única conta Google verificada autorizada neste ambiente privado.

Não versione chaves, tokens ou credenciais. Configure `ConnectionStrings__ResumeMatcher`, `LLM__ApiKey` e demais segredos por variáveis de ambiente ou pelo Secret Manager do .NET.

## Consistência das análises

Antes de chamar o provider, o backend calcula um SHA-256 com o texto normalizado do currículo, a vaga normalizada, o modelo, as configurações relevantes do LLM, a versão do prompt, a versão das regras e as configurações de scoring. Se o hash já estiver persistido, a resposta salva é devolvida sem chamar novamente o Mock ou o Gemini e sem recalcular a saída do LLM. Alterações reais no prompt ou nas regras devem incrementar suas versões explícitas para permitir uma nova análise.

## Container e publicação

O backend possui um Dockerfile preparado para escutar na porta `8080`:

```powershell
docker build -f src/backend/ResumeMatcher.Api/Dockerfile -t resumematcher-api .
```

Forneça connection string e chave do Gemini por um gerenciador de segredos. A API protege os dados com autenticação JWT e autorização para uma única conta; a transição da barreira IAM deve seguir [Publicação privada](docs/DEPLOYMENT.md) e [Autenticação](docs/AUTHENTICATION.md).

O frontend possui configuração para [Firebase Hosting](docs/FRONTEND_HOSTING.md), com publicação de `src/frontend/dist` e login Google. A ativação do provider no painel e a publicação da API protegida são necessárias para completar o fluxo no navegador.

## Compilar e testar

```powershell
dotnet build ResumeMatcher.slnx
dotnet test tests/ResumeMatcher.Tests/ResumeMatcher.Tests.csproj

cd src/frontend
pnpm build
```

## Documentação

- [Arquitetura e fluxo técnico](docs/ARCHITECTURE.md)
- [Convenções e regras de desenvolvimento](docs/CONVENTIONS.md)
- [Estado atual e roadmap](docs/ROADMAP.md)
- [Publicação privada e configuração](docs/DEPLOYMENT.md)
- [Contexto para manutenção e continuidade](AGENTS.md)
- [Autenticação Google e ativação no Cloud Run](docs/AUTHENTICATION.md)
- [Isolamento, retenção e exclusão](docs/DATA_PRIVACY.md)
- [CI e proteção das branches](docs/CI.md)

## Uso responsável

O ResumeMatcher deve destacar informações que já existem no currículo. Qualquer funcionalidade futura de otimização precisa distinguir sugestões de redação de alterações factuais e exigir confirmação explícita antes de incluir competências, experiências ou formações não comprovadas.

## Licença

O projeto ainda não possui uma licença definida. Antes de aceitar contribuições ou distribuir o software, adicione um arquivo `LICENSE` compatível com o objetivo do repositório.
