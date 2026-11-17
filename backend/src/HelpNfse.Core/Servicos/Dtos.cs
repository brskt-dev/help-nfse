using HelpNfse.Core.Dominio;
using HelpNfse.Core.Provedores;

namespace HelpNfse.Core.Servicos;

// ---------- Emissor ----------
public sealed record EmissorDto(
    bool Configurado,
    string RazaoSocial,
    string? NomeFantasia,
    string Cnpj,
    string? InscricaoMunicipal,
    string? Email,
    string? Telefone,
    Endereco Endereco,
    OpcaoSimplesNacional OpcaoSimplesNacional,
    RegimeApuracaoSimples RegimeApuracaoSimples,
    RegimeEspecialTributacao RegimeEspecialTributacao,
    string SerieDps,
    long ProximoNumeroDps,
    RegraDataCompetencia RegraDataCompetencia,
    List<string> PendenciasConfirmacao,
    DateTimeOffset? AtualizadoEm);

public sealed record SalvarEmissorDto(
    string RazaoSocial,
    string? NomeFantasia,
    string Cnpj,
    string? InscricaoMunicipal,
    string? Email,
    string? Telefone,
    Endereco Endereco,
    OpcaoSimplesNacional OpcaoSimplesNacional,
    RegimeApuracaoSimples RegimeApuracaoSimples,
    RegimeEspecialTributacao RegimeEspecialTributacao,
    string SerieDps,
    long ProximoNumeroDps,
    RegraDataCompetencia RegraDataCompetencia);

// ---------- Recorrências ----------
public sealed record RecorrenciaResumoDto(
    long Id,
    string Identificacao,
    bool Ativa,
    string TomadorNome,
    string TomadorDocumento,
    string Descricao,
    CamposVariaveis CamposVariaveis,
    DateTimeOffset AtualizadaEm);

public sealed record RecorrenciaDto(
    long Id,
    string Identificacao,
    bool Ativa,
    DadosTomador Tomador,
    DadosServico Servico,
    CamposVariaveis CamposVariaveis,
    string? Observacoes,
    DateTimeOffset CriadaEm,
    DateTimeOffset AtualizadaEm,
    DateTimeOffset? DesativadaEm,
    uint Versao,
    List<HistoricoRecorrenciaDto> Historico);

public sealed record HistoricoRecorrenciaDto(
    long NotaId, int Ano, int Mes, decimal? Valor, SituacaoNota Situacao, string? NumeroNfse, DateTimeOffset? EmitidaEm);

public sealed record SalvarRecorrenciaDto(
    string Identificacao,
    DadosTomador Tomador,
    DadosServico Servico,
    CamposVariaveis CamposVariaveis,
    string? Observacoes,
    uint? Versao);

// ---------- Competências ----------
public sealed record CompetenciaResumoDto(
    long Id, int Ano, int Mes, DateTimeOffset AbertaEm, List<ResumoSituacaoDto> Resumo, int Quantidade, decimal TotalEmitido);

public sealed record ResumoSituacaoDto(SituacaoNota Situacao, int Quantidade, decimal Total);

public sealed record ValorAnteriorDto(decimal Valor, int Ano, int Mes, SituacaoNota Situacao);

public sealed record LinhaDto(
    long Id,
    OrigemNota Origem,
    long? RecorrenciaId,
    bool RecorrenciaAtiva,
    string Identificacao,
    string TomadorNome,
    string TomadorDocumento,
    string Descricao,
    decimal? Valor,
    ValorAnteriorDto? ValorAnterior,
    SituacaoNota Situacao,
    string? Mensagem,
    List<string> Pendencias,
    CamposVariaveis CamposVariaveis,
    CamposVariaveis CamposPendentesRevisao,
    CenarioSimulacao CenarioSimulacao,
    string? NumeroNfse,
    DateTimeOffset? EmitidaEm,
    int Tentativas,
    uint Versao);

public sealed record CompetenciaDto(
    int Ano,
    int Mes,
    bool Aberta,
    long? Id,
    DateTimeOffset? AbertaEm,
    int RecorrenciasAtivas,
    int RecorrenciasSemLinha,
    List<LinhaDto> Linhas,
    List<ResumoSituacaoDto> Resumo);

