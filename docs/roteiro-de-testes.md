# Roteiro de testes manuais (simulação)

Pré-requisito: aplicação no ar com dados de demonstração (`docker compose up --build`, ver README).

## 1. Rotina mensal completa

1. Entre com o administrador. Note a faixa vermelha permanente: **SIMULAÇÃO — SEM VALIDADE FISCAL**.
2. **Histórico:** volte um mês (‹). O mês anterior está todo emitido. Abra "Detalhes" do Condomínio
   Horizonte → aba *Tentativas*: envio com resultado incerto seguido de consulta que confirmou a emissão.
3. Volte ao mês atual (mês de **emissão**): ele ainda não foi aberto. Clique em **Abrir mês**. As linhas nascem
   *Pendente* e **sem valor**; a coluna "Valor anterior" mostra o valor do mês anterior. Sob a descrição
   aparece o **mês do serviço** (outubro na maioria; a Escola usa o mesmo mês da emissão).
4. Clique em **Abrir** de novo, ou recarregue: nenhuma linha é duplicada.
5. Marque todas as linhas → **Copiar valor anterior**. Só as linhas sem valor recebem cópia (explícita).
6. **Marcar como prontas:** as linhas com campo variável não revisado (Mercado, Condomínio e Instituto) são
   recusadas, com o motivo por linha.
7. Edite um valor diretamente na tabela de uma linha *Pronta*: ela volta para *Pendente*.
8. Abra o Mercado (clique no nome), revise a descrição e salve. Troque o "Mês de referência do serviço" de uma
   linha e veja a descrição ser recalculada. Faça o mesmo com o Condomínio (informações
   complementares) e com o Instituto (descrição).
9. Ignore uma linha neste mês (menu ⋮ → Ignorar neste mês). Restaure-a e ignore de novo, se quiser.
10. Adicione uma **nota avulsa**.
11. Marque tudo como pronto e clique em **Emitir notas prontas**. Confira a quantidade e o total na
    confirmação.
12. Acompanhe: as linhas passam por *Processando* e terminam em *Emitida*. A tela se atualiza sozinha. Feche o
    navegador durante o processamento e volte depois: o worker continua.
13. Linha emitida → ⋮ → **Baixar PDF (simulação)**. O PDF traz a marca d'água e os dados congelados.
14. Tente editar uma nota emitida: não é possível.
15. Em **Recorrências**, altere o nome de um tomador já emitido. Volte à competência: a linha emitida mantém
    os dados antigos.
16. Desative uma recorrência. Ela não aparece em novas competências, e o histórico continua visível na
    página da recorrência.

## 2. Cenários de simulação

Antes de marcar como pronta, abra a linha → *Cenário de simulação*:

| Cenário | Resultado esperado |
|---|---|
| Rejeição fiscal | **Erro** com `SIM-E001`. Troque para *Sucesso* (a linha volta para pendente), marque como pronta e emita: aba *Tentativas* com Rejeitada → Autorizada |
| Falha temporária (1x) | **Erro** sem nota emitida. Menu ⋮ → "Preparar reenvio" → "Emitir notas prontas": autorizada |
| Falha temporária persistente | **Erro** a cada envio |
| Timeout — nota emitida | *Resultado a confirmar* → consulta "Encontrada" → *Emitida* (um único envio) |
| Timeout — nota não emitida | *Resultado a confirmar* → consulta "Não encontrada" (após a janela) → **Erro**. O reenvio manual usa o mesmo número de DPS → *Emitida* |
| Timeout + consulta indisponível | consultas indisponíveis, nenhum reenvio; depois *Emitida* |
| Interromper o worker | localmente, o worker é encerrado e reiniciado pelo Docker (veja com `docker compose ps`). Após o reinício, consulta "Encontrada" → *Emitida*, sem reenvio. Em *Tentativas*: Interrompida → Encontrada |

## 3. Proteções

- **Duplo clique:** na confirmação, clique várias vezes em "Confirmar emissão": um único lote é criado.
- **Lista desatualizada:** abra a confirmação, altere uma nota pronta em outra aba e confirme: a emissão é
  recusada e a prévia é recarregada.
- **Sem sessão:** em uma janela anônima, acesse `/api/notas/1/pdf`: resposta 401.
- **Worker parado:** `docker compose stop worker` → o indicador no topo mostra "Worker sem sinal". As notas
  ficam em *Processando* e são processadas quando o worker volta (`docker compose start worker`).
