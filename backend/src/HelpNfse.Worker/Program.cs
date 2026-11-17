using HelpNfse.Core;
using HelpNfse.Core.Provedores.Simulacao;
using HelpNfse.Worker;

var builder = Host.CreateApplicationBuilder(args);

// No worker real, o cenário "InterromperWorker" encerra o processo de verdade (simula uma queda).
builder.Services.AddSingleton<IInterruptorProcesso, InterruptorProcessoReal>();
builder.Services.AddHelpNfseCore(builder.Configuration);
builder.Services.AddHostedService<ServicoFilaEmissao>();
// Dá tempo para a chamada em andamento ao provedor terminar e ter o resultado registrado.
builder.Services.Configure<HostOptions>(o => o.ShutdownTimeout = TimeSpan.FromSeconds(45));

var host = builder.Build();
host.Run();
