using System.Net;
using System.Net.Http.Json;
using HelpNfse.Tests.Infra;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace HelpNfse.Tests;

/// <summary>Dados e documentos protegidos por autenticação; sem cadastro público; CSRF.</summary>
public class AutenticacaoTests(PostgresServidor servidor) : IAsyncLifetime
{
    private WebApplicationFactory<Program> _app = null!;

    public async ValueTask InitializeAsync()
    {
        var cs = await servidor.CriarBancoAsync();
        _app = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseEnvironment("Testing");
            // UseSetting: disponível já durante a montagem dos serviços em Program.cs.
            b.UseSetting("ConnectionStrings:Default", cs);
            b.UseSetting("Admin:Email", "admin@teste.local");
            b.UseSetting("Admin:Senha", "senha-de-teste-123");
            b.UseSetting("Demo:CarregarDados", "true");
        });
    }

    public async ValueTask DisposeAsync() => await _app.DisposeAsync();

    [Theory]
    [InlineData("/api/competencias")]
    [InlineData("/api/recorrencias")]
    [InlineData("/api/emissor")]
    [InlineData("/api/notas/1")]
    [InlineData("/api/notas/1/pdf")]
    [InlineData("/api/notas/1/xml")]
    public async Task Endpoints_de_dados_e_documentos_exigem_autenticacao(string url)
    {
        var client = _app.CreateClient();
        var r = await client.GetAsync(url);
        Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode);
    }

    [Fact]
    public async Task Login_exige_token_antiforgery_e_libera_downloads()
    {
        var client = _app.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        var sessao = await client.GetAsync("/api/auth/sessao");
        var token = ExtrairToken(sessao);

        var semToken = await client.PostAsJsonAsync("/api/auth/login", new { email = "admin@teste.local", senha = "senha-de-teste-123" });
        Assert.Equal(HttpStatusCode.BadRequest, semToken.StatusCode);

        var senhaErrada = await PostComToken(client, token, "/api/auth/login", new { email = "admin@teste.local", senha = "errada" });
        Assert.Equal(HttpStatusCode.Unauthorized, senhaErrada.StatusCode);

        var ok = await PostComToken(client, token, "/api/auth/login", new { email = "admin@teste.local", senha = "senha-de-teste-123" });
        Assert.Equal(HttpStatusCode.NoContent, ok.StatusCode);

        var comps = await client.GetAsync("/api/competencias");
        Assert.Equal(HttpStatusCode.OK, comps.StatusCode);

        // Nota emitida dos dados de demonstração: PDF disponível; XML fiscal não existe na simulação.
        var pdf = await client.GetAsync("/api/notas/1/pdf");
        Assert.Equal(HttpStatusCode.OK, pdf.StatusCode);
        Assert.Equal("application/pdf", pdf.Content.Headers.ContentType?.MediaType);
        var xml = await client.GetAsync("/api/notas/1/xml");
        Assert.Equal(HttpStatusCode.NotFound, xml.StatusCode);
    }

    [Fact]
    public async Task Nao_existe_cadastro_publico()
    {
        var client = _app.CreateClient();
        var r = await client.PostAsJsonAsync("/api/auth/register", new { email = "x@y.z", password = "1234567890" });
        Assert.NotEqual(HttpStatusCode.OK, r.StatusCode);
        Assert.NotEqual(HttpStatusCode.NoContent, r.StatusCode);
    }

    private static string ExtrairToken(HttpResponseMessage r) =>
        r.Headers.GetValues("Set-Cookie").First(c => c.StartsWith("XSRF-TOKEN=")).Split(';')[0]["XSRF-TOKEN=".Length..];

    private static Task<HttpResponseMessage> PostComToken(HttpClient c, string token, string url, object corpo)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(corpo) };
        req.Headers.Add("X-XSRF-TOKEN", token);
        return c.SendAsync(req);
    }
}
