# Arquitetura do ResumeMatcher

## Visão geral

O ResumeMatcher recebe um currículo, extrai seu texto, compara o conteúdo com uma descrição de vaga e devolve uma análise estruturada. O Mock permite execução inteiramente local; o provider Gemini pode ser habilitado por configuração. A deduplicação persistida mantém comparações idênticas consistentes e evita chamadas repetidas ao provider.

## Componentes

```mermaid
flowchart LR
    Browser[React + TypeScript] -->|HTTP /api| Api[ASP.NET Core API]
    Api --> Application[Application]
    Application --> Domain[Domain]
    Infrastructure[Infrastructure] --> Application
    Infrastructure --> Domain
    Infrastructure --> PostgreSQL[(PostgreSQL)]
    Infrastructure --> Extractors[PDF/DOCX Extractors]
    Infrastructure --> Provider[Mock ou Gemini Provider]
```

### ResumeMatcher.Domain

Contém os dados centrais usados pelo negócio:

- currículo extraído;
- análise persistida;
- resultado da análise;
- itens de evidência.

Essa camada não deve depender de ASP.NET Core, Entity Framework, bibliotecas de documentos ou providers externos.

### ResumeMatcher.Application

Contém os casos de uso e os contratos que isolam detalhes externos:

- upload e validação do currículo;
- coordenação da comparação;
- cálculo ponderado da pontuação;
- contratos de repositório, extração e provider de comparação;
- regras de segurança para futuras otimizações.

### ResumeMatcher.Infrastructure

Implementa os contratos da camada Application:

- `ResumeMatcherDbContext` e repositórios com EF Core/PostgreSQL;
- `PdfResumeTextExtractor` com PdfPig;
- `DocxResumeTextExtractor` com Open XML SDK;
- `MockLLMProvider`, usado para validar o pipeline sem API externa;
- `GeminiLLMProvider`, provider real com modelo configurável via Google.GenAI;
- `GeminiRequestExecutor`, responsável por timeout e retry limitado de falhas temporárias;
- registro das dependências de infraestrutura.

### ResumeMatcher.Api

É o ponto de entrada HTTP. Responsabilidades:

- registrar dependências e configurações;
- expor controllers REST;
- aplicar CORS e tratamento centralizado de exceções;
- autenticar ID tokens Firebase (JWT RS256) e autorizar somente a conta Google verificada configurada;
- aplicar rate limiting e limites de entrada;
- expor `/health` com verificação do banco;
- aplicar migrations do EF Core no PostgreSQL durante a inicialização.

### Frontend

Aplicação React criada com Vite. Ela envia o currículo, solicita a comparação e apresenta o score geral, o detalhamento e as evidências retornadas pela API.

O `AuthGate` exige login Google pelo SDK Firebase e consulta `GET /api/auth/session` antes de exibir o formulário. Cada chamada obtém um ID token pelo SDK e o envia no header `Authorization`. A autorização efetiva fica na API, não no componente visual. As chaves públicas para validar a assinatura são descobertas via OIDC do Firebase e gerenciadas pelo middleware JWT do ASP.NET Core.

A política padrão e a política de fallback da API exigem assinatura, emissor, audiência e validade corretos, além de e-mail verificado, provider `google.com` e correspondência com um único `AllowedEmail` configurado. Uma configuração vazia impede a inicialização. O CORS é executado antes de autenticação e autorização para permitir o preflight das origens explicitamente cadastradas.

## Fluxo principal

```mermaid
sequenceDiagram
    participant U as Usuário
    participant W as Frontend
    participant A as API
    participant E as Extrator
    participant D as PostgreSQL
    participant P as Provider
    participant S as Scoring Engine

    U->>W: Seleciona currículo e informa a vaga
    W->>A: POST /api/resumes/upload
    A->>E: Extrai texto do PDF/DOCX
    A->>D: Persiste currículo
    A-->>W: resumeId
    W->>A: POST /api/analysis/compare
    A->>D: Recupera currículo
    A->>A: Normaliza entrada e calcula SHA-256
    A->>D: Procura análise pelo hash
    alt Cache hit
        D-->>A: Resultado persistido
    else Cache miss
        A->>P: Compara currículo e vaga
        A->>S: Calcula pontuação ponderada
        A->>D: Persiste análise e hash
    end
    A-->>W: Resultado estruturado
```

## Pontuação

O `WeightedScoringEngine` normaliza cada dimensão entre `0` e `100` e aplica os pesos configurados:

| Dimensão | Peso padrão |
| --- | ---: |
| Skills | 40% |
| Experiência | 30% |
| Senioridade | 15% |
| Requisitos | 10% |
| Formação | 5% |

A soma precisa ser igual a `1`. Uma configuração inválida impede a criação do serviço.

## Persistência

