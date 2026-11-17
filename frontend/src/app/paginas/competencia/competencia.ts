import { Component, computed, DestroyRef, effect, ElementRef, inject, input, signal, untracked } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatMenuModule } from '@angular/material/menu';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { Router } from '@angular/router';
import { Observable } from 'rxjs';
import { Api } from '../../core/api';
import {
  brl,
  competenciaAtual,
  deslocarMes,
  documento,
  mensagemErro,
  mesAnoIso,
  mesCurto,
  nomeMes,
  rotuloCompetencia,
  textoParaValor,
  valorParaTexto,
} from '../../core/formato';
import { Competencia, CompetenciaResumo, Linha, ResultadoLote, SITUACOES, SituacaoNota, temCampo } from '../../core/modelos';
import { ROTULO_CENARIO, ROTULO_SITUACAO } from '../../core/rotulos';
import { Downloads } from '../../compartilhado/download';
import { ResultadoLoteDialog } from '../../compartilhado/resultado-lote';
import { SituacaoBadge } from '../../compartilhado/situacao';
import { AvulsaDialog } from './avulsa';
import { EmitirDialog } from './emitir';
import { IgnorarDialog } from './ignorar';
import { NotaDetalheDialog } from './nota-detalhe';

const EDITAVEIS: SituacaoNota[] = ['Pendente', 'Pronta', 'Erro'];
const EM_ANDAMENTO: SituacaoNota[] = ['Processando', 'ResultadoAConfirmar'];

@Component({
  selector: 'app-competencia',
  imports: [
    FormsModule,
    MatButtonModule,
    MatCheckboxModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatMenuModule,
    MatProgressBarModule,
    MatTooltipModule,
    SituacaoBadge,
  ],
  templateUrl: './competencia.html',
  styleUrl: './competencia.scss',
})
export class CompetenciaPagina {
  readonly ano = input.required<string>();
  readonly mes = input.required<string>();

  private readonly api = inject(Api);
  private readonly router = inject(Router);
  private readonly dialog = inject(MatDialog);
  private readonly snack = inject(MatSnackBar);
  private readonly downloads = inject(Downloads);
  private readonly el = inject<ElementRef<HTMLElement>>(ElementRef);

  protected readonly comp = signal<Competencia | null>(null);
  protected readonly carregando = signal(false);
  protected readonly ocupado = signal(false);
  protected readonly erro = signal<string | null>(null);
  protected readonly selecionados = signal<ReadonlySet<number>>(new Set());
  protected readonly filtro = signal<SituacaoNota | null>(null);
  protected readonly busca = signal('');
  protected readonly abertas = signal<CompetenciaResumo[]>([]);
  protected readonly anoSeletor = signal(new Date().getFullYear());

  protected readonly a = computed(() => Number(this.ano()));
  protected readonly m = computed(() => Number(this.mes()));
  protected readonly titulo = computed(() => rotuloCompetencia(this.a(), this.m()));
  protected readonly ehAtual = computed(() => {
    const c = competenciaAtual();
    return c.ano === this.a() && c.mes === this.m();
  });

  protected readonly resumo = computed(() => {
    const r = this.comp()?.resumo ?? [];
    return SITUACOES.map((s) => r.find((x) => x.situacao === s) ?? { situacao: s, quantidade: 0, total: 0 });
  });
  protected readonly total = computed(() => this.resumo().reduce((acc, r) => acc + (r.situacao === 'Ignorada' ? 0 : r.total), 0));
  protected readonly prontas = computed(() => this.resumo().find((r) => r.situacao === 'Pronta')!);

  protected readonly linhas = computed(() => {
    const f = this.filtro();
    const b = this.busca().trim().toLowerCase();
    return (this.comp()?.linhas ?? []).filter(
      (l) =>
        (!f || l.situacao === f) &&
        (!b ||
          l.tomadorNome.toLowerCase().includes(b) ||
          l.identificacao.toLowerCase().includes(b) ||
          l.tomadorDocumento.toLowerCase().includes(b.replace(/\D/g, '') || '§') ||
          l.descricao.toLowerCase().includes(b)),
    );
  });
  protected readonly selecaoVisivel = computed(() => this.linhas().filter((l) => this.selecionados().has(l.id)));
  protected readonly todasSelecionadas = computed(
    () => this.linhas().length > 0 && this.selecaoVisivel().length === this.linhas().length,
  );
  protected readonly emAndamento = computed(() => (this.comp()?.linhas ?? []).some((l) => EM_ANDAMENTO.includes(l.situacao)));

  // utilitários para o template
  protected readonly brl = brl;
  protected readonly doc = documento;
  protected readonly mesCurto = mesCurto;
  protected readonly mesAno = mesAnoIso;
  protected readonly nomeMes = nomeMes;
  protected readonly rotuloSituacao = ROTULO_SITUACAO;
  protected readonly rotuloCenario = ROTULO_CENARIO;
  protected readonly temCampo = temCampo;
  protected readonly meses = Array.from({ length: 12 }, (_, i) => i + 1);

