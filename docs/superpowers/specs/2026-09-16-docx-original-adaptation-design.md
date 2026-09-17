# Adaptação local de DOCX preservando o original

## Objetivo e estado

Evoluir `tools/docx_layout_probe` para aplicar um conjunto explícito de alterações aprovadas em um DOCX real, preservando a estrutura original e produzindo uma cópia revisável. O primeiro marco é local: reconhecer o documento, aplicar mudanças, gerar o arquivo e conferir o resultado. Especificação aprovada pelo usuário e implementada localmente em 2026-09-16; veja o guia da ferramenta para os limites atuais. A aprovação visual e a adaptação pessoal continuam pendentes.

O usuário aprovou começar pelo fluxo DOCX antes de integrar à aplicação ou ampliar o PDF e confirmou este escopo. Não há autorização neste escopo para instalar software globalmente, criar recursos em nuvem, publicar serviços ou alterar os contratos da API.

## Evidências da situação atual

- A ferramenta .NET 10 usa Open XML SDK 3.5.1 e não faz parte da solução da API.
- `DocxSkillInsertionProbe` acrescenta uma habilidade em categoria explícita, aceita somente títulos `Heading1` e exige confirmação local.
- Existem 16 testes sintéticos e um comando que gera um par de DOCX fictícios. Não são provas de paginação ou adaptação completa.
- O DOCX fornecido para validação usa `SectionHeader`: fonte, cor, negrito e espaçamento próprios, sem `outlineLvl` ou herança de `Heading1`. Não modificar o arquivo para fazê-lo parecer um template suportado.
- O exportador da API recebe `AdaptedText`, não os bytes do original. O protótipo não deve ser conectado à API por uma mudança pontual no controller.
- A renderização local está indisponível por ausência de LibreOffice no runtime. Não apresentar aprovação estrutural como aprovação visual.

## Alternativas e decisão proposta

1. **Editar uma cópia do pacote original com operações delimitadas:** opção escolhida para esta etapa. Preserva estilos e partes não alteradas; permite validação independente de cada operação.
2. Reconstruir o currículo em um template: não atende ao requisito de preservar o layout recebido e não será fallback automático.
3. Converter o PDF para DOCX e trabalhar na conversão: fora deste marco; introduz perda de estrutura que este fluxo não deve esconder.

## Escopo funcional

### 1. Inspeção e seleção dos destinos

Ler o corpo principal, estilos, parágrafos, runs, listas e propriedades de seção. Criar um mapa de blocos endereçáveis, sem reescrever o documento.

- Reconhecer títulos com nível de estrutura definido diretamente ou herdado de estilos.
- Para estilos sem semântica de título, aceitar um mapeamento explícito de estilo para seção. O perfil local poderá declarar `SectionHeader`; não considerar automaticamente todo texto em negrito como título.
- Detectar herança circular ou indefinida de estilos e retornar motivo de revisão em vez de adivinhar.
- Distinguir nome do candidato, título profissional, título de seção, parágrafo de resumo, item de experiência e categoria de habilidades. Alterações no nome/contatos ficam fora deste marco.
- A seleção de um destino combina documento, índice do bloco e conteúdo esperado. Texto repetido não autoriza escolher a primeira ocorrência.
- Documentos com estruturas não suportadas recebem um diagnóstico específico. Não converter ou eliminar tabelas, controles, campos ou alterações controladas silenciosamente.

### 2. Plano explícito de alterações

O motor não cria afirmações profissionais nem chama Gemini. Recebe um plano local revisável com:

- versão do formato do plano;
- SHA-256 dos bytes do original;
- mapeamento de estilos semânticos aprovado para o documento;
- identificador e destino de cada operação;
- conteúdo original esperado;
- operação e conteúdo novo;
- identificação da evidência de origem ou declaração confirmada;
- decisão de aprovação explícita.

O hash impede aplicar um plano a uma versão diferente do arquivo. Os identificadores dos blocos valem para aquele documento, não para todos os currículos futuros. Aprovação local não é autenticação: na integração futura, as decisões canônicas continuarão vindo do backend.

Operações iniciais:

1. Substituir um trecho de texto no título profissional, resumo ou item de experiência existente.
2. Inserir uma habilidade numa categoria existente, preservando o formato da lista e evitando duplicação.

Não incluir ainda remoção/reordenação de seções, mudanças de cargos históricos, datas, empresas, medidas de resultado, criação de experiências ou inserção de parágrafos arbitrários. Esses casos recebem revisão necessária. Priorizar um conjunto pequeno que produza um resultado verdadeiro, não prometer edição universal.

Informações ausentes podem aparecer como sugestões pendentes no fluxo de negócio. Ausência de evidência não impede sugerir; impede incorporar o fato ao currículo sem confirmação. O motor deste marco só aplica as operações já aprovadas.

### 3. Preservação e consistência da aplicação

- Abrir uma cópia em memória com `AutoSave = false`; salvar explicitamente apenas a parte modificada.
- Preservar propriedades de parágrafos, estilos, fontes, cores, espaçamentos, listas, margens, cabeçalhos, rodapés e relacionamentos não envolvidos.
- Para substituição dentro de um run, manter seu estilo e o texto antes/depois do intervalo.
- Para trechos divididos em vários runs de formatação equivalente, distribuir a alteração sem apagar conteúdo fora do intervalo. Não exigir que o texto esteja em um único nó `w:t`.
- Se o trecho atravessar formatações diferentes e não houver correspondência inequívoca para a nova redação, recusar essa operação. Não achatar tudo no estilo do primeiro run.
- Não alterar campos, hyperlinks, revisões controladas, assinaturas, macros ou conteúdo ativo por aproximação. Definir recusas verificáveis para o perfil local; ele não equivale a um sanitizador de uploads públicos.
- Validar todas as operações contra o original antes de modificar a cópia. Recusar intervalos sobrepostos e destinos duplicados conflitantes.
- Se qualquer operação falhar, não entregar um arquivo parcialmente adaptado. A lista de motivos deve identificar a operação sem imprimir texto pessoal em logs.
- Nunca sobrescrever o original ou uma saída existente. Gravar num arquivo temporário próprio e publicar o resultado apenas após a validação estrutural; falhas de gravação devem ser reportadas e os temporários tratados sem apagar arquivos do usuário.

