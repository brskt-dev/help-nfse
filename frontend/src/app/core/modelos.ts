export type SituacaoNota =
  | 'Pendente'
  | 'Pronta'
  | 'Processando'
  | 'Emitida'
  | 'Erro'
  | 'ResultadoAConfirmar'
  | 'Ignorada';

export const SITUACOES: SituacaoNota[] = [
  'Pendente',
  'Pronta',
  'Processando',
  'ResultadoAConfirmar',
  'Erro',
  'Emitida',
  'Ignorada',
];

export type OrigemNota = 'Recorrencia' | 'Avulsa';
export type CenarioSimulacao =
  | 'Sucesso'
  | 'RejeicaoFiscal'
  | 'FalhaTemporaria'
  | 'FalhaTemporariaPersistente'
  | 'TimeoutEmitida'
  | 'TimeoutNaoEmitida'
  | 'ConsultaIndisponivel'
  | 'InterromperWorker';
export type TipoTentativa = 'Envio' | 'Consulta';
export type ResultadoTentativa =
  | 'EmAndamento'
  | 'Autorizada'
  | 'Rejeitada'
  | 'FalhaTemporaria'
  | 'Incerta'
  | 'Interrompida'
  | 'DuplicidadeDetectada'
  | 'Encontrada'
  | 'NaoEncontrada'
  | 'ConsultaIndisponivel';
export type StatusTrabalho = 'Pendente' | 'EmExecucao' | 'Concluido' | 'Suspenso' | 'Cancelado';
export type OpcaoSimplesNacional = 'NaoInformado' | 'NaoOptante' | 'Mei' | 'MeEpp';
export type RegimeApuracaoSimples =
  | 'NaoInformado'
  | 'TributosFederaisEMunicipalPeloSimples'
  | 'TributosFederaisPeloSimplesIssqnFora'
  | 'TributosFederaisEMunicipalFora';
export type RegimeEspecialTributacao =
  | 'NaoInformado'
  | 'Nenhum'
  | 'AtoCooperado'
  | 'Estimativa'
  | 'MicroempresaMunicipal'
  | 'NotarioOuRegistrador'
  | 'ProfissionalAutonomo'
  | 'SociedadeDeProfissionais';
export type ReferenciaServico = 'MesAnterior' | 'MesmoMes';
export type RegraDataCompetencia = 'NaoDefinida' | 'UltimoDiaDoMes' | 'PrimeiroDiaDoMes' | 'DataDaEmissao';
export type TributacaoIssqn =
  | 'NaoInformado'
  | 'OperacaoTributavel'
  | 'Imunidade'
  | 'ExportacaoDeServico'
  | 'NaoIncidencia';

/** Flags (o backend serializa enum [Flags] como texto separado por vírgula). */
export type CamposVariaveisTexto = string;

export interface Endereco {
  cep?: string | null;
  logradouro?: string | null;
  numero?: string | null;
  complemento?: string | null;
  bairro?: string | null;
  codigoMunicipio?: string | null;
  nomeMunicipio?: string | null;
  uf?: string | null;
}

export interface DadosTomador {
  documento: string;
  nome: string;
  email?: string | null;
  telefone?: string | null;
  inscricaoMunicipal?: string | null;
  endereco: Endereco;
}

export interface DadosServico {
  descricao: string;
  codigoTributacaoNacional?: string | null;
  codigoTributacaoMunicipal?: string | null;
  codigoNbs?: string | null;
  codigoMunicipioPrestacao?: string | null;
  tributacaoIssqn: TributacaoIssqn;
  aliquotaIss?: number | null;
  issRetido: boolean;
  cstIbsCbs?: string | null;
  classificacaoTributariaIbsCbs?: string | null;
  indicadorOperacaoIbsCbs?: string | null;
  informacoesComplementares?: string | null;
}

export interface Sessao {
  autenticado: boolean;
  email: string | null;
}

export interface Emissor {
  configurado: boolean;
  razaoSocial: string;
  nomeFantasia?: string | null;
  cnpj: string;
  inscricaoMunicipal?: string | null;
  email?: string | null;
  telefone?: string | null;
  endereco: Endereco;
  opcaoSimplesNacional: OpcaoSimplesNacional;
  regimeApuracaoSimples: RegimeApuracaoSimples;
  regimeEspecialTributacao: RegimeEspecialTributacao;
  serieDps: string;
  proximoNumeroDps: number;
  regraDataCompetencia: RegraDataCompetencia;
  pendenciasConfirmacao: string[];
  atualizadoEm?: string | null;
}

export interface RecorrenciaResumo {
  id: number;
  identificacao: string;
  ativa: boolean;
  tomadorNome: string;
  tomadorDocumento: string;
  descricao: string;
  camposVariaveis: CamposVariaveisTexto;
  referenciaServico: ReferenciaServico;
  atualizadaEm: string;
}

export interface HistoricoRecorrencia {
  notaId: number;
  ano: number;
  mes: number;
  mesReferencia: string;
  valor: number | null;
  situacao: SituacaoNota;
  numeroNfse: string | null;
  emitidaEm: string | null;
}

export interface Recorrencia {
  id: number;
  identificacao: string;
  ativa: boolean;
  tomador: DadosTomador;
  servico: DadosServico;
  camposVariaveis: CamposVariaveisTexto;
  referenciaServico: ReferenciaServico;
  observacoes: string | null;
  criadaEm: string;
  atualizadaEm: string;
  desativadaEm: string | null;
  versao: number;
  historico: HistoricoRecorrencia[];
}

export interface ResumoSituacao {
  situacao: SituacaoNota;
  quantidade: number;
  total: number;
}

