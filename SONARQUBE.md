# SonarQube Configuration for Backend

## Properties (sonar-project.properties)

```properties
sonar.projectKey=controle-financeiro-backend
sonar.projectName=ControleFinanceiro Backend
sonar.projectVersion=1.0.0
sonar.language=cs
sonar.sourceEncoding=UTF-8
sonar.token=${SONAR_TOKEN}

# Backend paths
sonar.tests=tests
sonar.test.inclusions=**/*Tests.cs
sonar.exclusions=**/bin/**,**/obj/**,**/Migrations/**

# Coverage
sonar.cs.opencover.reportsPaths=**/coverage.opencover.xml
sonar.coverage.reportsPaths=**/coverage.opencover.xml

# Quality gates
sonar.qualitygate.wait=true

# .NET specific
sonar.dotnet.buildConfiguration=Release
sonar.dotnet.microsoft.visualstudio.solution.file=ControleFinanceiro.sln
```

## GitHub Actions Workflow

```yaml
name: SonarCloud Analysis

on:
  push:
    branches: [main, develop]
  pull_request:
    types: [opened, synchronize, reopened]

jobs:
  sonarcloud:
    name: SonarCloud Analysis
    runs-on: windows-latest
    steps:
      - name: Checkout
        uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '9.0.x'

      - name: Restore dependencies
        run: dotnet restore

      - name: Build
        run: dotnet build --no-restore

      - name: Test with Coverage
        run: dotnet test --no-build --logger "console;verbosity=normal" --collect:"XPlat Code Coverage" --results-directory ./coverage

      - name: Convert to OpenCover
        run: dotnet tool install --global dotnet-reportgenerator-globaltool --version 5.3.0
        run: reportgenerator -reports:"./**/coverage.coverage.xml" -targetdir:./coverage -reporttypes:OpenCover

      - name: SonarCloud Scan
        uses: SonarSource/sonarqube-scan-action@master
        env:
          SONAR_TOKEN: ${{ secrets.SONAR_TOKEN }}
          SONAR_HOST_URL: ${{ secrets.SONAR_HOST_URL }}
```

## Secrets needed (GitHub):

| Secret | Description | Example |
|--------|-------------|---------|
| `SONAR_TOKEN` | Token from SonarCloud/sonarQube | `sqm_xxxxxxxxxxxxxxxx` |
| `SONAR_HOST_URL` | URL do servidor | `https://sonarcloud.io` |

## Para usar localmente:

```bash
# Install dotnet-sonarscanner
dotnet tool install --global dotnet-sonarscanner

# Run analysis
dotnet sonarscanner begin /k:"controle-financeiro-backend" /d:sonar.token="YOUR_TOKEN"
dotnet build
dotnet test /p:CollectCoverage=true
dotnet sonarscanner end /d:sonar.token="YOUR_TOKEN"
```