  private timer: ReturnType<typeof setTimeout> | undefined;

  constructor() {
    effect(() => {
      const ano = this.a();
      const mes = this.m();
      untracked(() => {
        this.comp.set(null);
        this.selecionados.set(new Set());
        this.filtro.set(null);
        this.anoSeletor.set(ano);
        this.carregar(ano, mes);
      });
    });
    inject(DestroyRef).onDestroy(() => clearTimeout(this.timer));
    this.api.competencias().subscribe((c) => this.abertas.set(c));
  }

  protected carregar(ano = this.a(), mes = this.m(), silencioso = false): void {
    if (!silencioso) this.carregando.set(true);
    this.api.competencia(ano, mes).subscribe({
      next: (c) => {
        if (c.ano !== this.a() || c.mes !== this.m()) return;
        this.comp.set(c);
        const ids = new Set(c.linhas.map((l) => l.id));
        this.selecionados.update((s) => new Set([...s].filter((id) => ids.has(id))));
        this.erro.set(null);
        this.carregando.set(false);
        // Enquanto houver linhas em processamento, atualiza automaticamente.
        clearTimeout(this.timer);
        if (c.linhas.some((l) => EM_ANDAMENTO.includes(l.situacao)))
          this.timer = setTimeout(() => this.carregar(this.a(), this.m(), true), 2500);
      },
      error: (e) => {
        this.erro.set(mensagemErro(e).titulo);
        this.carregando.set(false);
        if (silencioso) this.timer = setTimeout(() => this.carregar(ano, mes, true), 5000);
      },
    });
  }

  // ---------- navegação ----------
  protected irPara(delta: number): void {
    const d = deslocarMes(this.a(), this.m(), delta);
    void this.router.navigate(['/competencias', d.ano, d.mes]);
  }
  protected irParaMes(ano: number, mes: number): void {
    void this.router.navigate(['/competencias', ano, mes]);
  }
  protected irParaAtual(): void {
    const c = competenciaAtual();
    this.irParaMes(c.ano, c.mes);
  }
  protected mesAberto(ano: number, mes: number): boolean {
    return this.abertas().some((c) => c.ano === ano && c.mes === mes);
  }

  // ---------- abertura ----------
  protected abrir(): void {
    this.ocupado.set(true);
    this.api.abrirCompetencia(this.a(), this.m()).subscribe({
      next: (c) => {
        const novas = c.linhas.length - (this.comp()?.linhas.length ?? 0);
        this.comp.set(c);
        this.ocupado.set(false);
        this.snack.open(novas > 0 ? `${novas} linha(s) gerada(s) a partir das recorrências ativas.` : 'Nenhuma linha nova.');
        this.api.competencias().subscribe((x) => this.abertas.set(x));
      },
      error: (e) => this.falha(e),
    });
  }

  // ---------- seleção ----------
  protected alternar(id: number, marcado: boolean): void {
    this.selecionados.update((s) => {
      const n = new Set(s);
      if (marcado) n.add(id);
      else n.delete(id);
      return n;
    });
  }
  protected alternarTodas(marcado: boolean): void {
    this.selecionados.set(marcado ? new Set(this.linhas().map((l) => l.id)) : new Set());
  }
  protected filtrar(s: SituacaoNota): void {
    this.filtro.update((f) => (f === s ? null : s));
  }

  // ---------- edição do valor ----------
  protected editavel(l: Linha): boolean {
    return EDITAVEIS.includes(l.situacao);
  }
  protected textoValor(l: Linha): string {
    return valorParaTexto(l.valor);
  }

  protected salvarValor(l: Linha, ev: Event): void {
    const input = ev.target as HTMLInputElement;
    const valor = textoParaValor(input.value);
    if (valor !== null && (Number.isNaN(valor) || valor <= 0)) {
      this.snack.open(`Valor inválido para ${l.tomadorNome}. Use o formato 1.234,56.`, 'Fechar');
      input.value = valorParaTexto(l.valor);
      return;
    }
    if (valor === l.valor) {
      input.value = valorParaTexto(l.valor);
      return;
    }
    this.api.atualizarValor(l, valor).subscribe({
      next: (r) => {
        input.value = valorParaTexto(r.valor);
        if (r.voltouParaPendente) this.snack.open(`${l.tomadorNome}: valor alterado; a linha voltou para Pendente para nova revisão.`);
        this.carregar(this.a(), this.m(), true);
      },
      error: (e) => {
        input.value = valorParaTexto(l.valor);
        this.falha(e);
      },
    });
  }

  /** Enter salva e passa para o próximo campo de valor. */
  protected proximo(ev: Event): void {
    ev.preventDefault();
    const campos = Array.from(this.el.nativeElement.querySelectorAll<HTMLInputElement>('input.valor'));
    const i = campos.indexOf(ev.target as HTMLInputElement);
    (ev.target as HTMLInputElement).blur();
    campos[i + 1]?.focus();
    campos[i + 1]?.select();
  }