export interface ValorAnterior {
  valor: number;
  ano: number;
  mes: number;
  situacao: SituacaoNota;
}

export interface Linha {
  id: number;
  origem: OrigemNota;
  recorrenciaId: number | null;
  recorrenciaAtiva: boolean;
  identificacao: string;
  tomadorNome: string;
  tomadorDocumento: string;
  descricao: string;
  /** Mês de referência do serviço (AAAA-MM-01); o mês de emissão é o da página. */
  mesReferencia: string;
  valor: number | null;
  valorAnterior: ValorAnterior | null;
  situacao: SituacaoNota;
  mensagem: string | null;
  pendencias: string[];
  camposVariaveis: CamposVariaveisTexto;
  camposPendentesRevisao: CamposVariaveisTexto;
  cenarioSimulacao: CenarioSimulacao;
  numeroNfse: string | null;
  emitidaEm: string | null;
  tentativas: number;
  versao: number;
}

export interface CompetenciaResumo {
  id: number;
  ano: number;
  mes: number;
  abertaEm: string;
  resumo: ResumoSituacao[];
  quantidade: number;
  totalEmitido: number;
}

export interface Competencia {
  ano: number;
  mes: number;
  aberta: boolean;
  id: number | null;
  abertaEm: string | null;
  recorrenciasAtivas: number;
  recorrenciasSemLinha: number;
  linhas: Linha[];
  resumo: ResumoSituacao[];
}

export interface MensagemProvedor {
  codigo: string;
  descricao: string;
  complemento?: string | null;
}

export interface Tentativa {
  id: number;
  sequencia: number;
  tipo: TipoTentativa;
  resultado: ResultadoTentativa;
  iniciadaEm: string;
  finalizadaEm: string | null;
  identificadorDps: string | null;
  provedor: string;
  workerId: string | null;
  detalhe: string | null;
  mensagens: MensagemProvedor[];
  numeroNfse: string | null;
  chaveAcesso: string | null;
  possuiDadosEnviados: boolean;
}

export interface Trabalho {
  id: number;
  status: StatusTrabalho;
  proximaExecucaoEm: string;
  execucoes: number;
  falhasTemporarias: number;
  consultasIndisponiveis: number;
  ultimaMensagem: string | null;
}

export interface Dps {
  identificadorDps: string;
  serie: string;
  numero: number;
  dataCompetencia: string;
  competenciaAno: number;
  competenciaMes: number;
  mesReferenciaServico: string | null;
  confirmadaEm: string;
  prestador: {
    cnpj: string;
    razaoSocial: string;
    inscricaoMunicipal?: string | null;
    endereco: Endereco;
  };
  tomador: DadosTomador;
  servico: DadosServico;
  valor: number;
  provedor: string;
  simulacao: boolean;
}

export interface NotaDetalhe {
  id: number;
  competenciaId: number;
  ano: number;
  mes: number;
  origem: OrigemNota;
  recorrenciaId: number | null;
  identificacao: string;
  tomador: DadosTomador;
  servico: DadosServico;
  camposVariaveis: CamposVariaveisTexto;
  camposPendentesRevisao: CamposVariaveisTexto;
  mesReferencia: string;
  valor: number | null;
  valorAnterior: ValorAnterior | null;
  situacao: SituacaoNota;
  motivoIgnorada: string | null;
  mensagem: string | null;
  pendencias: string[];
  cenarioSimulacao: CenarioSimulacao;
  serieDps: string | null;
  numeroDps: number | null;
  identificadorDps: string | null;
  dataCompetencia: string | null;
  dps: Dps | null;
  numeroNfse: string | null;
  chaveAcesso: string | null;
  emitidaEm: string | null;
  emissaoSimulada: boolean;
  pdfDisponivel: boolean;
  xmlDisponivel: boolean;
  podeEditar: boolean;
  trabalho: Trabalho | null;
  tentativas: Tentativa[];
  criadaEm: string;
  atualizadaEm: string;
  versao: number;
}

export interface ResultadoItem {
  id: number;
  sucesso: boolean;
  identificacao: string;
  mensagem: string | null;
  erros: string[];
}

export interface ResultadoLote {
  sucessos: number;
  falhas: number;
  itens: ResultadoItem[];
}

export interface ItemPrevia {
  id: number;
  identificacao: string;
  tomadorNome: string;
  tomadorDocumento: string;
  valor: number;
}

export interface PreviaEmissao {
  quantidade: number;
  valorTotal: number;
  itens: ItemPrevia[];
  bloqueios: string[];
  provedor: string;
  simulacao: boolean;
}

export interface Lote {
  id: number;
  chaveIdempotencia: string;
  quantidade: number;
  valorTotal: number;
  criadoEm: string;
  repetido: boolean;
}

export interface LinhaResumo {
  id: number;
  situacao: SituacaoNota;
  valor: number | null;
  versao: number;
  voltouParaPendente: boolean;
}

export interface StatusWorker {
  workerId: string;
  iniciadoEm: string;
  ultimoSinal: string;
  ativo: boolean;
}

export interface Sistema {
  provedor: string;
  simulacao: boolean;
  aviso: string;
  certificadoConfigurado: boolean;
  workers: StatusWorker[];
  trabalhosPendentes: number;
  versao: string;
}

export function temCampo(campos: CamposVariaveisTexto | null | undefined, campo: string): boolean {
  return !!campos && campos.split(',').map((c) => c.trim()).includes(campo);
}

export function camposParaTexto(campos: string[]): CamposVariaveisTexto {
  return campos.length ? campos.join(', ') : 'Nenhum';
}
