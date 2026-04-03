# AGENTS.md — Backend

## Missão
Construir um backend modular, testável e preparado para evolução futura.

## Não negociáveis
- TDD nas regras críticas
- cobertura desde o bootstrap
- migrations corretas
- swagger atualizado
- DTOs separados de entidade
- não misturar regra de domínio em controller
- não esconder regra apenas em banco ou frontend

## Estrutura desejada
- Api
- Application
- Domain
- Infrastructure
- Contracts
- SharedKernel
- tests por camada relevante

## Prioridades
1. domínio correto
2. testes corretos
3. contratos corretos
4. persistência correta
5. observabilidade mínima