### 4. Saídas e validação

O comando local oferecerá dois passos: inspeção para preparar um plano e aplicação desse plano a uma cópia. A demonstração sintética atual continuará funcionando.

Saídas da aplicação:

- DOCX candidato à revisão;
- relatório local com operações aplicadas/recusadas e estado das verificações;
- indicação explícita `visual_review_pending` até que a aparência seja revisada.

Arquivos pessoais e planos que contenham trechos do currículo ficam em diretório local privado/ignorado pelo Git, nunca em fixtures ou documentação versionada. O console apresenta somente estado, motivos sem conteúdo pessoal e localização dos resultados. Não há upload para provedores de parsing ou IA neste marco.

Validação estrutural obrigatória:

- abrir novamente a saída;
- verificar o esquema Open XML e distinguir eventuais problemas já presentes na entrada dos introduzidos pela edição;
- confirmar todos os textos esperados e ausência de alterações fora dos intervalos autorizados;
- comparar partes não envolvidas, inclusive configurações e estilos;
- confirmar que os bytes da entrada permaneceram intactos.

Validação visual:

- comparar original e adaptado no mesmo renderizador e com as mesmas fontes;
- verificar quebra de linhas, legibilidade, sobreposição, cortes e contagem de páginas;
- preservar a quantidade de páginas quando o conteúdo couber, sem prometer geometria imutável após acrescentar texto;
- se houver página extra, informar e solicitar revisão; não reduzir fontes, remover fatos ou condensar conteúdo sem aprovação;
- não criar um limiar arbitrário de anos de experiência para autorizar segunda página.

Na ausência do renderizador local, produzir apenas um candidato claramente marcado como pendente e permitir revisão manual no Word. Instalação/conteinerização do LibreOffice e validação automática de DOCX para PDF exigem uma decisão própria antes de execução. Exportação PDF deste novo fluxo não faz parte da entrega inicial.

## Organização proposta

Manter o código nesta etapa em `tools/docx_layout_probe`, sem dependência da API ou do banco:

- `DocxDocumentInspector`: leitura da estrutura, estilos e mapa de destinos.
- `DocxAdaptationPlanModel`: plano vinculado ao documento e operações aprovadas.
- `DocxEditOperationModel`: uma alteração e suas precondições.
- `DocxAdaptationEngine`: validação conjunta e aplicação das alterações na cópia.
- `DocxAdaptationResultModel`: resultado e estado de revisão, sem confundir bytes gerados com layout validado.
- `Program.cs`: composição dos comandos locais e gravação segura das saídas.
- `tests/`: fixtures sintéticas independentes do currículo do usuário.

Cada tipo público principal terá arquivo próprio, respeitando as convenções do projeto. Interfaces adicionais só serão criadas se uma fronteira concreta precisar delas. Reaproveitar a edição existente quando suas garantias forem compatíveis, sem manter duas regras divergentes de seleção de categoria.

## Critérios de aceite

1. Um DOCX sintético com títulos `SectionHeader` pode ser inspecionado com mapeamento explícito sem modificar `styles.xml`.
2. Continuam funcionando os casos existentes de `Heading1` e a demonstração de inclusão de habilidade.
3. O motor substitui título/resumo e um trecho de experiência sintéticos em uma execução, sem modificar empresas, datas, contatos ou outros trechos.
4. Trechos fragmentados em runs equivalentes são tratados; formatação mista ambígua é recusada com motivo específico.
5. Hash desatualizado, destino ambíguo, operação não aprovada e operações sobrepostas não produzem saída adaptada.
6. Nenhum teste automatizado utiliza dados pessoais reais. O arquivo real serve somente à verificação manual local autorizada.
7. O original e as partes não envolvidas permanecem inalterados; saída existente não é sobrescrita.
8. O estado de validação visual acompanha o resultado. Sem renderização ou revisão manual concluída, o arquivo não é apresentado como layout aprovado.
9. Não há mudança em scoring, extração/hash da comparação, contratos JSON, autenticação, banco, Docker, frontend ou deploy.

## Sequência após revisão

1. Escrever testes sintéticos de inspeção e reconhecimento de seções; implementar o mapa de blocos.
2. Escrever testes das operações e de suas recusas; implementar aplicação transacional em memória.
3. Implementar os comandos locais e as saídas protegidas por testes de arquivo.
4. Validar o documento real sem gravar conteúdo pessoal no repositório e apresentar as alterações propostas para a vaga.
5. Gerar a cópia após aprovação do conteúdo e revisar no Word ou renderizador autorizado.
6. Só depois desenhar integração com armazenamento privado, API, prévia e download.

## Não incluído

Suporte ampliado a PDF, conversão geral PDF/DOCX, serviço separado publicado, novas chamadas de IA, upload de dados reais, instalação global, integração na aplicação e garantia de layout idêntico em todos os editores. Esses itens não estão concluídos nem são pré-requisitos para validar o primeiro fluxo DOCX local.
