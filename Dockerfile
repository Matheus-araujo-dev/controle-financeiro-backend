FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG configuration=Release
WORKDIR /src

COPY ["src/ControleFinanceiro.Api/ControleFinanceiro.Api.csproj", "ControleFinanceiro.Api/"]
COPY ["src/ControleFinanceiro.Application/ControleFinanceiro.Application.csproj", "ControleFinanceiro.Application/"]
COPY ["src/ControleFinanceiro.Domain/ControleFinanceiro.Domain.csproj", "ControleFinanceiro.Domain/"]
COPY ["src/ControleFinanceiro.Infrastructure/ControleFinanceiro.Infrastructure.csproj", "ControleFinanceiro.Infrastructure/"]
COPY ["src/ControleFinanceiro.SharedKernel/ControleFinanceiro.SharedKernel.csproj", "ControleFinanceiro.SharedKernel/"]
COPY ["src/ControleFinanceiro.Contracts/ControleFinanceiro.Contracts.csproj", "ControleFinanceiro.Contracts/"]

RUN dotnet restore "ControleFinanceiro.Api/ControleFinanceiro.Api.csproj"

COPY src/ .

WORKDIR "/src/ControleFinanceiro.Api"
RUN dotnet publish -c $configuration -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "ControleFinanceiro.Api.dll"]
