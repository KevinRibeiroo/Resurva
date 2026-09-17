# Estado atual e roadmap

Este documento separa o que já existe do que está planejado. Itens futuros não devem ser apresentados como funcionalidades prontas no README ou na interface.

## MVP atual

- [x] Backend ASP.NET Core em .NET 10 (C# 14).
- [x] Frontend React 19.2 com TypeScript e Vite 8.
- [x] Upload de PDF e DOCX com limite de tamanho.
- [x] Extração local de texto.
- [x] Persistência com EF Core, PostgreSQL e migrations.
- [x] Cache persistido por conteúdo, modelo e versões de análise.
- [x] Provider Mock para comparação sem serviço externo.
- [x] Score ponderado e determinístico.
- [x] Resultado com evidências, lacunas, forças e recomendações.
- [x] Validador inicial de segurança para otimizações.
- [x] Testes unitários de extração, pontuação e segurança.
- [x] Testes de integração do pipeline HTTP com dependências isoladas.
- [x] Health check, rate limiting e container do backend.
- [x] Login Google no frontend e validação JWT Firebase na API com autorização de uma conta.
- [ ] Concluir ativação do Firebase Authentication e validar o login real após publicar o backend protegido.

## Próximos passos

### Exportação baseada no original (local)

- [x] Motor DOCX integrado à tela de adaptação, com seleção de destinos, confirmação canônica e download.
- [x] Reenvio em memória, ownership/retenção, hash/versão e recusa de layouts incompatíveis.
- [ ] Validar renderização/paginação e conversão PDF com fontes controladas antes de promover para produção.
- [ ] Definir armazenamento seguro de originais e disponibilidade fora da sessão.

### 1. Consolidar a base técnica

- [x] Aplicar integralmente a convenção de arquivos `Entity`, `Model` e interfaces `I...`.
- [ ] Separar tipos públicos que ainda estejam agrupados em arquivos genéricos.
- [ ] Adicionar análise estática, formatação e validações no build.
- [x] Atualizar o Vite: atualizado para Vite 8.2.2 e React 19.2.8.
- [x] Criar testes de integração para os endpoints HTTP.
- [x] Versionar CI para build/testes backend e typecheck/build frontend, com PostgreSQL descartável.
- [x] Automatizar CD do frontend no Firebase Hosting via GitHub Actions ao mergear na `develop`.
- [ ] Publicar/validar o workflow no GitHub e ativar checks obrigatórios/proteção das branches.
- [ ] Adicionar testes de comportamento do frontend.

### 2. Provider de IA real

- [x] Implementar um provider real atrás de `ILLMProvider` (`GeminiLLMProvider`).
- [x] Definir schema estruturado e validar toda resposta do modelo.
- [x] Configurar timeout, retry com limite e tratamento de indisponibilidade.
- [x] Armazenar chaves somente em Secret Manager ou variáveis de ambiente.
- [ ] Informar ao usuário quando o currículo for enviado a um serviço externo.
- [x] Manter o Mock disponível para desenvolvimento e testes.

### 3. Persistência e privacidade

- [x] Substituir `EnsureCreatedAsync` por migrations do EF Core.
- [ ] Planejar e executar a transferência de dados SQLite legados, caso precisem ser preservados.
- [x] Definir retenção: 30 dias desde última atualização, exclusão de dados junto da conta e até 30 dias nos backups.
- [x] Implementar expiração de leitura/cache e comando de limpeza física, com testes.
- [ ] Agendar a limpeza, definir sua frequência e alertas, verificar retenção de backups e reconciliar exclusões após restore.
- [ ] Implementar encerramento completo da conta (Firebase, sessões, dados e recuperação de falhas). Operação interna de apagar dados já existe, sem endpoint de encerramento.
- [x] Disponibilizar exclusão manual de currículo e análises relacionadas.
- [ ] Evitar persistir texto integral quando ele não for necessário.
- [x] Implementar autenticação para o ambiente privado de um usuário.
- [x] Implementar propriedade de currículos/análises e isolamento de consultas, exclusão e cache por UID, com testes HTTP/PostgreSQL.
- [ ] Revisar atribuição dos dados legados ao UID e fazer rollout controlado da migration.
- [ ] Definir proteção de dados, auditoria e estratégia de backup.

### 4. Experiência do usuário

- [ ] Melhorar feedback de upload, progresso e mensagens de erro.
- [ ] Adicionar histórico de análises.
- [ ] Permitir ajustar pesos da comparação com limites seguros.
- [ ] Explicar como cada score foi calculado.
- [ ] Melhorar acessibilidade e experiência em dispositivos móveis.

### 5. Otimização responsável do currículo

- [x] Sugerir melhorias de redação baseadas apenas em fatos existentes.
- [x] Destacar alterações factuais para confirmação explícita.
- [x] Bloquear sugestões sem evidência quando forem apresentadas como verdade.
- [x] Manter histórico das sugestões aceitas e rejeitadas e controle de versão contra conflitos.
- [x] Gerar e exibir a versão adaptada em texto puro com cópia para área de transferência.
- [ ] Exportação com diagramação/estilização visual em PDF/DOCX (próxima fase).

## Planos futuros

- [ ] Permitir comparação como visitante, com armazenamento temporário aprovado. Prazo temporário, limites de uso/custo e proteção contra abuso ainda a definir; nenhuma quota anônima foi escolhida.

- [ ] Suporte a mais idiomas.
- [ ] Comparação de um currículo com várias vagas.
- [ ] Perfis de pontuação por área profissional.
- [ ] Exportação de relatórios em PDF.
- [ ] Métricas agregadas sem exposição de dados pessoais.
- [ ] Implantação conteinerizada e ambientes separados.

## Fora do escopo imediato

- Tomar decisões de contratação.
- Afirmar que o score representa probabilidade real de contratação.
- Inventar experiências ou competências para aumentar a aderência.
- Treinar modelos com currículos dos usuários sem consentimento explícito.

## Como atualizar este roadmap

- Marque um item como concluído somente quando código, testes e documentação estiverem prontos.
- Registre novas decisões arquiteturais em `docs/ARCHITECTURE.md`.
- Mova ideias sem compromisso para “Planos futuros”.
- Não use o roadmap como substituto para issues detalhadas.
