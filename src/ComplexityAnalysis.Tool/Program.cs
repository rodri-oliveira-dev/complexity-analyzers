using ComplexityAnalysis.Tool.Cli;

Environment.ExitCode = await ComplexityToolApplication.RunAsync(
    args,
    Console.Out,
    Console.Error,
    CancellationToken.None);
