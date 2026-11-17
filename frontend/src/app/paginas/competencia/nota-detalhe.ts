import { Component, computed, DestroyRef, inject, OnInit, signal } from '@angular/core';
import { FormBuilder, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatTabsModule } from '@angular/material/tabs';
import { Api } from '../../core/api';
import {
  brl,
  dataBr,
  dataHoraBr,
  documento,
  mensagemErro,
  mesAnoIso,
  mesCurto,
  opcoesMesReferencia,
  textoParaValor,
  valorParaTexto,
} from '../../core/formato';
import { CenarioSimulacao, Endereco, NotaDetalhe, Tentativa, temCampo } from '../../core/modelos';
import { CENARIOS, OPCOES_TRIBUTACAO, ROTULO_RESULTADO, ROTULO_STATUS_TRABALHO } from '../../core/rotulos';
import { CamposServico, CamposTomador, formServico, formTomador, limpar } from '../../compartilhado/formularios';
import { Downloads } from '../../compartilhado/download';
import { SituacaoBadge } from '../../compartilhado/situacao';

@Component({
  selector: 'app-nota-detalhe',
  imports: [
    ReactiveFormsModule,
    MatDialogModule,
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressBarModule,
    MatSelectModule,
    MatTabsModule,
    SituacaoBadge,
    CamposTomador,
    CamposServico,
  ],
  templateUrl: './nota-detalhe.html',
  styleUrl: './nota-detalhe.scss',
})
export class NotaDetalheDialog implements OnInit {
  protected readonly data = inject<{ id: number; editar: boolean }>(MAT_DIALOG_DATA);
  private readonly api = inject(Api);
  private readonly fb = inject(FormBuilder);
  private readonly snack = inject(MatSnackBar);
  private readonly downloads = inject(Downloads);

  protected readonly nota = signal<NotaDetalhe | null>(null);
  protected readonly erros = signal<string[]>([]);
  protected readonly salvando = signal(false);
  protected readonly aba = signal(0);
  protected readonly dadosEnviados = signal<Record<number, string>>({});
  protected form: FormGroup | null = null;

  protected readonly cenarios = CENARIOS;
  protected readonly rotuloResultado = ROTULO_RESULTADO;
  protected readonly rotuloStatus = ROTULO_STATUS_TRABALHO;
  protected readonly brl = brl;
  protected readonly doc = documento;
  protected readonly dataHora = dataHoraBr;
  protected readonly data_ = dataBr;
  protected readonly mesCurto = mesCurto;
  protected readonly mesAno = mesAnoIso;
  protected readonly opcoesMes = computed(() => {
    const n = this.nota();
    return n ? opcoesMesReferencia(n.ano, n.mes) : [];
  });
  protected readonly temCampo = temCampo;

  private timer: ReturnType<typeof setTimeout> | undefined;

  constructor() {
    inject(DestroyRef).onDestroy(() => clearTimeout(this.timer));
  }

  ngOnInit(): void {
    this.carregar();
  }

  private carregar(): void {
    this.api.nota(this.data.id).subscribe({
      next: (n) => {
        const mudouSituacao = this.nota()?.situacao !== n.situacao;
        this.nota.set(n);
        if (!this.form || mudouSituacao) this.montarForm(n);
        clearTimeout(this.timer);
        if (n.situacao === 'Processando' || n.situacao === 'ResultadoAConfirmar') this.timer = setTimeout(() => this.carregar(), 2500);
      },
      error: (e) => this.erros.set([mensagemErro(e).titulo]),
    });
  }

  private montarForm(n: NotaDetalhe): void {
    if (!n.podeEditar) {
      this.form = null;
      return;
    }
    this.form = this.fb.group({
      valor: [valorParaTexto(n.valor)],
      mesReferencia: [n.mesReferencia],
      descricao: [n.servico.descricao],
      informacoesComplementares: [n.servico.informacoesComplementares ?? ''],
      cenarioSimulacao: [n.cenarioSimulacao as CenarioSimulacao],
      identificacao: [n.identificacao],
      tomador: formTomador(this.fb, n.tomador),
      servico: formServico(this.fb, n.servico),
    });
  }

