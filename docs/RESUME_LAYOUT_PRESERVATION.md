# Preservação de layout: prova técnica local

## Estado desta etapa

Prova isolada em `tools/layout_probe`, não integrada à API, ao frontend, à persistência ou ao CI/CD. **A exportação publicada continua usando o exportador anterior.** Não há promessa de fidelidade para qualquer PDF, conversão PDF/DOCX ou reflow nesta etapa.

Branch: `codex/feat-preservar-layout-curriculo`, criada da `feat/ajustando-cdg-para-owsap` no commit `9683119caf0374d6541eac913e113dc015679bf7`. O PR de segurança permanece independente. Não houve push, merge ou alteração dos painéis. Para revisar antes do merge de segurança, comparar esta branch com a branch de segurança; se aquele PR entrar por squash, reconciliar os commits antes de redirecionar o PR para develop.

## Decisão comprovada

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

1. Escolher o motor de produção para edição localizada e para reflow, verificando licença, fontes, Linux/Cloud Run, recursos e limites de segurança. Não assumir que este spike resolve reconstrução geral.
2. Definir armazenamento privado do original: interface na Application, implementação na Infrastructure, ownership e expiração/exclusão conjunta. A escolha do serviço e os custos precisam de aprovação. Registros antigos exigirão reupload; não é possível recuperar o layout a partir do texto puro atual.
3. Criar representação de conteúdo + layout com IDs estáveis de seção/bloco, versão do documento e origem da informação. Não mudar a extração/hash da comparação existente inadvertidamente.
4. Substituir inclusões genéricas ao final do texto por operações canônicas de inserir/alterar em seção/categoria, preservando confirmação explícita, declarações e controle de concorrência.
5. Implementar reflow medido por fonte e espaço. Priorizar uma página sem apagar informações ou diminuir a legibilidade automaticamente. Resumos/condensação e ajustes visuais precisam de revisão; não escolher um limiar arbitrário de anos de experiência para liberar segunda página.
6. Para DOCX, preservar estilos, parágrafos, listas e seções do original e validar paginação no renderizador escolhido. Não prometer paginação idêntica em todos os editores.
7. Integrar prévia do artefato efetivamente exportado, indicação de fidelidade e avisos de alterações de layout/página. Não usar fallback silencioso para o template genérico.
8. Ampliar fixtures sintéticas (fontes, Unicode, layout denso, múltiplas páginas, erros), revisão visual e testes de autorização, retenção e exclusão antes de ativar a funcionalidade na API.
