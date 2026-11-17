using System.Text.Json;
using HelpNfse.Core.Dominio;
using HelpNfse.Core.Provedores.Simulacao;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HelpNfse.Core.Dados;

public class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<IdentityUser>(options), IDataProtectionKeyContext
{
    public DbSet<Emissor> Emissores => Set<Emissor>();
    public DbSet<Recorrencia> Recorrencias => Set<Recorrencia>();
    public DbSet<Competencia> Competencias => Set<Competencia>();
    public DbSet<Nota> Notas => Set<Nota>();
    public DbSet<LoteEmissao> Lotes => Set<LoteEmissao>();
    public DbSet<TrabalhoEmissao> Trabalhos => Set<TrabalhoEmissao>();
    public DbSet<TentativaEmissao> Tentativas => Set<TentativaEmissao>();
    public DbSet<StatusWorker> StatusWorkers => Set<StatusWorker>();
    public DbSet<SimuladorRegistro> SimuladorRegistros => Set<SimuladorRegistro>();
    public DbSet<DataProtectionKey> DataProtectionKeys { get; set; } = null!;

    protected override void ConfigureConventions(ModelConfigurationBuilder builder)
    {
        // Enums gravados como texto: legível no banco e estável a reordenações.
        builder.Properties<SituacaoNota>().HaveConversion<string>().HaveMaxLength(30);
        builder.Properties<OrigemNota>().HaveConversion<string>().HaveMaxLength(20);
        builder.Properties<CenarioSimulacao>().HaveConversion<string>().HaveMaxLength(40);
        builder.Properties<TipoTentativa>().HaveConversion<string>().HaveMaxLength(20);
        builder.Properties<ResultadoTentativa>().HaveConversion<string>().HaveMaxLength(30);
        builder.Properties<StatusTrabalho>().HaveConversion<string>().HaveMaxLength(20);
        builder.Properties<OpcaoSimplesNacional>().HaveConversion<string>().HaveMaxLength(40);
        builder.Properties<RegimeApuracaoSimples>().HaveConversion<string>().HaveMaxLength(60);
        builder.Properties<RegimeEspecialTributacao>().HaveConversion<string>().HaveMaxLength(40);
        builder.Properties<RegraDataCompetencia>().HaveConversion<string>().HaveMaxLength(30);
        builder.Properties<TributacaoIssqn>().HaveConversion<string>().HaveMaxLength(30);
        builder.Properties<decimal>().HavePrecision(15, 2);
    }

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        b.Entity<Emissor>(e =>
        {
            e.ToTable("emissor");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.RazaoSocial).HasMaxLength(150);
            e.Property(x => x.NomeFantasia).HasMaxLength(150);
            e.Property(x => x.Cnpj).HasMaxLength(14);
            e.Property(x => x.InscricaoMunicipal).HasMaxLength(30);
            e.Property(x => x.Email).HasMaxLength(150);
            e.Property(x => x.Telefone).HasMaxLength(20);
            e.Property(x => x.SerieDps).HasMaxLength(5);
            e.ComplexProperty(x => x.Endereco, MapearEndereco);
        });

        b.Entity<Recorrencia>(e =>
        {
            e.ToTable("recorrencias");
            e.Property(x => x.Identificacao).HasMaxLength(120);
            e.HasIndex(x => x.Identificacao).IsUnique();
            e.Property(x => x.Observacoes).HasMaxLength(1000);
            e.Property(x => x.CamposVariaveis).HasConversion<int>();
            e.Property(x => x.Versao).IsRowVersion();
            e.ComplexProperty(x => x.Tomador, MapearTomador);
            e.ComplexProperty(x => x.Servico, MapearServico);
        });

        b.Entity<Competencia>(e =>
        {
            e.ToTable("competencias");
            e.HasIndex(x => new { x.Ano, x.Mes }).IsUnique();
            e.Ignore(x => x.Chave);
            e.Ignore(x => x.Rotulo);
            e.HasMany(x => x.Notas).WithOne(x => x.Competencia).HasForeignKey(x => x.CompetenciaId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<Nota>(e =>
        {
            e.ToTable("notas");
            e.Property(x => x.Identificacao).HasMaxLength(120);
            e.Property(x => x.CamposVariaveis).HasConversion<int>();
            e.Property(x => x.CamposRevisados).HasConversion<int>();
            e.Property(x => x.MotivoIgnorada).HasMaxLength(300);
            e.Property(x => x.SerieDps).HasMaxLength(5);
            e.Property(x => x.IdentificadorDps).HasMaxLength(80);
            e.Property(x => x.DpsJson).HasColumnType("jsonb");
            e.Property(x => x.NumeroNfse).HasMaxLength(40);
            e.Property(x => x.ChaveAcesso).HasMaxLength(80);
            e.Property(x => x.UltimaMensagem).HasMaxLength(2000);
            e.Property(x => x.Versao).IsRowVersion();
            e.ComplexProperty(x => x.Tomador, MapearTomador);
            e.ComplexProperty(x => x.Servico, MapearServico);
            e.Ignore(x => x.EstaBloqueadaParaEdicao);
            e.Ignore(x => x.CamposPendentesDeRevisao);

            e.HasOne(x => x.Recorrencia).WithMany().HasForeignKey(x => x.RecorrenciaId)
                .OnDelete(DeleteBehavior.Restrict);
            // Geração idempotente: no máximo uma linha por recorrência em cada competência.
            e.HasIndex(x => new { x.CompetenciaId, x.RecorrenciaId }).IsUnique()
                .HasFilter("recorrencia_id IS NOT NULL");
            e.HasIndex(x => x.IdentificadorDps).IsUnique().HasFilter("identificador_dps IS NOT NULL");
            e.HasIndex(x => x.Situacao);
            e.HasMany(x => x.Tentativas).WithOne().HasForeignKey(x => x.NotaId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<LoteEmissao>(e =>
        {
            e.ToTable("lotes_emissao");
            e.HasIndex(x => x.ChaveIdempotencia).IsUnique();
            e.Property(x => x.SolicitadoPor).HasMaxLength(150);
            e.HasOne<Competencia>().WithMany().HasForeignKey(x => x.CompetenciaId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<TrabalhoEmissao>(e =>
        {
            e.ToTable("trabalhos_emissao");
            e.Property(x => x.BloqueadoPor).HasMaxLength(120);
            e.Property(x => x.UltimaMensagem).HasMaxLength(2000);
            e.HasOne(x => x.Nota).WithMany().HasForeignKey(x => x.NotaId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<LoteEmissao>().WithMany().HasForeignKey(x => x.LoteId).OnDelete(DeleteBehavior.Restrict);
            // No máximo um trabalho ativo por nota: impede processamento duplicado.
            e.HasIndex(x => x.NotaId).IsUnique()
                .HasFilter("status IN ('Pendente', 'EmExecucao', 'Suspenso')")
                .HasDatabaseName("ux_trabalhos_emissao_nota_ativa");
            e.HasIndex(x => new { x.Status, x.ProximaExecucaoEm });
        });

        b.Entity<TentativaEmissao>(e =>
        {
            e.ToTable("tentativas_emissao");
            e.Property(x => x.IdentificadorDps).HasMaxLength(80);
            e.Property(x => x.Provedor).HasMaxLength(40);
            e.Property(x => x.WorkerId).HasMaxLength(120);
            e.Property(x => x.Detalhe).HasMaxLength(2000);
            e.Property(x => x.DadosEnviadosJson).HasColumnType("jsonb");
            e.Property(x => x.NumeroNfse).HasMaxLength(40);
            e.Property(x => x.ChaveAcesso).HasMaxLength(80);
            e.HasIndex(x => new { x.NotaId, x.Sequencia }).IsUnique();
            e.Property(x => x.Mensagens)
                .HasColumnType("jsonb")
                .HasConversion(
                    v => JsonSerializer.Serialize(v, JsonOpcoes.Padrao),
                    s => JsonSerializer.Deserialize<List<MensagemProvedor>>(s, JsonOpcoes.Padrao) ?? new(),
                    new ValueComparer<List<MensagemProvedor>>(
                        (a, c) => JsonSerializer.Serialize(a, JsonOpcoes.Padrao) == JsonSerializer.Serialize(c, JsonOpcoes.Padrao),
                        v => JsonSerializer.Serialize(v, JsonOpcoes.Padrao).GetHashCode(),
                        v => v.ToList()));
        });

        b.Entity<StatusWorker>(e =>
        {
            e.ToTable("status_workers");
            e.HasKey(x => x.WorkerId);
            e.Property(x => x.WorkerId).HasMaxLength(120);
            e.Property(x => x.Provedor).HasMaxLength(40);
        });

        b.Entity<SimuladorRegistro>(e =>
        {
            e.ToTable("simulador_registros");
            e.HasKey(x => x.IdentificadorDps);
            e.Property(x => x.IdentificadorDps).HasMaxLength(80);
            e.Property(x => x.NumeroNfse).HasMaxLength(40);
            e.Property(x => x.ChaveAcesso).HasMaxLength(80);
        });

        b.HasSequence<long>("simulador_numero_nfse");
    }

    private static void MapearEndereco(ComplexPropertyBuilder<Endereco> e)
    {
        e.Property(x => x.Cep).HasMaxLength(8);
        e.Property(x => x.Logradouro).HasMaxLength(150);
        e.Property(x => x.Numero).HasMaxLength(20);
        e.Property(x => x.Complemento).HasMaxLength(100);
        e.Property(x => x.Bairro).HasMaxLength(100);
        e.Property(x => x.CodigoMunicipio).HasMaxLength(7);
        e.Property(x => x.NomeMunicipio).HasMaxLength(100);
        e.Property(x => x.Uf).HasMaxLength(2);
    }

    private static void MapearTomador(ComplexPropertyBuilder<DadosTomador> e)
    {
        e.Property(x => x.Documento).HasMaxLength(14);
        e.Property(x => x.Nome).HasMaxLength(150);
        e.Property(x => x.Email).HasMaxLength(150);
        e.Property(x => x.Telefone).HasMaxLength(20);
        e.Property(x => x.InscricaoMunicipal).HasMaxLength(30);
        e.ComplexProperty(x => x.Endereco, MapearEndereco);
    }

    private static void MapearServico(ComplexPropertyBuilder<DadosServico> e)
    {
        e.Property(x => x.Descricao).HasMaxLength(2000);
        e.Property(x => x.CodigoTributacaoNacional).HasMaxLength(6);
        e.Property(x => x.CodigoTributacaoMunicipal).HasMaxLength(20);
        e.Property(x => x.CodigoNbs).HasMaxLength(9);
        e.Property(x => x.CodigoMunicipioPrestacao).HasMaxLength(7);
        e.Property(x => x.AliquotaIss).HasPrecision(5, 2);
        e.Property(x => x.InformacoesComplementares).HasMaxLength(2000);
    }
}

public static class JsonOpcoes
{
    public static readonly JsonSerializerOptions Padrao = new(JsonSerializerDefaults.Web)
    {
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
    };
}
