# Arquitetura e decisões

## Visão geral

```
Navegador (Angular)  ──HTTPS/cookie──►  API ASP.NET Core  ──►  PostgreSQL  ◄──  Worker .NET
                                        (serve o frontend)        (dados + fila)       │
                                                                                       ▼
                                                                         IProvedorNfse (Simulação)
```

- **API**: cadastros, competências, validação, registro dos pedidos de emissão, documentos. Nunca chama o
  provedor de NFS-e.
- **Worker**: consome a fila no banco e é o único que fala com o provedor. Funciona com o navegador fechado.
- **Banco como fila**: tabela `trabalhos_emissao`. Sem Redis, Kafka ou S3, proporcional a ~60 notas/mês.
- **Uma imagem**: a API serve o Angular compilado (`wwwroot`). O worker e as migrações usam a mesma imagem
  com outro comando. Isso atende ao contrato da infraestrutura (sem nginx) e economiza memória.

## Versões escolhidas

| Item | Versão | Motivo |
|---|---|---|
| .NET / ASP.NET Core / EF Core | 10 (LTS, suporte até nov/2028) | LTS atual; o .NET 11 acabou de sair e é STS |
| Npgsql EF Core | 10.0.x | compatível com EF Core 10 e PostgreSQL 18 |
| PostgreSQL | 18 | versão do servidor compartilhado BRSKT; os testes também rodam em 16+ |
| Angular / Material | 22.2 | versão estável atual (exige Node ^22.22 ou ^24.15) |
| Node (build) | 22 LTS | compatível com Angular 22 |
| QuestPDF | 2026.9 (Community) | layout de PDF declarativo; licença gratuita para receita bruta anual < US$ 1 mi, a confirmar para o cliente |
| xUnit v3 + Testcontainers | 3.2 / 4.17 | testes de integração com PostgreSQL real |

## Modelo de dados (principais tabelas)

| Tabela | Conteúdo |
|---|---|
| `emissor` | prestador único: dados cadastrais, endereço, regime, série e próximo número da DPS, regra da data de competência |
| `recorrencias` | identificação, tomador, serviço e parâmetros fiscais, campos variáveis, ativa/inativa |
| `competencias` | mês aberto (ano, mês), único por mês |
| `notas` | linha da competência: **cópia** do tomador e do serviço, valor, situação, cenário de simulação, DPS congelada (`dps_json`), resultado (número, chave, XML) |
| `lotes_emissao` | cada confirmação de "Emitir notas prontas", com chave de idempotência única |
| `trabalhos_emissao` | fila: status, próxima execução, bloqueio (worker, prazo, token), contadores |
| `tentativas_emissao` | histórico imutável de cada envio e consulta: resultado, mensagens, dados enviados |
| `simulador_registros` | "memória do servidor" simulado, independente do que o worker registrou |
| `status_workers` | sinal de vida do worker, exibido na interface |
| Identity + `data_protection_keys` | usuário administrador e chaves de cookie |

Enums são gravados como texto. Valores monetários usam `numeric(15,2)`. A concorrência otimista usa `xmin`.

### Mês de emissão e mês do serviço

- A entidade `Competencia` representa o **mês de emissão**: quando as notas saem. A navegação, os totais, o
  "valor anterior" e o histórico são todos organizados por ele.
- Cada nota guarda o **mês de referência do serviço** (`notas.mes_referencia`), que pode ser diferente. O padrão
  vem da recorrência ("mês anterior ao da emissão" ou "mesmo mês") e pode ser ajustado em cada linha editável.
  Ao mudar o mês de referência, a descrição é recalculada a partir do texto-fonte com marcadores
  (`notas.descricao_modelo`).
- A **data de competência da DPS** (campo fiscal) é outra coisa: segue a regra configurada no emissor (a
  confirmar com o contador; nas notas atuais do cliente, é a data da emissão).

### Cópia por mês

Ao abrir o mês, cada recorrência ativa gera uma linha com **cópia** do tomador, do serviço e dos
campos variáveis. Alterações posteriores no cadastro não afetam meses existentes. Para trazer o cadastro
atual para uma linha ainda não emitida, há a ação explícita "Atualizar dados a partir do cadastro". Na
confirmação da emissão, os dados completos da DPS (incluindo o prestador) são congelados em `notas.dps_json`.
O PDF e os reenvios usam somente esses dados.

### Campos variáveis

O **valor** é sempre preenchido a cada competência. A recorrência pode marcar *Descrição* e *Informações
complementares* como variáveis: na linha, esses campos ficam editáveis e precisam ser revisados (salvos)
antes de a linha ficar pronta. Os demais campos vêm do cadastro. É uma modelagem simples (flags) e extensível
para novos campos conhecidos, sem construtor genérico de formulários.

Marcadores aceitos na descrição e nas informações complementares:

- mês do serviço: `{mes_referencia}` (outubro), `{mes_ano_referencia}` (outubro/2026), `{referencia}` (10/2026);
- mês de emissão: `{mes}`, `{mes_ano}`, `{competencia}`, `{ano}`;
- mês anterior ao da emissão: `{mes_anterior}`, `{competencia_anterior}`.

Escritos em MAIÚSCULAS, geram texto em maiúsculas (ex.: "MÊS DE REFERÊNCIA {MES_REFERENCIA}" vira
"MÊS DE REFERÊNCIA OUTUBRO").

## Situações da nota

```
Pendente ──marcar──► Pronta ──confirmar emissão──► Processando ──► Emitida (bloqueada)
   ▲   ▲               │                                │
   │   └── editar ─────┘                                ├──► Erro (rejeição ou falhas temporárias esgotadas)
   │                                                    │       └── editar/marcar ──► Pendente/Pronta
   │                                                    └──► Resultado a confirmar ──consulta──► Emitida
   └── restaurar ── Ignorada ◄── ignorar (Pendente/Pronta/Erro)          └── inexistência confirmada ──► Erro (reenvio manual)
```

