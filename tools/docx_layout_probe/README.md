# Adaptação DOCX local

Ferramenta experimental .NET 10/Open XML SDK. Edita uma cópia do pacote original, **não reconstrói um template**. Não inicia a API, não conecta banco, não chama IA e não está integrada ao frontend ou ao exportador publicado.

## Executar

Na raiz do repositório:

```powershell
dotnet test tools/docx_layout_probe/tests/DocxLayoutProbe.Tests.csproj
dotnet run --project tools/docx_layout_probe -- --help
```

### 1. Inspecionar

Crie um perfil JSON local, por exemplo `.artifacts/profile.json`:

```json
{
  "headingStyleIds": ["SectionHeader"],
  "blockRoles": {}
}
```

O perfil explicita que esse estilo representa títulos de seção. Não transforma o documento ou seus estilos. Títulos com `outlineLvl` direto/herdado e `Heading1` definido também são reconhecidos. Um perfil vazio não adivinha que negrito/cor significa título.

```powershell
dotnet run --project tools/docx_layout_probe -- inspect "C:\caminho\entrada.docx" .artifacts/profile.json .artifacts/inspecao-1
```

Cria `inspection.json` (mapa de blocos) e `plan.json` (hash/perfil e operações vazias). A pasta de saída **não pode existir** e sua pasta pai precisa existir. Os IDs `p:0`, `p:1` etc são índices dos parágrafos do corpo e só valem em conjunto com o hash daquela entrada.

Nome/contatos são protegidos. O preâmbulo não é editável automaticamente. Depois de conferir o mapa, o perfil pode declarar, por exemplo, `"blockRoles": {"p:1": "professional_title"}` para um título profissional real no preâmbulo; reinspecione em outra pasta. Essa declaração não libera o primeiro parágrafo com conteúdo, contatos, datas, títulos de seção, campos ou cabeçalhos de emprego.

### 2. Revisar o plano

Edite o `plan.json` gerado. Exemplo **fictício**, que só funciona se o mapa realmente contiver esse texto/bloco:

```json
{
  "version": 1,
  "sourceSha256": "COPIAR_O_HASH_DA_INSPECAO",
  "profile": {"headingStyleIds": ["SectionHeader"], "blockRoles": {}},
  "operations": [
    {
      "id": "resumo-1",
      "blockId": "p:4",
      "expectedText": "API em C#",
      "kind": "replace_text",
      "start": 0,
      "length": 3,
      "newText": "API REST",
      "approved": false,
      "evidenceIds": ["REFERENCIA_DA_EVIDENCIA_OU_CONFIRMACAO"]
    }
  ]
}
```

- `expectedText`: texto completo e exato do bloco na inspeção; não apenas o trecho.
- `start`/`length`: intervalo do trecho nas coordenadas originais, zero-based, em unidades UTF-16 (.NET). Não contar bytes. O motor recusa cortar pares substitutos de caracteres.
- `replace_text`: somente título profissional explicitamente mapeado, resumo ou item de experiência; não permite remoção vazia ou criação de parágrafos.
- `insert_skill`: somente categoria existente de habilidades; informe `start: 0`, `length: 0` e `newText` com uma habilidade única. A categoria precisa ter dois pontos e lista simples com vírgulas ou ponto e vírgula, com o último nó de texto separado do rótulo. Preserva pontuação e formatação do último trecho; listas ambíguas, conjunções e duplicatas pedem revisão. Uma inclusão por categoria em cada plano; inclusões concorrentes no mesmo trecho são recusadas.
- `id`: único, composto por letras ASCII, números, `_` ou `-`; use identificadores técnicos, não dados pessoais.
- `evidenceIds`: referências declaradas para revisão humana. **O motor não verifica a veracidade da evidência nem da experiência profissional.**
- `approved`: só mudar para `true` após aprovação real do conteúdo. Um arquivo JSON local não representa autorização autenticada da API.

Nenhuma sugestão ausente deve ser apresentada como fato sem confirmação. Não utilizar o plano para alterar empresa, cargo histórico, período ou resultados. Cabeçalhos de experiência são protegidos; alterações nos tokens numéricos dos itens são recusadas, mas isso não substitui revisão factual da redação.

### 3. Gerar a cópia

```powershell
dotnet run --project tools/docx_layout_probe -- apply "C:\caminho\entrada.docx" .artifacts/inspecao-1/plan.json .artifacts/resultado-1
```

Produz `adaptado.docx` e `report.json`. Todas as precondições são verificadas antes de alterar a cópia em memória. Na gravação, os arquivos são preparados numa pasta temporária irmã e publicados por movimentação para destino novo; falhas tratadas/cancelamento limpam somente a pasta temporária da operação. Uma interrupção abrupta do processo/sistema pode deixar `.docx-stage-*` órfã, que também contém dados privados. Nunca sobrescreve o original ou uma saída existente.

O relatório registra operações aplicadas, quantidade de erros de schema preexistentes e `visual_review_pending`, sem texto do currículo. As falhas imprimem um código seguro, sem conteúdo pessoal; não publicam um DOCX parcial.

## Garantias e limites

- Hash SHA-256, versão do plano, destino, conteúdo esperado, aprovação, referência de evidência e ausência de sobreposição são obrigatórios.
- Preserva propriedades de parágrafo/run, estilos, margens, listas e demais partes. Todas as partes fora do documento principal são comparadas byte a byte.
- O XML principal é comparado semanticamente: apenas nós de texto previstos e `xml:space="preserve"` necessário para seus espaços podem mudar. Declarações de namespace não afetam essa comparação.
- Substituições podem atravessar runs com propriedades equivalentes. Propriedades diferentes são recusadas, mesmo quando um renderizador poderia produzir aparência parecida.
- Reabre a saída, verifica os textos esperados e compara erros de schema por identificador, parte, caminho e descrição. Erros preexistentes inalterados são reportados; erros novos bloqueiam a saída.
- Tabelas, múltiplas colunas declaradas, proteção documental, assinaturas, macros, partes ativas e controles não têm suporte. Campos, hyperlinks e revisões no parágrafo impedem sua edição; não são achatados.
- É um perfil restrito para documentos locais conhecidos, **não um sanitizador de upload público**, parser universal ou validador semântico de currículos. Não possui quotas de processamento para arquivos hostis.
- Planos/mapas contêm dados pessoais. `.artifacts/` é ignorada pelo Git, mas isso não oferece criptografia/controle de acesso nem impede sincronização do diretório. Mantenha-os num local privado e exclua-os quando não forem necessários.

## Revisão visual obrigatória

Código `0` significa inspeção/geração concluída, **não layout aprovado**. Código `2` significa falha/necessidade de revisão. Não há renderização automática, exportação PDF ou garantia de uma página nesta etapa.

Abra original e adaptado no Word com as mesmas fontes. Confira fontes, espaços, alinhamentos, quebras, cortes, sobreposição e páginas. Se surgir outra página, revise o conteúdo; a ferramenta não encolhe fontes, remove fatos ou estabelece um limite arbitrário de experiência profissional.

## Demonstração anterior

```powershell
dotnet run --project tools/docx_layout_probe -- .artifacts/demo-novo
```

Continua gerando dois DOCX fictícios, com uma inclusão de habilidade. Esse comando legado pode deixar saída parcial em falha de disco; os novos comandos `inspect`/`apply` utilizam a publicação conjunta descrita acima.