  protected ehAvulsa(): boolean {
    return this.nota()?.origem === 'Avulsa';
  }
  protected variavel(campo: string): boolean {
    return temCampo(this.nota()?.camposVariaveis, campo);
  }
  protected pendenteRevisao(campo: string): boolean {
    return temCampo(this.nota()?.camposPendentesRevisao, campo);
  }
  protected descricaoCenario(valor: CenarioSimulacao): string {
    return CENARIOS.find((c) => c.valor === valor)?.descricao ?? '';
  }
  protected rotuloTributacao(v: string): string {
    return OPCOES_TRIBUTACAO.find((o) => o.valor === v)?.rotulo ?? v;
  }
  protected endereco(e: Endereco | null | undefined): string {
    if (!e) return '—';
    const p = [
      [e.logradouro, e.numero, e.complemento].filter(Boolean).join(', '),
      e.bairro,
      [e.nomeMunicipio, e.uf].filter(Boolean).join('/'),
      e.codigoMunicipio ? `IBGE ${e.codigoMunicipio}` : null,
      e.cep ? `CEP ${e.cep}` : null,
    ].filter(Boolean);
    return p.length ? p.join(' · ') : '—';
  }
  protected classeResultado(t: Tentativa): string {
    switch (t.resultado) {
      case 'Autorizada':
      case 'Encontrada':
        return 'ok';
      case 'Rejeitada':
        return 'erro';
      case 'Incerta':
      case 'Interrompida':
      case 'DuplicidadeDetectada':
      case 'ConsultaIndisponivel':
      case 'EmAndamento':
        return 'alerta';
      default:
        return 'neutro';
    }
  }

  protected salvar(): void {
    const n = this.nota();
    if (!n || !this.form) return;
    const v = this.form.getRawValue();
    const valor = textoParaValor(v.valor);
    if (valor !== null && (Number.isNaN(valor) || valor <= 0)) {
      this.erros.set(['Valor inválido. Use o formato 1.234,56.']);
      return;
    }
    this.salvando.set(true);
    this.erros.set([]);
    const avulsa = n.origem === 'Avulsa';
    this.api
      .atualizarNota(n.id, {
        versao: n.versao,
        valor,
        mesReferencia: v.mesReferencia,
        descricao: !avulsa && this.variavel('Descricao') ? v.descricao : null,
        informacoesComplementares: !avulsa && this.variavel('InformacoesComplementares') ? v.informacoesComplementares ?? '' : null,
        cenarioSimulacao: v.cenarioSimulacao,
        identificacao: avulsa ? v.identificacao : null,
        tomador: avulsa ? (limpar(v.tomador) as never) : null,
        servico: avulsa ? ({ ...(limpar(v.servico) as object), issRetido: !!(v.servico as { issRetido?: boolean }).issRetido } as never) : null,
      })
      .subscribe({
        next: (nn) => {
          this.nota.set(nn);
          this.montarForm(nn);
          this.salvando.set(false);
          this.snack.open(
            nn.pendencias.length ? 'Salvo. Ainda há pendências antes de marcar como pronta.' : 'Salvo. A linha pode ser marcada como pronta.',
          );
        },
        error: (e) => {
          const m = mensagemErro(e);
          this.erros.set([m.titulo, ...m.erros]);
          this.salvando.set(false);
        },
      });
  }

  protected atualizarDoCadastro(): void {
    const n = this.nota();
    if (!n) return;
    if (!confirm('Substituir os dados desta linha (tomador, descrição e parâmetros fiscais) pelos dados atuais do cadastro da recorrência? O valor é mantido e os campos variáveis precisarão de nova revisão.'))
      return;
    this.api.atualizarDoCadastro(n.id).subscribe({
      next: (nn) => {
        this.nota.set(nn);
        this.montarForm(nn);
        this.snack.open('Dados atualizados a partir do cadastro.');
      },
      error: (e) => this.erros.set([mensagemErro(e).titulo]),
    });
  }

  protected consultar(): void {
    const n = this.nota();
    if (!n) return;
    this.api.consultar(n.id).subscribe({
      next: () => {
        this.snack.open('Consulta solicitada ao worker.');
        this.carregar();
      },
      error: (e) => this.erros.set([mensagemErro(e).titulo]),
    });
  }

  protected verDados(t: Tentativa): void {
    const n = this.nota();
    if (!n) return;
    if (this.dadosEnviados()[t.id]) {
      this.dadosEnviados.update((d) => ({ ...d, [t.id]: '' }));
      return;
    }
    this.api.dadosEnviados(n.id, t.id).subscribe((json) =>
      this.dadosEnviados.update((d) => ({ ...d, [t.id]: JSON.stringify(JSON.parse(json), null, 2) })),
    );
  }

  protected pdf(): void {
    const n = this.nota();
    if (n) this.downloads.baixar(`/api/notas/${n.id}/pdf`, `SIMULACAO-sem-validade-fiscal-${n.numeroNfse}.pdf`);
  }
  protected xml(): void {
    const n = this.nota();
    if (n) this.downloads.baixar(`/api/notas/${n.id}/xml`, `nfse-${n.chaveAcesso}.xml`);
  }
}
