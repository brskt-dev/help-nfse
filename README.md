# help-nfse

> **SIMULAÇÃO — SEM VALIDADE FISCAL.** Esta versão não emite notas fiscais reais. Todas as "emissões" são
> feitas por um provedor simulado, e os documentos gerados trazem esse aviso.

Microsistema para um prestador de serviços organizar as **NFS-e recorrentes do mês** (~60 notas): cadastrar
recorrências, abrir o mês, preencher as variáveis (valor e campos marcados), revisar, marcar como prontas,
confirmar a emissão e acompanhar o resultado de cada nota, com histórico completo.

**Dois meses distintos:** o app organiza e exibe tudo pelo **mês de emissão** (quando a nota sai). Cada nota
também tem o **mês de referência do serviço**, que pode ser outro (padrão definido na recorrência: mês
anterior ou mesmo mês) e pode ser usado na descrição com marcadores como `{MES_REFERENCIA}`.

- [Arquitetura e decisões](docs/arquitetura.md)
- [Pendências para a homologação (Produção Restrita da NFS-e Nacional)](docs/pendencias-homologacao.md)
- [Roteiro de testes manuais](docs/roteiro-de-testes.md)

## Stack

| Parte | Tecnologia |
|---|---|
| Frontend | Angular 22 (standalone, zoneless) + Angular Material 22, Vitest |
| API | ASP.NET Core 10 (LTS), Minimal APIs, ASP.NET Core Identity (cookie), antiforgery |
| Worker | .NET 10 Worker Service |
| Banco | PostgreSQL 18 (EF Core 10 + Npgsql 10); também é a fila de processamento |
| PDF | QuestPDF (licença Community) |
| Execução | Docker Compose; uma única imagem (API + frontend + worker) |

## Executar localmente (Docker)

Pré-requisitos: Docker com Compose v2.

```bash
cp .env.example .env
```

Edite o `.env` e troque as senhas. Em seguida:

```bash
docker compose up --build
```

- Acesse <http://localhost:8080>.
- Entre com `ADMIN_EMAIL` / `ADMIN_PASSWORD` do `.env`. O administrador é criado na primeira inicialização,
  quando ainda não existe nenhum usuário. Não há cadastro público.
- Com `DEMO_DADOS=true` (padrão), o banco vazio recebe **dados fictícios**: emissor, 9 recorrências (1 inativa)
  e os três meses anteriores já emitidos em simulação, com exemplos de rejeição corrigida e de resultado
  incerto reconciliado. O mês atual fica por abrir.
- Os dados ficam no volume `pgdata` (persistente). Para recomeçar do zero: `docker compose down -v`.

O compose local reproduz o modelo de banco do servidor: um usuário dono do schema, usado só nas migrações
(`helpnfse_migrator`), e um usuário só com DML para a aplicação (`helpnfse_app`). O serviço `migrate` roda
antes da API e do worker.

## Desenvolvimento (sem Docker)

Requer .NET SDK 10, Node 22 e um PostgreSQL local. As configurações de desenvolvimento estão em
`backend/src/*/appsettings.Development.json`. Por padrão usam `Host=localhost;Database=helpnfse;Username=helpnfse;Password=dev-local-only`;
ajuste para o seu banco ou use o PostgreSQL do compose (`docker compose up db`, porta `5433`).

Terminal 1, API em http://localhost:5080 (aplica as migrações e carrega a demonstração ao iniciar):

```bash
dotnet run --project backend/src/HelpNfse.Api --launch-profile http
```

Terminal 2, worker:

```bash
dotnet run --project backend/src/HelpNfse.Worker
```

Terminal 3, frontend em http://localhost:4200 (com proxy para a API):

```bash
cd frontend && npm install && npm start
```

Acesso de desenvolvimento: `admin@exemplo.local` / `troque-esta-senha-local` (somente no `appsettings.Development.json`).

## Testes automatizados

Backend: 68 testes de integração com PostgreSQL real, com um banco novo por teste:

```bash
HELPNFSE_TEST_PG="Host=localhost;Username=helpnfse;Password=dev-local-only;Database=postgres" dotnet test backend/tests/HelpNfse.Tests
```

Sem `HELPNFSE_TEST_PG`, os testes sobem um PostgreSQL 18 com Testcontainers (requer Docker). O usuário
precisa de permissão `CREATEDB`.

| Área | O que é verificado |
|---|---|
| Abertura do mês | abrir duas vezes e concorrentemente não duplica; reabrir inclui só recorrências novas; linhas nascem pendentes e sem valor; mês de referência do serviço e marcadores da descrição |
| Histórico | alterar recorrência/emissor não altera linhas nem DPS emitidas; desativar preserva histórico; nota emitida é imutável |
| Validação | sem valor, documento inválido e campo variável não revisado impedem "pronta"; lote misto marca só as válidas; editar uma pronta volta para pendente |
| Concorrência | clique repetido (mesma chave) devolve o mesmo lote; pedidos concorrentes não duplicam trabalhos; 4 workers em paralelo enviam cada nota uma única vez; índice único de trabalho ativo |
| Recuperação | queda do worker após o envio é reconciliada por consulta, sem reenvio; bloqueio expirado é assumido por outro worker; resultado registrado por worker que perdeu o bloqueio não gera reenvio |
| Resultado incerto e reenvio | timeout com emissão é confirmado por consulta; timeout sem emissão vai para Erro após consulta fora da janela mínima e o reenvio manual usa o mesmo número de DPS; consulta indisponível nunca reenvia; falha temporária vai para Erro sem reenvio automático; rejeição corrigida preservando tentativas; modo opcional de reenvio automático |
| Acesso | dados, PDF e XML exigem autenticação; login exige token antiforgery; não há cadastro público |

