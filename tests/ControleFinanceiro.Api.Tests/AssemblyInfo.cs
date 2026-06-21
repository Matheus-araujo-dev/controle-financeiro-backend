using Xunit;

// Integração roda contra um SQL Server real (LocalDB), com schema recriado por teste. LocalDB é uma
// instância única e leve; executar classes em paralelo causa contenção de DDL e timeouts. Rodamos as
// coleções sequencialmente para estabilidade (o trade-off de tempo é aceitável num gate de cobertura).
[assembly: CollectionBehavior(DisableTestParallelization = true)]