O MVP usa PostgreSQL com Npgsql e migrations do EF Core. Currículos armazenam o texto extraído; análises armazenam scores, o resultado completo serializado em JSON e um `AnalysisInputHash` SHA-256 hexadecimal opcional de 64 caracteres.

O hash é calculado sobre uma representação canônica composta por texto do currículo normalizado, descrição da vaga normalizada, modelo e configuração relevante do provider, versão do prompt, versão das regras de análise e configuração de scoring. Atualmente a temperatura do Gemini participa do fingerprint. A normalização remove espaços e quebras de linha redundantes, mas preserva pontuação e conteúdo semanticamente relevante.

`AnalysisInputHash` possui índice único. Dentro de uma instância da API, requisições simultâneas com o mesmo hash compartilham a análise em andamento. O índice também trata a corrida de persistência entre instâncias; nesse caso, o resultado vencedor é recarregado. Em múltiplas instâncias ainda pode haver duas chamadas externas antes da disputa de inserção, pois o MVP não usa lock distribuído.

A migration inicial cria o esquema PostgreSQL. A coluna do hash é anulável para que registros legados possam continuar válidos, embora análises antigas sem fingerprint não participem do cache. Dados existentes em arquivos SQLite não são transferidos automaticamente.

`Analysis.ResumeId` possui foreign key para `Resume.Id`. A exclusão de um currículo remove também suas análises. O serviço executa essa remoção explicitamente para manter o mesmo comportamento nos testes e o banco garante a integridade com `ON DELETE CASCADE`.

## Resiliência e limites

- Chamadas ao Gemini possuem timeout configurável e uma nova tentativa por padrão.
- Apenas timeout e falhas temporárias de rede, rate limit ou indisponibilidade são repetidos.
- Erros do provider são convertidos em respostas HTTP `502`, `503` ou `504` conforme a causa.
- A descrição da vaga aceita no máximo 75.000 caracteres.
- O texto extraído do currículo aceita no máximo 200.000 caracteres.
- Controllers da API compartilham um limite configurável de requisições; `/health` permanece fora desse limite.

## Limites de dependência

- `Domain` não referencia nenhum outro projeto.
- `Application` referencia somente `Domain`.
- `Infrastructure` referencia `Application` e `Domain`.
- `Api` referencia `Application` e `Infrastructure`.
- O frontend conversa com o backend somente pela API HTTP.

Não introduza dependências de infraestrutura na camada Domain ou acesso direto ao banco nos controllers.

## Segurança e privacidade

- Extração, scoring e persistência são executados pela aplicação.
- O provider Mock não envia conteúdo para terceiros; o Gemini envia currículo e vaga ao serviço externo quando habilitado.
- Currículos contêm dados pessoais; logs não devem registrar texto integral nem conteúdo de arquivos.
- `DELETE /api/resumes/{id}` permite apagar o currículo e todas as análises relacionadas.
- O uso de IA externa exige consentimento explícito, política de retenção, tratamento de falhas e documentação do provedor.
- Recomendações nunca devem inventar competências, experiências, cargos ou formação.

## Decisões atuais

- **Provider Mock por padrão:** permite validar extração, persistência, UI e pontuação sem custo externo.
- **Gemini opcional:** integração real atrás do mesmo contrato, ativada somente por configuração.
- **Score determinístico:** facilita testes e comparação de resultados.
- **Cache por conteúdo e versão:** garante a mesma resposta persistida para a mesma entrada e invalidação explícita quando modelo, prompt ou regras mudarem.
- **PostgreSQL:** prepara a persistência para evolução de esquema e ambientes compartilhados.
- **Publicação privada inicial:** os dados ficam restritos à conta autorizada pela API; o procedimento para substituir a barreira IAM por autenticação de usuário está em `AUTHENTICATION.md`.
- **Contratos por interface:** permite substituir persistência, extratores e provider sem alterar os casos de uso.
- **API e frontend separados:** mantém as responsabilidades claras e permite implantação independente no futuro.

## Limitações conhecidas

- O vocabulário de skills do Mock é fixo.
- Não há separação de dados por usuário: a autorização é limitada a uma única conta. Não amplie para uma lista de usuários antes de implementar ownership e isolamento do cache.
- Revogação de sessão e desativação de conta no Firebase não são consultadas a cada requisição; um token já emitido pode continuar válido até expirar. A lista de acesso efetiva continua sendo a conta configurada na API.
- Não há política automática de exclusão de currículos.
- Não há migração automática de dados legados do SQLite para PostgreSQL.
- A otimização automática de currículo ainda não está disponível.
- Não há CI/CD nem observabilidade estruturada completa.
- Os testes HTTP usam banco isolado em memória; a migration PostgreSQL é validada separadamente e foi aplicada no ambiente local.