Frontend (conversão e formatação de valores em reais):

```bash
cd frontend && npx ng test --watch=false
```

## Como testar a simulação

Na tela do mês, cada linha tem um **cenário de simulação** (em "Detalhes" ou no menu da linha → "Editar campos
variáveis"). O padrão é *Sucesso*. Os cenários são determinísticos.

**Reenvio é manual:** nenhuma nota com erro é reenviada automaticamente. Para tentar de novo, corrija o que for
preciso, use "Preparar reenvio (marcar como pronta)" e "Emitir notas prontas". As **consultas** de
reconciliação de resultados incertos continuam automáticas, porque não reenviam nada.

| Cenário | O que acontece |
|---|---|
| Sucesso | autorizada no primeiro envio |
| Rejeição fiscal | rejeição com código `SIM-E001`; a linha vai para **Erro**. Troque o cenário para *Sucesso* (a linha volta para pendente), marque como pronta e emita de novo: o histórico mostra as duas tentativas |
| Falha temporária (1x) | falha com certeza de não processamento: **Erro** ("nenhuma nota foi emitida"). Reenvie manualmente: a 2ª tentativa é autorizada |
| Falha temporária persistente | cada envio termina em **Erro**, sem nota emitida |
| Timeout — nota emitida | resposta perdida após a emissão: **Resultado a confirmar** → consulta → **Emitida**, sem reenvio |
| Timeout — nota não emitida | requisição perdida: *Resultado a confirmar* → consulta confirma a inexistência (após a janela mínima) → **Erro**. O reenvio manual usa o mesmo número de DPS |
| Timeout + consulta indisponível | as duas primeiras consultas falham; a linha fica em *Resultado a confirmar* até a consulta funcionar |
| Interromper o worker | o provedor emite e o processo do worker é encerrado antes de registrar a resposta. O Docker o reinicia e o worker reconcilia por consulta. Na VPS compartilhada a queda é simulada por uma falha e a recuperação ocorre quando o bloqueio expira (2 min) |

Roteiro completo: [docs/roteiro-de-testes.md](docs/roteiro-de-testes.md).

## Configuração

| Variável | Uso |
|---|---|
| `ConnectionStrings__Default` | banco da API e do worker (usuário só com DML) |
| `ConnectionStrings__Migrations` | banco para `--migrate` (usuário dono do schema) |
| `Admin__Email`, `Admin__Senha` | acesso inicial, criado só se não houver usuário (senha com 10+ caracteres) |
| `Admin__RedefinirSenha=true` | redefine a senha do administrador para `Admin__Senha` na inicialização; remova depois |
| `Demo__CarregarDados` | carrega os dados fictícios em banco vazio |
| `Emissao__Provedor` | apenas `Simulacao` nesta versão; qualquer outro valor impede a inicialização |
| `Emissao__WorkerId` | identificador estável do worker, usado na recuperação após reinício |
| `Emissao__ReenvioAutomatico` | `false` (padrão): reenvio manual após erro. `true`: reenvia sozinho após falha temporária e após inexistência confirmada |
| `Emissao__*` | tempos da fila: `DuracaoBloqueio`, `TimeoutProvedor`, `JanelaMinimaReenvio`, `EsperasConsulta__N`, `EsperasFalhaTemporaria__N`, `MaxFalhasTemporarias`, `MaxConsultasIndisponiveis` |
| `Simulacao__Latencia`, `Simulacao__PermitirInterrupcaoReal` | comportamento do simulador |
| `Certificado__*` | preparado para a homologação (arquivo `.pfx` montado ou base64 em secret, e a senha); **não usado** nesta versão |

Nenhum segredo fica no repositório: `.env` e arquivos de certificado são ignorados pelo Git.

## Deploy (infraestrutura BRSKT)

Produto `helpnfse`, domínio `helpnfse.brskt.dev`. Banco PostgreSQL compartilhado, Traefik com HTTPS e
deploy por GitHub Actions a cada push na `main` (ver `CLAUDE.md` e `deploy/compose.yml`).

- O CI roda os testes, publica `ghcr.io/brskt-dev/help-nfse:sha-<commit>` e envia o compose à VPS.
- Na VPS, `migrate` roda antes da nova versão; em caso de falha, o rollback é automático.
- Variáveis do app (`Admin__Email`, `Admin__Senha`, ...) vão no secret `APP_ENV` do repositório.
- Valide o compose antes de cada alteração em `deploy/`:

```bash
../brskt-infra/scripts/validate-compose.sh helpnfse deploy/compose.yml
```

## Segurança

- Um único administrador (ASP.NET Core Identity): senha com hash, bloqueio após 5 falhas e limite de
  tentativas de login.
- Cookie de sessão `HttpOnly` e `SameSite=Strict`, com token antiforgery em toda escrita.
- Todos os dados e downloads (PDF/XML) exigem sessão. Os downloads passam pela API, sem URL pública.
- CSP restritiva, `X-Frame-Options: DENY` e `Cache-Control: no-store` nas respostas da API.
- Chaves de proteção de dados ficam no banco, então as sessões sobrevivem a reinícios.
- O certificado A1 nunca passa pela interface. Na integração futura, ele será carregado pelo backend a partir
  de um secret.