  // ---------- ações ----------
  private executar(obs: Observable<ResultadoLote>, titulo: string, sucesso: (n: number) => string): void {
    this.ocupado.set(true);
    obs.subscribe({
      next: (r) => {
        this.ocupado.set(false);
        this.carregar(this.a(), this.m(), true);
        if (r.falhas > 0) this.dialog.open(ResultadoLoteDialog, { data: { titulo, resultado: r }, width: '560px' });
        else this.snack.open(sucesso(r.sucessos));
      },
      error: (e) => this.falha(e),
    });
  }

  private ids(l?: Linha): number[] {
    return l ? [l.id] : this.selecaoVisivel().map((x) => x.id);
  }

  protected copiarAnterior(l?: Linha): void {
    this.executar(
      this.api.copiarValorAnterior(this.ids(l), !!l),
      'Copiar valor anterior',
      (n) => `Valor anterior copiado em ${n} linha(s). Revise antes de marcar como pronta.`,
    );
  }
  protected marcarProntas(l?: Linha): void {
    this.executar(this.api.marcarProntas(this.ids(l)), 'Marcar como prontas', (n) => `${n} linha(s) pronta(s) para emissão.`);
  }
  protected voltarPendente(l?: Linha): void {
    this.executar(this.api.voltarPendente(this.ids(l)), 'Voltar para pendente', (n) => `${n} linha(s) voltaram para Pendente.`);
  }
  protected restaurar(l?: Linha): void {
    this.executar(this.api.restaurar(this.ids(l)), 'Restaurar', (n) => `${n} linha(s) restaurada(s).`);
  }
  protected ignorar(l?: Linha): void {
    const ids = this.ids(l);
    this.dialog
      .open(IgnorarDialog, { data: { quantidade: ids.length, nome: l?.tomadorNome }, width: '440px' })
      .afterClosed()
      .subscribe((motivo: string | null | undefined) => {
        if (motivo === undefined) return;
        this.executar(this.api.ignorar(ids, motivo), 'Ignorar neste mês', (n) => `${n} linha(s) ignorada(s) somente neste mês.`);
      });
  }
  protected consultar(l: Linha): void {
    this.api.consultar(l.id).subscribe({
      next: () => {
        this.snack.open('Consulta solicitada ao worker.');
        this.carregar(this.a(), this.m(), true);
      },
      error: (e) => this.falha(e),
    });
  }
  protected excluir(l: Linha): void {
    if (!confirm(`Excluir a nota avulsa "${l.identificacao}"? Esta ação não pode ser desfeita.`)) return;
    this.api.excluirAvulsa(l.id).subscribe({
      next: () => {
        this.snack.open('Nota avulsa excluída.');
        this.carregar();
      },
      error: (e) => this.falha(e),
    });
  }
  protected pdf(l: Linha): void {
    this.downloads.baixar(`/api/notas/${l.id}/pdf`, `SIMULACAO-sem-validade-fiscal-${l.numeroNfse}.pdf`);
  }

  protected detalhes(l: Linha, editar = false): void {
    this.dialog
      .open(NotaDetalheDialog, {
        data: { id: l.id, editar },
        width: '920px',
        maxWidth: '100vw',
        maxHeight: '100vh',
        panelClass: 'dialogo-detalhe',
        autoFocus: false,
      })
      .afterClosed()
      .subscribe(() => this.carregar(this.a(), this.m(), true));
  }

  protected avulsa(): void {
    const c = this.comp();
    if (!c?.id) return;
    this.dialog
      .open(AvulsaDialog, { data: { competenciaId: c.id, rotulo: this.titulo(), ano: c.ano, mes: c.mes }, width: '760px', maxWidth: '100vw', autoFocus: false })
      .afterClosed()
      .subscribe((criada) => criada && this.carregar());
  }

  protected emitir(): void {
    const c = this.comp();
    if (!c?.id) return;
    this.dialog
      .open(EmitirDialog, { data: { competenciaId: c.id, rotulo: this.titulo() }, width: '640px', maxWidth: '100vw', disableClose: true })
      .afterClosed()
      .subscribe((lote) => {
        if (lote) {
          this.snack.open(
            lote.repetido
              ? 'Este pedido de emissão já havia sido registrado.'
              : `${lote.quantidade} nota(s) enviada(s) para processamento. Você pode fechar o navegador; o worker continua.`,
            'OK',
            { duration: 8000 },
          );
          this.selecionados.set(new Set());
        }
        this.carregar(this.a(), this.m(), true);
      });
  }

  private falha(e: unknown): void {
    this.ocupado.set(false);
    const m = mensagemErro(e);
    this.snack.open([m.titulo, ...m.erros].join(' '), 'Fechar', { duration: 8000 });
    this.carregar(this.a(), this.m(), true);
  }
}
