# Atualização de plataforma .NET 10 LTS

.NET 9 encerra suporte em novembro de 2026. Esta etapa atualiza os projetos para
net10.0, pacotes Microsoft/EF Core para10.0.12, Npgsql EF para10.0.3 e imagens/CI
para10.0. A referência Microsoft.AspNetCore.OpenApi não utilizada foi removida;
Swagger continua provido pelo Swashbuckle e preserva as mesmas rotas/contratos.

Preparada em worktree isolado para não interferir na etapa de isolamento e regras
financeiras. Validação inicial: build zero avisos/erros;244 domínio,96 aplicação,
60 infraestrutura e376 API verdes (3 testes PostgreSQL indisponível pulados no
fallback SQLite). Exige nova validação da composição com segurança/financeiro,
coverage80 e PostgreSQL no CI antes de promover.

Estado: preparada; não publicada nem concluída. Promoção somente após develop verde
e main verde, com evidência dos SHAs e runs.