// ---------- Notas ----------
public sealed record TentativaDto(
    long Id,
    int Sequencia,
    TipoTentativa Tipo,
    ResultadoTentativa Resultado,
    DateTimeOffset IniciadaEm,
    DateTimeOffset? FinalizadaEm,
    string? IdentificadorDps,
    string Provedor,
    string? WorkerId,
    string? Detalhe,
    List<MensagemProvedor> Mensagens,
    string? NumeroNfse,
    string? ChaveAcesso,
    bool PossuiDadosEnviados);

public sealed record TrabalhoDto(
    long Id, StatusTrabalho Status, DateTimeOffset ProximaExecucaoEm, int Execucoes, int FalhasTemporarias,
    int ConsultasIndisponiveis, string? UltimaMensagem);

public sealed record NotaDetalheDto(
    long Id,
    long CompetenciaId,
    int Ano,
    int Mes,
    OrigemNota Origem,
    long? RecorrenciaId,
    string Identificacao,
    DadosTomador Tomador,
    DadosServico Servico,
    CamposVariaveis CamposVariaveis,
    CamposVariaveis CamposPendentesRevisao,
    decimal? Valor,
    ValorAnteriorDto? ValorAnterior,
    SituacaoNota Situacao,
    string? MotivoIgnorada,
    string? Mensagem,
    List<string> Pendencias,
    CenarioSimulacao CenarioSimulacao,
    string? SerieDps,
    long? NumeroDps,
    string? IdentificadorDps,
    DateOnly? DataCompetencia,
    DadosDps? Dps,
    string? NumeroNfse,
    string? ChaveAcesso,
    DateTimeOffset? EmitidaEm,
    bool EmissaoSimulada,
    bool PdfDisponivel,
    bool XmlDisponivel,
    bool PodeEditar,
    TrabalhoDto? Trabalho,
    List<TentativaDto> Tentativas,
    DateTimeOffset CriadaEm,
    DateTimeOffset AtualizadaEm,
    uint Versao);

public sealed record AtualizarNotaDto(
    uint Versao,
    decimal? Valor,
    string? Descricao,
    string? InformacoesComplementares,
    CenarioSimulacao? CenarioSimulacao,
    // Somente notas avulsas:
    string? Identificacao,
    DadosTomador? Tomador,
    DadosServico? Servico);

public sealed record AtualizarValorDto(uint Versao, decimal? Valor);

public sealed record CriarAvulsaDto(
    string Identificacao,
    DadosTomador Tomador,
    DadosServico Servico,
    decimal? Valor,
    CenarioSimulacao? CenarioSimulacao);

public sealed record IdsDto(List<long> Ids);
public sealed record CopiarValorAnteriorDto(List<long> Ids, bool Sobrescrever);
public sealed record IgnorarDto(List<long> Ids, string? Motivo);

public sealed record ResultadoItemDto(long Id, bool Sucesso, string Identificacao, string? Mensagem, List<string> Erros);
public sealed record ResultadoLoteDto(int Sucessos, int Falhas, List<ResultadoItemDto> Itens);

// ---------- Emissão ----------
public sealed record ItemPreviaDto(long Id, string Identificacao, string TomadorNome, string TomadorDocumento, decimal Valor);
public sealed record PreviaEmissaoDto(
    int Quantidade, decimal ValorTotal, List<ItemPreviaDto> Itens, List<string> Bloqueios, string Provedor, bool Simulacao);
public sealed record SolicitarEmissaoDto(Guid ChaveIdempotencia, List<long> NotaIds, decimal ValorTotal);
public sealed record LoteDto(long Id, Guid ChaveIdempotencia, int Quantidade, decimal ValorTotal, DateTimeOffset CriadoEm, bool Repetido);

// ---------- Sistema ----------
public sealed record StatusWorkerDto(string WorkerId, DateTimeOffset IniciadoEm, DateTimeOffset UltimoSinal, bool Ativo);
public sealed record SistemaDto(
    string Provedor, bool Simulacao, string Aviso, bool CertificadoConfigurado, List<StatusWorkerDto> Workers,
    int TrabalhosPendentes, string Versao);
