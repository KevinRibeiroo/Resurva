# Segurança

Não publique senhas, tokens, chaves privadas, dados pessoais ou currículos em issues, pull requests, logs de CI ou exemplos. Use documentos sintéticos nos testes e armazene arquivos pessoais em `private-data/`, ignorado pelo Git.

Configure credenciais por variáveis de ambiente ou gerenciadores de segredos. Variáveis `VITE_*` são incorporadas ao JavaScript público: use somente configuração pública do Firebase e endereços de serviços, nunca chaves Gemini ou conexão de banco. Restrinja a chave pública Firebase às APIs necessárias; não permita a Generative Language API nessa chave.

Antes de enviar commits, revise os arquivos preparados com `git diff --cached`. O `.gitignore` previne inclusões acidentais, mas não remove arquivos já versionados nem substitui a revisão. Para evitar expor o e-mail pessoal, configure o endereço `noreply` fornecido pelo GitHub como `user.email` neste repositório.

Ao descobrir uma credencial exposta, revogue ou troque a credencial primeiro. A remoção do arquivo atual não apaga o histórico. A limpeza deve incluir branches e tags afetadas; referências antigas de pull requests e caches do GitHub podem exigir intervenção do suporte. Não mescle nem envie branches de clones anteriores à limpeza, pois isso pode reintroduzir o histórico removido.

Para relatar uma vulnerabilidade, use o relato privado na aba Security do GitHub quando estiver habilitado. Não inclua dados reais ou credenciais numa issue pública.

O código público não concede acesso aos dados da aplicação. A API deve continuar exigindo o token Firebase válido e a conta autorizada. Leia [Autenticação](docs/AUTHENTICATION.md) e [Privacidade](docs/DATA_PRIVACY.md) antes de alterar a implantação.