- Editar uma linha **Pronta** ou com **Erro** a devolve para **Pendente**: "pronta" significa "revisada com
  estes valores".
- **Emitida**, **Processando** e **Resultado a confirmar** não podem ser editadas.
- Valor anterior: o último valor preenchido da mesma recorrência em um mês de emissão anterior, desconsiderando
  linhas ignoradas. É só referência; a cópia é sempre explícita.
- **Reenvio manual** (decisão do usuário): nenhuma nota com erro é reenviada sozinha. Falha temporária e
  inexistência confirmada após resultado incerto levam a *Erro*; o usuário marca como pronta e emite de novo,
  com o mesmo número de DPS. As consultas de reconciliação continuam automáticas. O modo
  `Emissao__ReenvioAutomatico=true` existe, desligado, para uma fase futura.

## Emissão: garantias

1. **Pedido idempotente.** O diálogo de confirmação gera uma chave única. Cliques repetidos devolvem o mesmo
   lote (`lotes_emissao.chave_idempotencia` é única).
2. **Transição condicional.** Dentro de uma transação, com bloqueio das notas (`FOR UPDATE`), só notas
   `Pronta` da lista confirmada passam a `Processando`. Se a lista ou o total mudaram, o pedido é recusado e o
   usuário revisa de novo.
3. **Um trabalho ativo por nota.** Índice único parcial em `trabalhos_emissao(nota_id)` para os status
   Pendente/EmExecucao/Suspenso.
4. **Reivindicação segura.** O worker reivindica com `FOR UPDATE SKIP LOCKED` e um bloqueio com prazo
   (lease). Várias instâncias podem rodar sem processar o mesmo trabalho.
5. **Tentativa gravada antes do envio.** O registro "em andamento" é commitado antes de chamar o provedor. Se
   o processo cair, a tentativa fica aberta e é tratada como *interrompida*.
6. **Reconciliar antes de reenviar.** Nenhum envio acontece enquanto o último envio da nota não tiver
   resultado conclusivo de "não processado" (rejeição ou falha temporária), a menos que uma consulta
   posterior tenha confirmado que a nota não existe. Isso cobre timeout, interrupção, duplicidade e resultado
   gravado por um worker que perdeu o bloqueio.
7. **Janela mínima.** Após um envio incerto, a inexistência só é considerada confirmada se a consulta for feita
   ao menos `JanelaMinimaReenvio` depois (padrão: 2 min). Assim, o serviço remoto tem tempo de concluir o
   processamento.
8. **Mesmo identificador.** O número da DPS é atribuído uma única vez por nota e reutilizado. O serviço
   remoto pode reconhecer a duplicidade, e a consulta usa esse identificador.
9. **Resultado sempre registrado.** A resposta do provedor é gravada na tentativa mesmo que o worker tenha
   perdido o bloqueio. Só o dono atual altera a nota e o trabalho.
10. **Recuperação no reinício.** Com `Emissao__WorkerId` estável, o worker libera na inicialização os próprios
    trabalhos que ficaram em execução e os reconcilia na hora. Sem isso, a recuperação ocorre quando o
    bloqueio expira.
11. **Desligamento gracioso.** A chamada em andamento ao provedor não é cancelada pelo SIGTERM: o worker
    aguarda até 45 s para registrar a resposta.

Classificação dos resultados do provedor (`IProvedorNfse`):

| Resultado | Significado | Ação |
|---|---|---|
| Autorizada | NFS-e gerada | Emitida |
| Rejeitada | DPS analisada e recusada (conhecido) | Erro, com mensagens; o usuário corrige e prepara de novo |
| FalhaTemporaria | **certeza** de não processamento | Erro, com reenvio manual (no modo automático: novo envio com espera) |
| Incerto | pode ter sido processado (ex.: timeout após envio) | Resultado a confirmar, depois consulta |
| JaProcessada | o servidor acusa duplicidade | Resultado a confirmar, depois consulta |

## Autenticação

ASP.NET Core Identity com cookie (sem implementação própria de senha/sessão), sem endpoint de cadastro. O
administrador é criado a partir de variáveis de ambiente quando o banco não tem usuários. O antiforgery segue
o padrão do Angular (cookie `XSRF-TOKEN` e header `X-XSRF-TOKEN`). A política padrão de autorização exige
usuário autenticado em todas as rotas da API, inclusive nos downloads.

## PDF e XML

- **Simulação:** PDF simples gerado sob demanda a partir dos dados congelados da nota, com marca d'água e
  rodapé "SIMULAÇÃO — SEM VALIDADE FISCAL". Não é um DANFSe. Notas simuladas **não têm XML**: o endpoint de
  XML responde que não há XML fiscal.
- **Integração futura:** o XML autorizado será guardado em `notas.xml_autorizado`, junto com os dados
  congelados, e servido pelo endpoint autenticado. O DANFSe será gerado conforme o leiaute oficial, ou obtido
  de um serviço oficial se houver suporte confirmado (ver pendências).

## Extensão: provedor real

Implementar `IProvedorNfse` em um novo projeto (ex.: `HelpNfse.Provedores.NfseNacional`):

- `GerarIdentificadorDps`: formato oficial do Id da DPS;
- `EmitirAsync`: montar o XML da DPS, assinar, enviar com mTLS e classificar a resposta com o mesmo rigor
  (falha temporária só com certeza de não processamento);
- `ConsultarPorDpsAsync`: consulta pela DPS/chave.

Depois, liberar o novo valor de `Emissao__Provedor`. O restante (fila, reconciliação, histórico, interface)
não muda.
