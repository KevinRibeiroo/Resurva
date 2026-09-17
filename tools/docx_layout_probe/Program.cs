using DocxLayoutProbe;

if (args.Length > 0 && args[0] is "inspect" or "apply")
{
    using var cancellation = new CancellationTokenSource();
    Console.CancelKeyPress += (_, e) => { e.Cancel = true; cancellation.Cancel(); };
    return await DocxLocalCommands.RunAsync(args, Console.Out, Console.Error, cancellation.Token);
}

if (args.Length != 1 || args[0] is "--help" or "-h")
{
    Console.WriteLine("Uso: dotnet run --project tools/docx_layout_probe -- PASTA_NOVA");
    Console.WriteLine("Gera original.docx e adaptado.docx com dados fictícios. Não lê currículos pessoais nem inicia a API.");
    Console.WriteLine("Inspeção: dotnet run --project tools/docx_layout_probe -- inspect INPUT.docx PROFILE.json PASTA_NOVA");
    Console.WriteLine("Aplicação: dotnet run --project tools/docx_layout_probe -- apply INPUT.docx PLAN.json PASTA_NOVA");
    Console.WriteLine("Inspeção/plano contêm dados privados. Aplicação exige aprovação explícita e revisão visual no Word.");
    return args.Length == 1 ? 0 : 2;
}

try
{
    DocxDemoGenerator.Generate(args[0]);
    Console.WriteLine($"Exemplos gerados em: {Path.GetFullPath(args[0])}");
    Console.WriteLine("Alteração: HABILIDADES > Bancos de Dados > inclusão de PostgreSQL.");
    Console.WriteLine("Validação visual pendente: abra os dois arquivos no Word e compare layout e paginação.");
    return 0;
}
catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
{
    Console.Error.WriteLine($"Não foi possível gerar o par de exemplos: {exception.Message}");
    Console.Error.WriteLine("Se houve falha de gravação, a pasta pode conter saída parcial; escolha outra pasta para tentar novamente.");
    return 2;
}
