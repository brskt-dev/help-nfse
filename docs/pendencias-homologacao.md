# Pendências para a homologação (Produção Restrita da NFS-e Nacional)

Esta entrega valida o fluxo **simulado**. Antes da integração real, os itens abaixo precisam ser confirmados.
Nada aqui foi presumido como regra fiscal; os campos existem no sistema e estão marcados como
"confirmar" na interface.

## 1. Confirmar com o contador

| Item | Onde fica no sistema | Observação |
|---|---|---|
| Opção pelo Simples Nacional e regime de apuração | Emissor | As notas de exemplo indicam ME/EPP, com tributos federais e municipal pelo SN |
| Regime especial de tributação | Emissor | |
| Inscrição municipal do prestador | Emissor | As notas de exemplo não informam IM; confirmar se é exigida |
| Regra da **data de competência** da DPS | Emissor | Nas notas de exemplo, a competência da NFS-e é a **data da emissão** (dia 16), e a descrição cita o mês de referência anterior. A demonstração usa "Data da emissão" |
| Código de tributação nacional (cTribNac) e NBS por serviço | Recorrência | Exemplo: 11.02.01 e NBS 1.1802.30.00 (monitoramento) |
| Código de tributação municipal | Recorrência | Vazio nos exemplos |
| Tributação do ISSQN e município de incidência | Recorrência | Exemplo: operação tributável no município do prestador |
| **ISS retido** pelo tomador e alíquota | Recorrência | Varia por tomador. Com retenção, a nota mostra a alíquota aplicada; sem retenção, não. Confirmar quando a alíquota deve ser informada na DPS |
| **IBS/CBS** (Reforma Tributária): CST, cClassTrib, indicador da operação | Recorrência | Exemplos: CST 000, cClassTrib 000001, indicador 030101. Os valores são apurados pelo sistema nacional; o help-nfse não calcula tributos |
| Endereço do tomador: obrigatório? | Recorrência | Os exemplos trazem endereço completo dos tomadores |
| Uso de CPF e de CNPJ alfanumérico | Recorrência | A validação já aceita o CNPJ alfanumérico; confirmar o suporte no leiaute |
| Informações complementares padrão | Recorrência | Nos exemplos, só o texto da Lei 12.741/2012 (gerado pelo emissor nacional) |

## 2. Confirmar na documentação oficial (antes de escrever o provedor real)

Consultar a documentação **atual** do Portal da NFS-e Nacional (gov.br/nfse): manuais, leiautes (XSD),
notas técnicas e a documentação das APIs. Não usar endpoints de memória e não confundir a API de **emissão**
(Sefin Nacional) com a de **distribuição de documentos** (ADN).

- [ ] **Autenticação mútua (mTLS)** com o certificado A1 do CNPJ do prestador: requisitos de cadeia e de
      certificado ICP-Brasil.
- [ ] **Assinatura da DPS**: padrão XMLDSig, algoritmos, elemento assinado e canonicalização.
- [ ] **Leiaute da DPS** vigente, com o grupo IBS/CBS, e versão do schema. Validar o XML com o XSD antes do
      envio.
- [ ] **Formato do identificador da DPS** e regras de série/número:
  - as notas atuais saem pelo **emissor nacional** ("Ambiente Gerador 2") com **série 70000**;
  - confirmar qual faixa de série um aplicativo próprio deve usar, para não colidir com a numeração do
    portal;
  - confirmar se um número rejeitado pode ser reutilizado. O sistema reutiliza o mesmo número da nota em
    reenvios.
- [ ] **Envio**: endpoint de Produção Restrita, compactação/codificação do XML, formato da resposta de
      sucesso e de rejeição, e códigos de erro.
- [ ] **Duplicidade**: como o serviço responde a uma DPS já processada (mapear para `JaProcessada`).
- [ ] **Consulta para reconciliação**: como consultar a NFS-e a partir do identificador da DPS. Mapear
      "não encontrada" e "indisponível" sem ambiguidade.
- [ ] **Classificação de falhas**: quais respostas e erros de rede garantem "não processado"
      (`FalhaTemporaria`) e quais devem ser tratados como incertos.
- [ ] **Data de competência**: limites (ex.: não pode ser futura). A regra "último dia do mês" pode gerar data
      futura se a emissão ocorrer antes do fim do mês.
- [ ] **XML autorizado**: onde obtê-lo após a emissão, para guardar em `notas.xml_autorizado`.
- [ ] **DANFSe**: o leiaute oficial é o **DANFSe v2.0**, com QR Code. Verificar se há serviço oficial
      para obter o PDF; se não houver, gerar conforme o leiaute. Não presumir endpoint de PDF.
- [ ] Limites de requisição e janelas de manutenção, para ajustar esperas e o tamanho do bloqueio.

## 3. Ajustes técnicos previstos

- [ ] Novo projeto `HelpNfse.Provedores.NfseNacional` implementando `IProvedorNfse`.
- [ ] Carregamento do certificado: `Certificado__CaminhoArquivo` (arquivo montado) ou
      `Certificado__ConteudoBase64`, com a senha em `Certificado__Senha` ou `Certificado__CaminhoArquivoSenha`.
      Na VPS, combinar com a sessão de infraestrutura como entregar o arquivo como secret (o contrato só
      permite montagens em `/opt/brskt/apps/helpnfse/data/`) ou usar o `APP_ENV`. Nunca registrar a senha em
      log.
- [ ] O worker precisará da rede `egress` (já prevista no `deploy/compose.yml`).
- [ ] Liberar `Emissao__Provedor=ProducaoRestrita` somente após a validação; manter "Simulacao" como padrão.
- [ ] Trocar o aviso permanente da interface conforme o ambiente (Produção Restrita também não tem validade
      fiscal).
- [ ] Ajustar os tempos (`JanelaMinimaReenvio`, `EsperasConsulta`, `TimeoutProvedor`) conforme o
      comportamento real do serviço.
- [ ] Revisar a licença do QuestPDF para o porte do cliente (Community: receita bruta anual < US$ 1 milhão).

## 4. Regras de negócio a confirmar com o usuário

- [ ] **"Competência" no help-nfse = mês de emissão ou mês de referência do serviço?** Nas notas de exemplo,
      os serviços de outubro são faturados em novembro. Há duas formas de uso: abrir "novembro" e usar
      `{MES_ANTERIOR}` na descrição, ou abrir "outubro" e emitir em novembro com a regra "Data da emissão".
- [ ] Após confirmar que uma nota com resultado incerto **não** foi emitida, o sistema reenvia sozinho (mesmo
      identificador, após a janela mínima). Confirmar se o usuário prefere aprovar esse reenvio manualmente.
- [ ] Outros campos variáveis por competência além de descrição e informações complementares (ex.:
      desconto, vencimento).
