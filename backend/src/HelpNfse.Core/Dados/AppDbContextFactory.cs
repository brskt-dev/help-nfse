using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace HelpNfse.Core.Dados;

/// <summary>Usado apenas pelas ferramentas do EF (dotnet ef migrations add). Não conecta ao banco.</summary>
public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var cs = Environment.GetEnvironmentVariable("ConnectionStrings__Migrations")
                 ?? "Host=localhost;Database=helpnfse;Username=helpnfse";
        var opts = new DbContextOptionsBuilder<AppDbContext>();
        ConfiguracaoServicos.ConfigurarBanco(opts, cs);
        return new AppDbContext(opts.Options);
    }
}
