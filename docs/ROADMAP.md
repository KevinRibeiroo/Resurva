# Estado atual e roadmap

Este documento separa o que já existe do que está planejado. Itens futuros não devem ser apresentados como funcionalidades prontas no README ou na interface.

## MVP atual

- [x] Backend ASP.NET Core em .NET 8.
- [x] Frontend React com TypeScript e Vite.
- [x] Upload de PDF e DOCX com limite de tamanho.
- [x] Extração local de texto.
- [x] Persistência local com EF Core e SQLite.
- [x] Provider Mock para comparação sem serviço externo.
- [x] Score ponderado e determinístico.
- [x] Resultado com evidências, lacunas, forças e recomendações.
- [x] Validador inicial de segurança para otimizações.
- [x] Testes unitários de extração, pontuação e segurança.

## Próximos passos

### 1. Consolidar a base técnica

- [x] Aplicar integralmente a convenção de arquivos `Entity`, `Model` e interfaces `I...`.
- [ ] Separar tipos públicos que ainda estejam agrupados em arquivos genéricos.
- [ ] Adicionar análise estática, formatação e validações no build.
- [ ] Criar testes de integração para os endpoints HTTP.
- [ ] Configurar CI para build e testes de backend e frontend.

### 2. Provider de IA real

- [x] Implementar um provider real atrás de `ILLMProvider` (`GeminiLLMProvider`).
- [x] Definir schema estruturado e validar toda resposta do modelo.
- [ ] Configurar timeout, retry com limite e tratamento de indisponibilidade.
- [x] Armazenar chaves somente em Secret Manager ou variáveis de ambiente.
- [ ] Informar ao usuário quando o currículo for enviado a um serviço externo.
- [x] Manter o Mock disponível para desenvolvimento e testes.

### 3. Persistência e privacidade

- [ ] Substituir `EnsureCreatedAsync` por migrations do EF Core.
- [ ] Definir política de retenção e exclusão de currículos.
- [ ] Evitar persistir texto integral quando ele não for necessário.
- [ ] Implementar autenticação antes de armazenar dados de múltiplos usuários.
- [ ] Definir proteção de dados, auditoria e estratégia de backup.

### 4. Experiência do usuário

- [ ] Melhorar feedback de upload, progresso e mensagens de erro.
- [ ] Adicionar histórico de análises.
- [ ] Permitir ajustar pesos da comparação com limites seguros.
- [ ] Explicar como cada score foi calculado.
- [ ] Melhorar acessibilidade e experiência em dispositivos móveis.

### 5. Otimização responsável do currículo

- [ ] Sugerir melhorias de redação baseadas apenas em fatos existentes.
- [ ] Destacar alterações factuais para confirmação explícita.
- [ ] Bloquear sugestões sem evidência quando forem apresentadas como verdade.
- [ ] Manter histórico das sugestões aceitas e rejeitadas.
- [ ] Exportar uma versão revisada em formato apropriado.

## Planos futuros

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
