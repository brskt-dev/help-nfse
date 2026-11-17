import { HttpErrorResponse } from '@angular/common/http';

const moeda = new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' });
const numero = new Intl.NumberFormat('pt-BR', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
const dataHora = new Intl.DateTimeFormat('pt-BR', { dateStyle: 'short', timeStyle: 'medium', timeZone: 'America/Sao_Paulo' });
const meses = ['janeiro', 'fevereiro', 'março', 'abril', 'maio', 'junho', 'julho', 'agosto', 'setembro', 'outubro', 'novembro', 'dezembro'];

export function brl(v: number | null | undefined): string {
  return v == null ? '—' : moeda.format(v);
}

/** Valor para edição: "1.234,56". */
export function valorParaTexto(v: number | null | undefined): string {
  return v == null ? '' : numero.format(v);
}

/**
 * Interpreta um valor digitado em reais. Aceita "1.234,56", "1234,56", "R$ 1.234,56" e "1234.56".
 * Retorna null para vazio e NaN para texto inválido.
 */
export function textoParaValor(texto: string | null | undefined): number | null {
  let t = (texto ?? '').replace(/R\$|\s/g, '');
  if (!t) return null;
  if (t.includes(',')) {
    t = t.replace(/\./g, '').replace(',', '.');
  } else if (!/^\d+\.\d{1,2}$/.test(t)) {
    t = t.replace(/\./g, '');
  }
  if (!/^-?\d+(\.\d+)?$/.test(t)) return NaN;
  return Math.round(Number(t) * 100) / 100;
}

export function nomeMes(mes: number): string {
  return meses[mes - 1];
}

export function rotuloCompetencia(ano: number, mes: number): string {
  const n = nomeMes(mes);
  return `${n[0].toUpperCase()}${n.slice(1)} de ${ano}`;
}

export function mesCurto(ano: number, mes: number): string {
  return `${String(mes).padStart(2, '0')}/${ano}`;
}

/** "2026-10-01" → "outubro/2026". */
export function mesAnoIso(iso: string | null | undefined): string {
  if (!iso) return '—';
  const [a, m] = iso.split('-').map(Number);
  return `${nomeMes(m)}/${a}`;
}

/** Opções de mês de referência do serviço em torno do mês de emissão (12 meses antes até 1 depois). */
export function opcoesMesReferencia(anoEmissao: number, mesEmissao: number): { valor: string; rotulo: string }[] {
  const lista: { valor: string; rotulo: string }[] = [];
  for (let d = -12; d <= 1; d++) {
    const x = deslocarMes(anoEmissao, mesEmissao, d);
    const valor = `${x.ano}-${String(x.mes).padStart(2, '0')}-01`;
    const sufixo = d === 0 ? ' (mesmo mês da emissão)' : d === -1 ? ' (mês anterior)' : '';
    lista.push({ valor, rotulo: `${nomeMes(x.mes)}/${x.ano}${sufixo}` });
  }
  return lista.reverse();
}

export function dataHoraBr(iso: string | null | undefined): string {
  return iso ? dataHora.format(new Date(iso)) : '—';
}

export function dataBr(isoData: string | null | undefined): string {
  if (!isoData) return '—';
  const [a, m, d] = isoData.split('-');
  return `${d}/${m}/${a}`;
}

export function documento(doc: string | null | undefined): string {
  const d = (doc ?? '').toUpperCase();
  if (d.length === 11) return `${d.slice(0, 3)}.${d.slice(3, 6)}.${d.slice(6, 9)}-${d.slice(9)}`;
  if (d.length === 14) return `${d.slice(0, 2)}.${d.slice(2, 5)}.${d.slice(5, 8)}/${d.slice(8, 12)}-${d.slice(12)}`;
  return d;
}

/** Extrai a mensagem em português de um erro da API (ProblemDetails). */
export function mensagemErro(e: unknown): { titulo: string; erros: string[] } {
  if (e instanceof HttpErrorResponse) {
    const pd = e.error as { title?: string; detail?: string; erros?: string[] } | null;
    if (e.status === 0) return { titulo: 'Sem conexão com o servidor.', erros: [] };
    if (pd && typeof pd === 'object') return { titulo: pd.title ?? pd.detail ?? `Erro ${e.status}`, erros: pd.erros ?? [] };
    if (e.status === 429) return { titulo: 'Muitas tentativas. Aguarde um minuto.', erros: [] };
    return { titulo: `Erro ${e.status}`, erros: [] };
  }
  return { titulo: 'Erro inesperado.', erros: [] };
}

export function competenciaAtual(): { ano: number; mes: number } {
  const p = new Intl.DateTimeFormat('en-CA', { timeZone: 'America/Sao_Paulo', year: 'numeric', month: '2-digit' }).formatToParts(new Date());
  return { ano: Number(p.find((x) => x.type === 'year')!.value), mes: Number(p.find((x) => x.type === 'month')!.value) };
}

export function deslocarMes(ano: number, mes: number, delta: number): { ano: number; mes: number } {
  const total = ano * 12 + (mes - 1) + delta;
  return { ano: Math.floor(total / 12), mes: (total % 12) + 1 };
}
