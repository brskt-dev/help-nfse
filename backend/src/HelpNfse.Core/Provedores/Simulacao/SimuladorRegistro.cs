using HelpNfse.Core.Dominio;

namespace HelpNfse.Core.Provedores.Simulacao;

/// <summary>
/// Estado do "servidor" simulado. Representa o que o serviço remoto saberia sobre cada DPS,
/// independentemente do que o cliente (worker) conseguiu registrar. Permite simular timeouts com emissão
/// efetivada e a reconciliação posterior por consulta.
/// </summary>
public class SimuladorRegistro
{
    public string IdentificadorDps { get; set; } = "";
    public CenarioSimulacao Cenario { get; set; }
    public bool Emitida { get; set; }
    public string? NumeroNfse { get; set; }
    public string? ChaveAcesso { get; set; }
    public DateTimeOffset? EmitidaEm { get; set; }
    public int ChamadasEnvio { get; set; }
    public int ChamadasConsulta { get; set; }
    public int FalhasGeradas { get; set; }
    public bool InterrupcaoExecutada { get; set; }
    public DateTimeOffset CriadoEm { get; set; }
    public DateTimeOffset AtualizadoEm { get; set; }
}
