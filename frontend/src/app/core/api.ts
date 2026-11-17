import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import {
  Competencia,
  CompetenciaResumo,
  DadosServico,
  DadosTomador,
  Emissor,
  Linha,
  LinhaResumo,
  Lote,
  NotaDetalhe,
  PreviaEmissao,
  Recorrencia,
  RecorrenciaResumo,
  ResultadoLote,
  CenarioSimulacao,
  Sistema,
} from './modelos';

export interface SalvarRecorrencia {
  identificacao: string;
  tomador: DadosTomador;
  servico: DadosServico;
  camposVariaveis: string;
  observacoes: string | null;
  versao: number | null;
}

export interface AtualizarNota {
  versao: number;
  valor: number | null;
  descricao?: string | null;
  informacoesComplementares?: string | null;
  cenarioSimulacao?: CenarioSimulacao | null;
  identificacao?: string | null;
  tomador?: DadosTomador | null;
  servico?: DadosServico | null;
}

export interface CriarAvulsa {
  identificacao: string;
  tomador: DadosTomador;
  servico: DadosServico;
  valor: number | null;
  cenarioSimulacao: CenarioSimulacao | null;
}

/** Cliente tipado da API. Todas as rotas exigem sessão autenticada, exceto /auth. */
@Injectable({ providedIn: 'root' })
export class Api {
  private readonly http = inject(HttpClient);

  sistema(): Observable<Sistema> {
    return this.http.get<Sistema>('/api/sistema');
  }

  // Emissor
  emissor(): Observable<Emissor> {
    return this.http.get<Emissor>('/api/emissor');
  }
  salvarEmissor(dto: Omit<Emissor, 'configurado' | 'pendenciasConfirmacao' | 'atualizadoEm'>): Observable<Emissor> {
    return this.http.put<Emissor>('/api/emissor', dto);
  }

  // Recorrências
  recorrencias(incluirInativas = true): Observable<RecorrenciaResumo[]> {
    return this.http.get<RecorrenciaResumo[]>('/api/recorrencias', {
      params: new HttpParams().set('incluirInativas', incluirInativas),
    });
  }
  recorrencia(id: number): Observable<Recorrencia> {
    return this.http.get<Recorrencia>(`/api/recorrencias/${id}`);
  }
  criarRecorrencia(dto: SalvarRecorrencia): Observable<Recorrencia> {
    return this.http.post<Recorrencia>('/api/recorrencias', dto);
  }
  atualizarRecorrencia(id: number, dto: SalvarRecorrencia): Observable<Recorrencia> {
    return this.http.put<Recorrencia>(`/api/recorrencias/${id}`, dto);
  }
  definirAtiva(id: number, ativa: boolean): Observable<void> {
    return this.http.post<void>(`/api/recorrencias/${id}/${ativa ? 'reativar' : 'desativar'}`, {});
  }

  // Competências
  competencias(): Observable<CompetenciaResumo[]> {
    return this.http.get<CompetenciaResumo[]>('/api/competencias');
  }
  competencia(ano: number, mes: number): Observable<Competencia> {
    return this.http.get<Competencia>(`/api/competencias/${ano}/${mes}`);
  }
  abrirCompetencia(ano: number, mes: number): Observable<Competencia> {
    return this.http.post<Competencia>(`/api/competencias/${ano}/${mes}/abrir`, {});
  }
  previaEmissao(competenciaId: number): Observable<PreviaEmissao> {
    return this.http.get<PreviaEmissao>(`/api/competencias/${competenciaId}/emissao/previa`);
  }
  solicitarEmissao(competenciaId: number, chaveIdempotencia: string, notaIds: number[], valorTotal: number): Observable<Lote> {
    return this.http.post<Lote>(`/api/competencias/${competenciaId}/emissao`, { chaveIdempotencia, notaIds, valorTotal });
  }
  criarAvulsa(competenciaId: number, dto: CriarAvulsa): Observable<NotaDetalhe> {
    return this.http.post<NotaDetalhe>(`/api/competencias/${competenciaId}/avulsas`, dto);
  }

  // Notas
  nota(id: number): Observable<NotaDetalhe> {
    return this.http.get<NotaDetalhe>(`/api/notas/${id}`);
  }
  atualizarNota(id: number, dto: AtualizarNota): Observable<NotaDetalhe> {
    return this.http.put<NotaDetalhe>(`/api/notas/${id}`, dto);
  }
  atualizarValor(linha: Pick<Linha, 'id' | 'versao'>, valor: number | null): Observable<LinhaResumo> {
    return this.http.put<LinhaResumo>(`/api/notas/${linha.id}/valor`, { versao: linha.versao, valor });
  }
  copiarValorAnterior(ids: number[], sobrescrever: boolean): Observable<ResultadoLote> {
    return this.http.post<ResultadoLote>('/api/notas/copiar-valor-anterior', { ids, sobrescrever });
  }
  marcarProntas(ids: number[]): Observable<ResultadoLote> {
    return this.http.post<ResultadoLote>('/api/notas/marcar-prontas', { ids });
  }
  voltarPendente(ids: number[]): Observable<ResultadoLote> {
    return this.http.post<ResultadoLote>('/api/notas/voltar-pendente', { ids });
  }
  ignorar(ids: number[], motivo: string | null): Observable<ResultadoLote> {
    return this.http.post<ResultadoLote>('/api/notas/ignorar', { ids, motivo });
  }
  restaurar(ids: number[]): Observable<ResultadoLote> {
    return this.http.post<ResultadoLote>('/api/notas/restaurar', { ids });
  }
  atualizarDoCadastro(id: number): Observable<NotaDetalhe> {
    return this.http.post<NotaDetalhe>(`/api/notas/${id}/atualizar-cadastro`, {});
  }
  consultar(id: number): Observable<void> {
    return this.http.post<void>(`/api/notas/${id}/consultar`, {});
  }
  excluirAvulsa(id: number): Observable<void> {
    return this.http.delete<void>(`/api/notas/${id}`);
  }
  dadosEnviados(notaId: number, tentativaId: number): Observable<string> {
    return this.http.get(`/api/notas/${notaId}/tentativas/${tentativaId}/dados`, { responseType: 'text' });
  }
  /** Download autenticado (cookie de sessão), sem expor o documento por URL pública. */
  baixar(url: string): Observable<Blob> {
    return this.http.get(url, { responseType: 'blob' });
  }
}
