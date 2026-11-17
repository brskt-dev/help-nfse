using HelpNfse.Core;
using HelpNfse.Core.Dados;
using HelpNfse.Core.Dominio;
using HelpNfse.Core.Emissao;
using HelpNfse.Core.Servicos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HelpNfse.Tests.Infra;

public sealed class RelogioTeste : TimeProvider
{
    public DateTimeOffset Agora { get; set; } = new(2026, 11, 17, 15, 0, 0, TimeSpan.Zero);
    public override DateTimeOffset GetUtcNow() => Agora;
    public void Avancar(TimeSpan t) => Agora += t;
}

/// <summary>Contêiner de serviços do Core apontando para um banco exclusivo do teste.</summary>
public sealed class Ambiente : IAsyncDisposable
{
    public required ServiceProvider Sp { get; init; }
    public required RelogioTeste Relogio { get; init; }
    public required string ConnectionString { get; init; }

    public static readonly TimeSpan JanelaReenvio = TimeSpan.FromMinutes(2);
    public static readonly TimeSpan Bloqueio = TimeSpan.FromMinutes(2);

    public static async Task<Ambiente> CriarAsync(PostgresServidor servidor, Dictionary<string, string?>? extra = null,
        Action<IServiceCollection>? configurar = null)
    {
        var cs = await servidor.CriarBancoAsync();
        var valores = new Dictionary<string, string?>
        {
            ["ConnectionStrings:Default"] = cs,
            ["Simulacao:Latencia"] = "00:00:00",
            ["Simulacao:PermitirInterrupcaoReal"] = "false",
            ["Emissao:JanelaMinimaReenvio"] = JanelaReenvio.ToString(),
            ["Emissao:DuracaoBloqueio"] = Bloqueio.ToString(),
            ["Emissao:MaxFalhasTemporarias"] = "3",
            ["Emissao:MaxConsultasIndisponiveis"] = "5",
            ["Emissao:EsperasFalhaTemporaria:0"] = "00:00:10",
            ["Emissao:EsperasConsulta:0"] = "00:00:30",
        };
        foreach (var (k, v) in extra ?? []) valores[k] = v;
        var config = new ConfigurationBuilder().AddInMemoryCollection(valores).Build();

        var relogio = new RelogioTeste();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<TimeProvider>(relogio);
        services.AddHelpNfseCore(config);
        configurar?.Invoke(services);
        return new Ambiente { Sp = services.BuildServiceProvider(), Relogio = relogio, ConnectionString = cs };
    }

    public AppDbContext NovoDb() => Sp.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContext();

    public async Task<TR> Com<T, TR>(Func<T, Task<TR>> acao) where T : notnull
    {
        await using var scope = Sp.CreateAsyncScope();
        return await acao(scope.ServiceProvider.GetRequiredService<T>());
    }

    public async Task Com<T>(Func<T, Task> acao) where T : notnull
    {
        await using var scope = Sp.CreateAsyncScope();
        await acao(scope.ServiceProvider.GetRequiredService<T>());
    }

    public ProcessadorEmissao Processador => Sp.GetRequiredService<ProcessadorEmissao>();

    // ---------- Cenários ----------

    public Task<EmissorDto> CriarEmissorAsync() => Com<EmissorServico, EmissorDto>(s => s.SalvarAsync(new SalvarEmissorDto(
        "Prestador Teste Ltda", null, "11222333000181", "123", null, null,
        new Endereco { CodigoMunicipio = "3550308", NomeMunicipio = "São Paulo", Uf = "SP", Cep = "01001000" },
        OpcaoSimplesNacional.MeEpp, RegimeApuracaoSimples.TributosFederaisEMunicipalPeloSimples, RegimeEspecialTributacao.Nenhum,
        "1", 1, RegraDataCompetencia.UltimoDiaDoMes), default));

    private static int _seqDoc;

    public static string CnpjValido()
    {
        // Gera CNPJ com dígitos verificadores corretos a partir de uma base sequencial.
        var n = Interlocked.Increment(ref _seqDoc);
        var b = $"{10000000 + n:D8}0001";
        int Dv(string s, int[] p) { var soma = s.Select((c, i) => (c - '0') * p[i]).Sum(); var r = soma % 11; return r < 2 ? 0 : 11 - r; }
        var d1 = Dv(b, [5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2]);
        var d2 = Dv(b + d1, [6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2]);
        return $"{b}{d1}{d2}";
    }

