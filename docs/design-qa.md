# Acervo — verificação do redesign

Referência: opção 1 verde, segunda rodada, aprovada pelo usuário em 18/09/2026.
Implementação: frontend existente, com prévia isolada em `/tests/visual/index.html` e dados fictícios.

## Revisão e ajustes

- A primeira comparação visual mostrou a folha de evidências sem a borda verde e com citação serifada demais. Corrigido: borda verde, aba superior e texto de evidência em Public Sans.
- O cabeçalho da prévia transbordava em 320px. Corrigido com quebra de linha; o formulário passou a medir 310px de conteúdo para 320px de viewport.
- Navegação envolvia botões em links. Substituída por `ButtonLink`, com um único elemento interativo e os mesmos estilos.
- O teste antigo buscava um nome de competência agora presente no botão e no painel. Ajustado para buscar o botão por papel e nome acessível.

## Fidelidade visual

Referência e captura final foram abertas juntas no mesmo retorno de ferramenta. A referência é um mockup de 1487 × 1058; a inspeção final usa o painel do navegador, aproximadamente 795 × 1203. Avaliação de direção, componentes e responsividade; não uma alegação de equivalência pixel a pixel entre dimensões distintas.

- Tipografia: Newsreader na marca e títulos; Public Sans nos controles, requisitos e evidências. Fonte serifada limitada à hierarquia editorial.
- Espaçamento: duas colunas no desktop, folha branca à direita e requisitos com divisórias; no celular, cada evidência abre abaixo do requisito selecionado.
- Cores: fundo verde claro, masthead floresta, texto verde escuro e ações verdes. Gradientes e brilho do tema antigo removidos dos componentes.
- Imagens: esta direção não depende de fotografias ou ilustrações. Marca textual e ícones de interface existentes.
- Conteúdo: cargo e arquivo fictícios do mockup não foram inventados no produto. Título genérico, dimensões oficiais e observações retornadas pela API. A aba de texto completo foi substituída por navegação para pontuação e observações, pois o resultado não fornece o currículo completo.

## Verificações

- TypeScript e build Vite aprovados.
- 29 testes em 9 arquivos aprovados: autenticação, envio, resultado, evidências por teclado, ausência de evidências, confirmações, adaptação, exportação de layout e sessão do original.
- Resultado, envio, landing e login inspecionados no navegador. Inspeção de larguras de 320, 390, 768 e 1440px durante o desenvolvimento.
- API autenticada e exportações reais não foram executadas na verificação visual; a prévia usa dados fictícios e não remove a proteção da aplicação.
- Auditor estático premium executado. Identificou regras de posse do select, navegação aninhada, textareas e scrollbars. Posse documentada; navegação e scrollbars corrigidos; resize controlado nos CSS Modules. O auditor não reconhece todas as associações entre JSX e CSS Modules; os testes e a revisão do código complementam esse relatório.

Resultado: direção visual aplicada e fluxos de frontend verificados. A validação integrada com a conta autorizada continua separada da prévia visual.
