# Preservação de layout: prova técnica local

## Estado desta etapa

O DOCX agora está integrado à **aplicação local**, com motor compartilhado em `ResumeMatcher.DocumentLayout` e download pela tela após confirmar/aplicar o plano. [Como testar, contrato e limites](LOCAL_LAYOUT_TEST.md). A CLI em `tools/docx_layout_probe` foi mantida; PDF continua isolado em `tools/layout_probe`. **Não houve deploy: a exportação publicada continua usando o exportador anterior.** Não há promessa de fidelidade para qualquer PDF, conversão PDF/DOCX ou reflow.

## Direção aprovada: ferramentas gratuitas e camada própria

- DOCX: editar o documento original com Open XML SDK, sem reconstruir um template a partir de texto puro. LibreOffice headless é o candidato à renderização/conversão para PDF, ainda não validado neste ambiente.
- PDF: manter a pesquisa com pypdf para edições localizadas e explícitas. Não converter arbitrariamente PDF para DOCX prometendo o mesmo layout.
- A camada própria coordenará alterações confirmadas, identificação de blocos e validação. Não construir um renderizador completo do zero.
- Um serviço de documentos isolado é a direção futura, não uma infraestrutura já criada. A API .NET continuará responsável por autenticação, ownership e confirmação. Python não foi adicionado ao backend nem ao deploy.
- Bibliotecas gratuitas não tornam computação, armazenamento ou operação gratuitos. Licenças, versões, fontes disponíveis e custos do serviço deverão ser verificados antes da produção.

## Prova DOCX: preservação estrutural, não visual

`tools/docx_layout_probe` é uma ferramenta de console experimental .NET 10 com Open XML SDK 3.5.1 e suíte independente. A aplicação não referencia seu executável: CLI e Infrastructure usam a biblioteca `ResumeMatcher.DocumentLayout`, incluída na solução.

```powershell
dotnet test tools/docx_layout_probe/tests/DocxLayoutProbe.Tests.csproj
```

### Gerar arquivos para comparar no Word

Na raiz do repositório:

```powershell
dotnet run --project tools/docx_layout_probe -- .artifacts/docx-layout-demo
```

O comando cria uma **pasta nova** com `original.docx` e `adaptado.docx`. São exemplos com dados inteiramente fictícios, não o currículo pessoal do usuário. No adaptado, a única mudança de conteúdo é `Bancos de Dados: SQL Server, MySQL.` → `Bancos de Dados: SQL Server, MySQL, PostgreSQL.`. A confirmação se aplica somente à informação inventada desta demonstração; não autoriza adaptações em documentos reais.

Abra os dois no Word, de preferência lado a lado em layout de impressão. Compare categoria, fonte, negrito, espaçamento, margens, demais seções e quantidade de páginas. A demonstração não garante que toda inclusão futura caiba em uma página.

A pasta de destino não pode existir; para repetir, use por exemplo `.artifacts/docx-layout-demo-2`. Arquivos são abertos com `CreateNew`, nunca sobrescritos. Uma falha de disco/permissão durante a gravação pode deixar saída parcial; o console avisa para escolher uma pasta nova. Código de saída `0` significa geração concluída, **não aprovação visual**; `2` significa uso inválido/falha. `--help` mostra a ajuda. Não é necessário iniciar a API, frontend, banco ou Gemini. `.artifacts` é ignorado pelo Git.

Os 16 testes iniciais da demonstração foram mantidos e ampliados pela suíte de inspeção/aplicação descrita abaixo. A tentativa de renderização automática parou por ausência de `soffice.exe`. Os DOCX são amostras para revisão manual, não exportações homologadas.

`DocxSkillInsertionProbe.Insert` recebe bytes do original, seção, categoria, habilidade única e confirmação explícita de demonstração. Trabalha numa cópia em memória; não grava arquivos, não infere habilidades e não chama IA. O retorno é apenas candidato a exportação e **sempre exige validação de renderização**.

Perfil inicial: título de seção com estilo `Heading1`, categoria única em parágrafo com texto/runs simples, lista separada por vírgulas ou ponto e vírgula. Altera apenas o último nó de texto da lista, herdando seu estilo e preservando a pontuação. Recusa tabela, categoria ambígua, seção não reconhecida, revisão controlada no destino, documento protegido, múltiplas colunas declaradas e habilidade já existente. Não é parser universal nem sanitizador de arquivos não confiáveis.

Os 14 testes sintéticos validam a confirmação, os limites de seção, Unicode, pontuação, as recusas e a preservação. O teste principal compara todos os outros arquivos do pacote byte a byte, incluindo configurações, compara o XML do documento permitindo apenas a alteração textual prevista (ignorando realocação de declarações de namespace pelo SDK) e valida o esquema Open XML. A entrada permanece inalterada. O salvamento automático do SDK fica desabilitado para evitar reserializar configurações apenas consultadas; somente o documento alterado é salvo explicitamente.

