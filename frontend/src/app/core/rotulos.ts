import {
  CenarioSimulacao,
  OpcaoSimplesNacional,
  RegimeApuracaoSimples,
  RegimeEspecialTributacao,
  RegraDataCompetencia,
  ReferenciaServico,
  ResultadoTentativa,
  SituacaoNota,
  StatusTrabalho,
  TributacaoIssqn,
} from './modelos';

export const ROTULO_SITUACAO: Record<SituacaoNota, string> = {
  Pendente: 'Pendente',
  Pronta: 'Pronta',
  Processando: 'Processando',
  Emitida: 'Emitida',
  Erro: 'Erro',
  ResultadoAConfirmar: 'Resultado a confirmar',
  Ignorada: 'Ignorada',
};

export const CENARIOS: { valor: CenarioSimulacao; rotulo: string; descricao: string }[] = [
  { valor: 'Sucesso', rotulo: 'Sucesso', descricao: 'A nota é autorizada no primeiro envio.' },
  { valor: 'RejeicaoFiscal', rotulo: 'Rejeição fiscal', descricao: 'O provedor rejeita a DPS. A linha vai para Erro e pode ser corrigida.' },
  { valor: 'FalhaTemporaria', rotulo: 'Falha temporária (1x)', descricao: 'O serviço falha sem processar; a linha vai para Erro (nenhuma nota emitida). Reenvio manual: marque como pronta e emita de novo — a segunda tentativa é autorizada.' },
  { valor: 'FalhaTemporariaPersistente', rotulo: 'Falha temporária persistente', descricao: 'O serviço continua indisponível: cada envio termina em Erro, sem nota emitida.' },
  { valor: 'TimeoutEmitida', rotulo: 'Timeout — nota emitida', descricao: 'O provedor emite, mas a resposta não chega. A consulta posterior confirma a emissão, sem reenvio.' },
  { valor: 'TimeoutNaoEmitida', rotulo: 'Timeout — nota não emitida', descricao: 'A requisição se perde. A consulta confirma que nada foi emitido e a linha vai para Erro; o reenvio é manual e usa o mesmo número de DPS.' },
  { valor: 'ConsultaIndisponivel', rotulo: 'Timeout + consulta indisponível', descricao: 'Resultado incerto e as duas primeiras consultas falham. A linha permanece "Resultado a confirmar" até a consulta funcionar.' },
  { valor: 'InterromperWorker', rotulo: 'Interromper o worker', descricao: 'O provedor emite e o processo do worker é encerrado antes de registrar a resposta. Ao reiniciar, o worker reconcilia por consulta.' },
];

export const ROTULO_CENARIO: Record<CenarioSimulacao, string> = Object.fromEntries(
  CENARIOS.map((c) => [c.valor, c.rotulo]),
) as Record<CenarioSimulacao, string>;

export const OPCOES_REFERENCIA: { valor: ReferenciaServico; rotulo: string }[] = [
  { valor: 'MesAnterior', rotulo: 'Mês anterior ao da emissão' },
  { valor: 'MesmoMes', rotulo: 'Mesmo mês da emissão' },
];

export const ROTULO_RESULTADO: Record<ResultadoTentativa, string> = {
  EmAndamento: 'Em andamento',
  Autorizada: 'Autorizada',
  Rejeitada: 'Rejeitada',
  FalhaTemporaria: 'Falha temporária',
  Incerta: 'Resultado incerto',
  Interrompida: 'Interrompida',
  DuplicidadeDetectada: 'Duplicidade detectada',
  Encontrada: 'Encontrada na consulta',
  NaoEncontrada: 'Não encontrada na consulta',
  ConsultaIndisponivel: 'Consulta indisponível',
};

export const ROTULO_STATUS_TRABALHO: Record<StatusTrabalho, string> = {
  Pendente: 'Na fila',
  EmExecucao: 'Em execução',
  Concluido: 'Concluído',
  Suspenso: 'Suspenso (aguarda ação)',
  Cancelado: 'Cancelado',
};

export const OPCOES_SIMPLES: { valor: OpcaoSimplesNacional; rotulo: string }[] = [
  { valor: 'NaoInformado', rotulo: 'Não informado' },
  { valor: 'NaoOptante', rotulo: 'Não optante' },
  { valor: 'Mei', rotulo: 'Optante — MEI' },
  { valor: 'MeEpp', rotulo: 'Optante — ME/EPP' },
];

export const OPCOES_APURACAO: { valor: RegimeApuracaoSimples; rotulo: string }[] = [
  { valor: 'NaoInformado', rotulo: 'Não informado' },
  { valor: 'TributosFederaisEMunicipalPeloSimples', rotulo: 'Federais e municipal pelo Simples Nacional' },
  { valor: 'TributosFederaisPeloSimplesIssqnFora', rotulo: 'Federais pelo Simples; ISSQN fora do Simples' },
  { valor: 'TributosFederaisEMunicipalFora', rotulo: 'Federais e municipal fora do Simples' },
];

export const OPCOES_REGIME_ESPECIAL: { valor: RegimeEspecialTributacao; rotulo: string }[] = [
  { valor: 'NaoInformado', rotulo: 'Não informado' },
  { valor: 'Nenhum', rotulo: 'Nenhum' },
  { valor: 'AtoCooperado', rotulo: 'Ato cooperado' },
  { valor: 'Estimativa', rotulo: 'Estimativa' },
  { valor: 'MicroempresaMunicipal', rotulo: 'Microempresa municipal' },
  { valor: 'NotarioOuRegistrador', rotulo: 'Notário ou registrador' },
  { valor: 'ProfissionalAutonomo', rotulo: 'Profissional autônomo' },
  { valor: 'SociedadeDeProfissionais', rotulo: 'Sociedade de profissionais' },
];

export const OPCOES_DATA_COMPETENCIA: { valor: RegraDataCompetencia; rotulo: string }[] = [
  { valor: 'NaoDefinida', rotulo: 'Não definida' },
  { valor: 'UltimoDiaDoMes', rotulo: 'Último dia do mês da competência' },
  { valor: 'PrimeiroDiaDoMes', rotulo: 'Primeiro dia do mês da competência' },
  { valor: 'DataDaEmissao', rotulo: 'Data da emissão' },
];

export const OPCOES_TRIBUTACAO: { valor: TributacaoIssqn; rotulo: string }[] = [
  { valor: 'NaoInformado', rotulo: 'Não informado' },
  { valor: 'OperacaoTributavel', rotulo: 'Operação tributável' },
  { valor: 'Imunidade', rotulo: 'Imunidade' },
  { valor: 'ExportacaoDeServico', rotulo: 'Exportação de serviço' },
  { valor: 'NaoIncidencia', rotulo: 'Não incidência' },
];
