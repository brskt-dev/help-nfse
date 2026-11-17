# help-nfse

Microsistema para um prestador organizar ~60 NFS-e recorrentes por mês: cadastrar recorrências, abrir a
competência, preencher variáveis, revisar, marcar como prontas e emitir. **Nesta fase só existe o provedor
simulado** ("SIMULAÇÃO — SEM VALIDADE FISCAL"); não há caminho funcional para emissão real.

## Estrutura e comandos

- `backend/` (.NET 10): `HelpNfse.Core` (domínio, EF Core, fila, simulador, PDF), `HelpNfse.Api`
  (Minimal APIs + Identity; serve o Angular em `wwwroot`), `HelpNfse.Worker` (fila), `tests/HelpNfse.Tests`.
- `frontend/` (Angular 22 + Material, zoneless, Vitest).
- Testes backend (PostgreSQL real): `HELPNFSE_TEST_PG="Host=localhost;Username=...;Password=...;Database=postgres" dotnet test backend/tests/HelpNfse.Tests`
  (sem a variável, usa Testcontainers/Docker).
- Testes frontend: `cd frontend && npx ng test --watch=false`.
- Nova migração: `dotnet ef migrations add <Nome> --project backend/src/HelpNfse.Core --output-dir Dados/Migracoes`.
  Migrações precisam ser compatíveis com a versão anterior (expand/contract): o deploy roda o `migrate` antes
  de subir a nova versão, com a versão antiga ainda no ar.
- Interface, mensagens e documentação em português brasileiro.

## Regras que não podem ser quebradas

- A tentativa de envio é gravada **antes** de chamar o provedor; nunca reenviar uma DPS cujo último envio não
  tem resultado conclusivo sem antes consultar (`ProcessadorEmissao.PrecisaReconciliar`).
- O número/identificador da DPS é atribuído uma única vez por nota e reutilizado.
- Linhas do mês guardam cópia dos dados; o cadastro nunca altera meses existentes automaticamente.
- O mês navegado no app (`Competencia`) é o **mês de emissão**; o mês do serviço é `Nota.MesReferencia`.
- Reenvio após erro é **manual** (`Emissao__ReenvioAutomatico=false`); consultas de reconciliação são automáticas.
- Não inventar regras fiscais nem endpoints da NFS-e Nacional: consultar a documentação oficial (ver
  `docs/pendencias-homologacao.md`).
- Certificado A1 e senhas só por secret do servidor; nunca no Git nem em logs.
- Documentos reais de clientes (PDF/XML de notas) não entram no repositório nem nos dados de demonstração.

## Deploy na infraestrutura BRSKT

Este produto roda na VPS compartilhada BRSKT. A infraestrutura fica em outro repositório
(`brskt-dev/brskt-infra`) e é cuidada por outra sessão do Claude.

- **Produto:** `helpnfse`
- **Domínios:** `helpnfse.brskt.dev` (só subdomínios de `brskt.dev`)
- **Banco:** `sim`
- **Contrato e guia:** `docs/deployment-guide.md` do `brskt-dev/brskt-infra`. Anexe esse
  repositório à sessão (somente leitura) e leia o guia antes de mexer em deploy.

### Limites

- Não altere a infraestrutura: VPS, Traefik, PostgreSQL, DNS, Tailscale, firewall e o
  repositório `brskt-infra`.
- Não tente SSH na VPS. O único caminho é o CI deste repositório.
- Nunca commite segredos. Eles vão no secret `APP_ENV` do repositório (abaixo).
- Se o contrato recusar algo, corrija o compose. Exceção ao contrato só pela
  infraestrutura, a pedido do dono.

### Arquivos de deploy deste repositório