    public Task<RecorrenciaDto> CriarRecorrenciaAsync(string nome, CamposVariaveis variaveis = CamposVariaveis.Nenhum,
        string descricao = "Serviço mensal {competencia}", ReferenciaServico referencia = ReferenciaServico.MesAnterior) =>
        Com<RecorrenciaServico, RecorrenciaDto>(s => s.CriarAsync(new SalvarRecorrenciaDto(
            $"Rec {nome}",
            new DadosTomador { Documento = CnpjValido(), Nome = nome },
            new DadosServico
            {
                Descricao = descricao, CodigoTributacaoNacional = "010101", CodigoMunicipioPrestacao = "3550308",
                TributacaoIssqn = TributacaoIssqn.OperacaoTributavel,
            },
            variaveis, referencia, null, null), default));

    public Task<CompetenciaDto> AbrirAsync(int ano = 2026, int mes = 11) =>
        Com<CompetenciaServico, CompetenciaDto>(s => s.AbrirAsync(ano, mes, default));

    public Task<CompetenciaDto> ObterAsync(int ano = 2026, int mes = 11) =>
        Com<CompetenciaServico, CompetenciaDto>(s => s.ObterAsync(ano, mes, default));

    public async Task DefinirValorAsync(long notaId, decimal valor)
    {
        await using var db = NovoDb();
        var versao = (await db.Notas.SingleAsync(n => n.Id == notaId)).Versao;
        await Com<NotaServico>(s => s.AtualizarValorAsync(notaId, new AtualizarValorDto(versao, valor), default));
    }

    public async Task DefinirCenarioAsync(long notaId, CenarioSimulacao cenario)
    {
        await using var db = NovoDb();
        var n = await db.Notas.SingleAsync(x => x.Id == notaId);
        await Com<NotaServico>(s => s.AtualizarAsync(notaId,
            new AtualizarNotaDto(n.Versao, n.Valor, null, null, null, cenario, null, null, null), default));
    }

    public Task<ResultadoLoteDto> MarcarProntasAsync(params long[] ids) =>
        Com<NotaServico, ResultadoLoteDto>(s => s.MarcarProntasAsync(ids.ToList(), default));

    /// <summary>Cria emissor + N recorrências, abre a competência, preenche valores e marca tudo como pronto.</summary>
    public async Task<(long CompetenciaId, List<long> Notas)> PrepararProntasAsync(int quantidade, decimal valor = 100m,
        CenarioSimulacao cenario = CenarioSimulacao.Sucesso)
    {
        await CriarEmissorAsync();
        for (var i = 0; i < quantidade; i++) await CriarRecorrenciaAsync($"Cliente {i:D2}");
        var comp = await AbrirAsync();
        var ids = comp.Linhas.Select(l => l.Id).ToList();
        foreach (var id in ids)
        {
            await DefinirValorAsync(id, valor);
            if (cenario != CenarioSimulacao.Sucesso) await DefinirCenarioAsync(id, cenario);
        }
        var r = await MarcarProntasAsync(ids.ToArray());
        if (r.Falhas > 0) throw new InvalidOperationException("Falha ao preparar: " + string.Join("; ", r.Itens.SelectMany(i => i.Erros)));
        return (comp.Id!.Value, ids);
    }

    public async Task<LoteDto> SolicitarEmissaoAsync(long competenciaId, Guid? chave = null)
    {
        var previa = await Com<EmissaoServico, PreviaEmissaoDto>(s => s.PreviaAsync(competenciaId, default));
        return await Com<EmissaoServico, LoteDto>(s => s.SolicitarAsync(competenciaId,
            new SolicitarEmissaoDto(chave ?? Guid.NewGuid(), previa.Itens.Select(i => i.Id).ToList(), previa.ValorTotal), "teste", default));
    }

    /// <summary>Processa a fila até não haver trabalho disponível no instante atual do relógio.</summary>
    public async Task<int> ProcessarDisponiveisAsync(string worker = "worker-teste", int limite = 200)
    {
        var n = 0;
        while (n < limite && await Processador.ProcessarProximoAsync(worker, default)) n++;
        return n;
    }

    public async Task<Nota> NotaAsync(long id)
    {
        await using var db = NovoDb();
        return await db.Notas.AsNoTracking().Include(n => n.Tentativas).SingleAsync(n => n.Id == id);
    }

    public async Task<int> ChamadasEnvioSimuladorAsync(long notaId)
    {
        await using var db = NovoDb();
        var nota = await db.Notas.AsNoTracking().SingleAsync(n => n.Id == notaId);
        var reg = await db.SimuladorRegistros.AsNoTracking().SingleOrDefaultAsync(r => r.IdentificadorDps == nota.IdentificadorDps);
        return reg?.ChamadasEnvio ?? 0;
    }

    public async ValueTask DisposeAsync() => await Sp.DisposeAsync();
}
