namespace HelpNfse.Core.Configuracao;

/// <summary>Parâmetros do processamento da fila de emissão (seção "Emissao").</summary>
public sealed class OpcoesEmissao
{
    public const string Secao = "Emissao";

    /// <summary>Provedor ativo. Nesta entrega o único valor aceito é "Simulacao".</summary>
    public string Provedor { get; set; } = "Simulacao";

    /// <summary>Intervalo entre verificações da fila quando não há trabalho.</summary>
    public TimeSpan IntervaloPolling { get; set; } = TimeSpan.FromSeconds(2);

    /// <summary>Duração do bloqueio (lease) de um trabalho. Deve ser maior que o timeout do provedor.</summary>
    public TimeSpan DuracaoBloqueio { get; set; } = TimeSpan.FromMinutes(2);

    /// <summary>Tempo máximo de uma chamada ao provedor. Ao estourar, o resultado é tratado como incerto.</summary>
    public TimeSpan TimeoutProvedor { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>Quantidade de falhas temporárias (com certeza de não processamento) antes de marcar Erro.</summary>
    public int MaxFalhasTemporarias { get; set; } = 4;

    // Observação: listas sem valor padrão na propriedade, pois o binder de configuração acrescentaria aos itens existentes.

    /// <summary>Esperas entre novas tentativas após falha temporária (padrão: 10s, 30s, 1min, 5min).</summary>
    public TimeSpan[]? EsperasFalhaTemporaria { get; set; }

    /// <summary>Esperas entre consultas de reconciliação quando o resultado é incerto (padrão: 15s, 30s, 1min, 2min, 5min, 15min).</summary>
    public TimeSpan[]? EsperasConsulta { get; set; }

    public static readonly TimeSpan[] PadraoEsperasFalhaTemporaria =
        [TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30), TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5)];

    public static readonly TimeSpan[] PadraoEsperasConsulta =
        [TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(30), TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(15)];

    /// <summary>Consultas indisponíveis seguidas antes de suspender e aguardar ação do usuário.</summary>
    public int MaxConsultasIndisponiveis { get; set; } = 8;

    /// <summary>
    /// Tempo mínimo entre um envio incerto e a consulta que autoriza o reenvio (evita reenviar
    /// enquanto o serviço remoto ainda pode estar processando).
    /// </summary>
    public TimeSpan JanelaMinimaReenvio { get; set; } = TimeSpan.FromMinutes(2);

    /// <summary>Identificador estável do worker (padrão: nome da máquina/container).</summary>
    public string? WorkerId { get; set; }
}

/// <summary>Parâmetros do provedor simulado (seção "Simulacao").</summary>
public sealed class OpcoesSimulacao
{
    public const string Secao = "Simulacao";

    /// <summary>Latência artificial por chamada, para tornar o processamento visível na interface.</summary>
    public TimeSpan Latencia { get; set; } = TimeSpan.FromMilliseconds(800);

    /// <summary>Permite que o cenário "InterromperWorker" encerre o processo do worker de verdade.</summary>
    public bool PermitirInterrupcaoReal { get; set; } = true;
}

/// <summary>
/// Configuração futura do certificado A1 (seção "Certificado"). Não é usada pela simulação.
/// O arquivo e a senha devem vir de secrets do servidor, nunca do repositório nem de logs.
/// </summary>
public sealed class OpcoesCertificado
{
    public const string Secao = "Certificado";

    /// <summary>Caminho de um arquivo .pfx montado no container (ex.: /run/secrets/certificado.pfx).</summary>
    public string? CaminhoArquivo { get; set; }

    /// <summary>Alternativa ao arquivo: conteúdo do .pfx em base64, vindo de variável de ambiente secreta.</summary>
    public string? ConteudoBase64 { get; set; }

    /// <summary>Caminho de um arquivo contendo a senha do certificado.</summary>
    public string? CaminhoArquivoSenha { get; set; }

    /// <summary>Alternativa ao arquivo de senha: senha em variável de ambiente secreta.</summary>
    public string? Senha { get; set; }

    public bool Configurado =>
        !string.IsNullOrWhiteSpace(CaminhoArquivo) || !string.IsNullOrWhiteSpace(ConteudoBase64);

    /// <summary>Evita que a senha ou o conteúdo apareçam em logs por ToString/serialização acidental.</summary>
    public override string ToString() => $"Certificado(configurado={Configurado})";
}

/// <summary>Carga de dados fictícios de demonstração (seção "Demo").</summary>
public sealed class OpcoesDemo
{
    public const string Secao = "Demo";
    public bool CarregarDados { get; set; }
}