**Pendente:** LibreOffice não está disponível no runtime local; não houve validação visual ou de paginação DOCX. Preservar XML não garante que uma inclusão caiba na linha/página. Antes de promover a integração local para produção, validar DOCX → PDF → imagens com fontes controladas e detectar overflow sem reduzir fonte ou remover conteúdo silenciosamente. A edição de PDF continua separada.

### Evolução local: inspeção e aplicação de plano aprovado

Implementados `DocxDocumentInspector`, `DocxAdaptationEngine` e os comandos `inspect`/`apply`. Instruções, contratos JSON locais e exemplos: [guia do protótipo DOCX](../tools/docx_layout_probe/README.md).

- Reconhece títulos por outline direto/herdado ou perfil explícito, inclusive `SectionHeader`, sem reescrever os estilos.
- Gera blocos endereçáveis vinculados ao SHA-256 do original. Nome/contatos, cabeçalhos de emprego e conteúdos não suportados não são liberados para edição.
- Aplica substituições em título profissional/resumo/itens de experiência e uma inclusão em categoria de habilidades, com conteúdo esperado, evidência declarada e aprovação. Não gera afirmações nem comprova automaticamente a veracidade das referências.
- Valida o plano inteiro, recusa sobreposições e formatação ambígua, reabre o resultado, compara schema antes/depois e verifica partes/XML não autorizados.
- Publica `adaptado.docx` e relatório juntos numa pasta nova. Nunca sobrescreve entrada/saída. O estado continua `visual_review_pending`.
- Os testes são inteiramente fictícios. A inspeção manual do DOCX autorizado reconheceu seções e blocos com perfil explícito; nenhum currículo pessoal adaptado foi gerado nesta entrega. O conteúdo precisa de aprovação específica antes disso.

A suíte local possui 53 testes aprovados na verificação de 2026-09-16. A revisão local encontrou e corrigiu edições indevidas em revisões de formatação, numeração desativada e comparação de espaços. A revisão independente identificou alteração de números por pontuação e falta do ID da operação no diagnóstico; ambos foram reproduzidos, corrigidos e aprovados em nova revisão focada. Os comandos de demonstração, inspeção e aplicação também foram executados com dados fictícios, produzindo um candidato e relatório sem novos erros de schema. Nada deste fluxo foi integrado à aplicação publicada.

Branch: `codex/feat-preservar-layout-curriculo`, criada da `feat/ajustando-cdg-para-owsap` no commit `9683119caf0374d6541eac913e113dc015679bf7`. O PR de segurança permanece independente. Não houve push, merge ou alteração dos painéis. Para revisar antes do merge de segurança, comparar esta branch com a branch de segurança; se aquele PR entrar por squash, reconciliar os commits antes de redirecionar o PR para develop.

## Decisão comprovada no PDF

Para um PDF digital simples, uma habilidade pode ser inserida **na categoria existente**, preservando a página original, fontes incorporadas, tamanhos, cores, divisórias e demais operações de conteúdo. A ferramenta altera um único operando `Tj`; não cobre o texto antigo com um retângulo e não rasteriza a página.

O destino é informado explicitamente: seção + categoria. Por exemplo, inserir uma habilidade confirmada em uma lista de bancos de dados. O programa não infere competência profissional, não usa IA e não associa habilidades a empregadores.

A medição usa as métricas da própria fonte, não um limite de caracteres. Se houver espaço, a inclusão permanece na mesma linha. Se não houver, retorna `review_required`, sem reduzir fontes, cortar conteúdo, mudar margens ou criar outra página. A confirmação por flag é somente um mecanismo de demonstração local, **não substitui autenticação ou validação canônica do backend**.

## Limites explícitos

- Uma página, texto digital horizontal, sem crop/rotação.
- Categoria com corpo em uma linha, estilo uniforme e identificação única.
- Fonte TrueType de um byte com `ToUnicode` em `bfchar` e métricas compatíveis.
- Todos os caracteres novos já precisam existir na fonte incorporada; fontes subset frequentemente não os contêm.
- Links comuns fora da linha alterada são preservados. Formulários, assinaturas, XObjects/imagens, links na linha e conteúdo marcado precisam de outro tratamento.
- Sem suporte geral a colunas, categorias multilinha, tabelas, ligaturas, operações `TJ`, OCR, realocação de blocos ou DOCX.
- A identificação de seção usa títulos em linha própria e é heurística; não é um parser universal de currículos.
- Uma alteração por execução; fluxos com várias sugestões exigirão plano versionado, análise conjunta de colisões e validação do resultado.
- A prova não é uma barreira de segurança para PDFs arbitrários. Não expor como endpoint nem utilizar diretamente em produção.