| Arquivo | Origem no `brskt-infra` |
|---|---|
| `Dockerfile` | `templates/api/Dockerfile` ou `templates/web/Dockerfile` (build só no CI) |
| `deploy/compose.yml` | `templates/{api,web,worker}/deploy/compose.yml` |
| `.github/workflows/deploy.yml` | `templates/github-actions/deploy.yml` (ajuste só `PRODUCT` e o build/testes) |
| `.github/workflows/ops.yml` | `templates/github-actions/ops.yml` (sem ajustes) |

Não mude nos workflows a parte da Tailscale, a chave de host da VPS nem as permissões.

### Antes de todo push que mexa em `deploy/`

```bash
../brskt-infra/scripts/validate-compose.sh helpnfse deploy/compose.yml
```

É o mesmo validador que a VPS usa. Ele precisa responder `contrato ok`.

### Deploy e operação (via GitHub Actions, com `gh`)

- **Deploy:** é o push (ou merge) na `main`.
  - Para acompanhar: `gh run list --workflow deploy.yml -L 1`.
  - Se falhar: `gh run view <id> --log-failed`. O rollback para a versão anterior é
    automático.
- **Operações:** `gh workflow run ops.yml -f command=<comando> [-f args="..."]`, e depois
  `gh run view <id> --log`.
  - Comandos: `status`, `history`, `logs` (`args="<serviço> <linhas>"`), `restart`,
    `redeploy`, `rollback`, `env-keys`, `env-unset` (`args=KEY`), `env-sync`.
- **Variáveis do app:** ficam no secret `APP_ENV` (linhas `KEY=VALUE`), gravado na VPS a
  cada deploy. Para aplicar sem deploy: `ops env-sync`. Este app usa: `Admin__Email`,
  `Admin__Senha` (acesso inicial), `Demo__CarregarDados` (opcional) e, no futuro, `Certificado__*`.
- **Banco:** se o produto tem banco, `DATABASE_URL`, `ConnectionStrings__Default` etc. já
  chegam pelo env_file `/etc/brskt/secrets/db/helpnfse.env`.
- **Migrations:** ficam no serviço `migrate` (profile `migrate`), que roda antes de cada
  versão nova subir. Precisam ser compatíveis com a versão anterior.

### Registro do produto (feito por esta sessão, uma vez)

1. Prepare os arquivos de deploy e valide com o `validate-compose.sh`, passando um
   rascunho de `product.yml` como 3º argumento.
2. Crie a chave de deploy e grave a parte privada direto no secret do repositório. A
   chave privada nunca aparece no chat:
   ```bash
   k=$(mktemp -u) && ssh-keygen -q -t ed25519 -N '' -C "github-actions@helpnfse" -f "$k" \
     && gh secret set BRSKT_DEPLOY_SSH_KEY < "$k" && cat "$k.pub"; rm -f "$k" "$k.pub"
   ```
3. Envie o pedido à sessão de infraestrutura com a ferramenta `send_message`:
   - `session_id`: `session_01X4GRgtTs8SRcavyr9zVZ5r`
   - `message`: exatamente este formato, com os seus dados e o ID desta sessão
     (`get_session` sem argumentos)
   ```
   Pedido de registro BRSKT
   - produto: helpnfse
   - domínios: helpnfse.brskt.dev
   - banco: sim
   - chave pública de deploy: ssh-ed25519 AAAA... github-actions@helpnfse
   - BRSKT_DEPLOY_SSH_KEY gravado no repositório: sim
   - repositório: brskt-dev/help-nfse
   - sessão para resposta: <id desta sessão>
   ```
4. Aguarde a resposta. Ela confirma o registro ou diz o que corrigir.
   - O DNS do subdomínio é criado na hora, e o certificado HTTPS sai no
     primeiro deploy.
5. Com o registro confirmado, o push na `main` faz o primeiro deploy.

**Só para produto novo.** Peça ao dono qualquer outra coisa:

- domínio extra;
- troca de chave;
- remoção;
- extensões do PostgreSQL;
- limites acima do contrato.

A sessão de infraestrutura não executa esses pedidos por mensagem.