## Tecnologia e execução

Python é usado **apenas como ferramenta local de pesquisa**, aproveitando o runtime disponível. Não foi acrescentado à stack .NET/React, ao Docker ou à publicação. A tentativa com a biblioteca .NET existente mostrou que operações brutas de streams copiados não ficam disponíveis para edição; o spike usa pypdf para comprovar a estratégia antes da escolha do motor definitivo.

Dependências reproduzíveis em `tools/layout_probe/requirements.txt`: pypdf 6.10.0, pdfplumber 0.11.9 e reportlab 4.4.9. Instalar, se necessário, em ambiente virtual isolado; não instalar globalmente. O reportlab gera apenas fixtures sintéticas. A edição usa os recursos originais do PDF.

Na raiz do repositório, com o Python desse ambiente:

```powershell
python -m unittest discover -s tools/layout_probe -v
python tools/layout_probe/create_demo.py .artifacts/layout-probe/demo
python tools/layout_probe/layout_probe.py .artifacts/layout-probe/demo/original-sintetico.pdf --section "HABILIDADES TECNICAS" --category "Bancos de Dados" --skill "PostgreSQL"
```

O último comando verifica viabilidade, sem criar saída. Para escrever uma cópia de uma inclusão **realmente confirmada**, adicionar `--confirmed --output CAMINHO_NOVO.pdf`. A saída deve estar em diretório existente e não pode existir previamente. O original nunca é sobrescrito. Saída `0` indica viabilidade/geração; `2` indica revisão necessária. As mensagens não incluem texto do currículo.

O comando de demonstração cria `original-sintetico.pdf` e `adaptado-sintetico.pdf` numa pasta nova. Recusa sobrescrever arquivos existentes; use outra pasta para repetir. Os exemplos em `.artifacts` são ignorados pelo Git. Não adicionar currículos reais, screenshots pessoais ou arquivos gerados ao repositório.

## Evidências locais desta etapa

- 14 testes sintéticos aprovados, incluindo preservação das demais operações, fontes incorporadas e dimensão Letter; confirmação, duplicação, ambiguidade, falta de espaço, caractere ausente, links e proteção do arquivo original.
- As duas páginas sintéticas foram renderizadas com Poppler e inspecionadas visualmente; a mudança é restrita à linha de bancos de dados, sem sobreposição ou mudança de página.
- No PDF fornecido para análise pelo usuário, a checagem sem gravação localizou a categoria e estimou que a habilidade de exemplo cabe na linha, em página de 612 x 792 pontos, com aproximadamente 34,51 pontos restantes. Isso **não confirma a habilidade**, não gera currículo pessoal adaptado e não comprova suporte a todos os layouts.
- Build .NET: sem erros/avisos. Suíte existente: 134 aprovados, 1 teste PostgreSQL ignorado por não haver instância descartável configurada. Não houve execução contra Supabase ou Cloud Run.

## Próximas etapas, ainda não implementadas

1. Validar a composição gratuita candidata (Open XML SDK/LibreOffice para DOCX; pypdf para PDF localizado), verificando licença, fontes, Linux/Cloud Run, recursos e limites de segurança. Não assumir que os spikes resolvem reconstrução geral ou que LibreOffice já foi homologado.
2. Definir armazenamento privado do original: interface na Application, implementação na Infrastructure, ownership e expiração/exclusão conjunta. A escolha do serviço e os custos precisam de aprovação. Registros antigos exigirão reupload; não é possível recuperar o layout a partir do texto puro atual.
3. Criar representação de conteúdo + layout com IDs estáveis de seção/bloco, versão do documento e origem da informação. Não mudar a extração/hash da comparação existente inadvertidamente.
4. Substituir inclusões genéricas ao final do texto por operações canônicas de inserir/alterar em seção/categoria, preservando confirmação explícita, declarações e controle de concorrência.
5. Implementar reflow medido por fonte e espaço. Priorizar uma página sem apagar informações ou diminuir a legibilidade automaticamente. Resumos/condensação e ajustes visuais precisam de revisão; não escolher um limiar arbitrário de anos de experiência para liberar segunda página.
6. Para DOCX, preservar estilos, parágrafos, listas e seções do original e validar paginação no renderizador escolhido. Não prometer paginação idêntica em todos os editores.
7. Integrar prévia do artefato efetivamente exportado, indicação de fidelidade e avisos de alterações de layout/página. Não usar fallback silencioso para o template genérico.
8. Ampliar fixtures sintéticas (fontes, Unicode, layout denso, múltiplas páginas, erros), revisão visual e testes de autorização, retenção e exclusão antes de ativar a funcionalidade na API.
