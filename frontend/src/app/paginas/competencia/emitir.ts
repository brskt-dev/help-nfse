import { Component, inject, OnInit, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { Api } from '../../core/api';
import { brl, documento, mensagemErro } from '../../core/formato';
import { Lote, PreviaEmissao } from '../../core/modelos';

/**
 * Confirmação explícita: mostra quantidade, total e a lista exata que será enviada.
 * A chave de idempotência é gerada uma vez por abertura do diálogo; cliques repetidos não duplicam o pedido.
 */
@Component({
  selector: 'app-emitir',
  imports: [MatDialogModule, MatButtonModule, MatIconModule, MatProgressBarModule],
  template: `
    <h2 mat-dialog-title>Emitir notas prontas — emissão {{ data.rotulo }}</h2>
    <mat-dialog-content>
      @if (!previa()) {
        <mat-progress-bar mode="indeterminate" />
      }
      @if (previa(); as p) {
        @if (p.simulacao) {
          <div class="sim"><mat-icon>science</mat-icon> SIMULAÇÃO — SEM VALIDADE FISCAL. Nenhuma nota real será emitida.</div>
        }
        <div class="totais">
          <div><span class="suave">Quantidade</span><strong>{{ p.quantidade }}</strong></div>
          <div><span class="suave">Valor total</span><strong>{{ brl(p.valorTotal) }}</strong></div>
        </div>
        @if (p.bloqueios.length) {
          <div class="bloqueio">
            <strong>Não é possível emitir:</strong>
            <ul>
              @for (b of p.bloqueios; track b) {
                <li>{{ b }}</li>
              }
            </ul>
          </div>
        }
        <div class="lista">
          @for (i of p.itens; track i.id) {
            <div class="item">
              <span>{{ i.tomadorNome }} <span class="suave pequeno">{{ doc(i.tomadorDocumento) }}</span></span>
              <strong>{{ brl(i.valor) }}</strong>
            </div>
          }
        </div>
        <p class="suave pequeno">
          As notas serão processadas pelo worker no servidor, uma a uma. Você pode fechar o navegador.
          Notas emitidas ficam bloqueadas para edição.
        </p>
      }
      @if (erro().length) {
        <div class="erro-texto" role="alert">
          @for (e of erro(); track e) {
            <div>{{ e }}</div>
          }
        </div>
      }
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button (click)="ref.close()" [disabled]="enviando()">Cancelar</button>
      <button mat-flat-button (click)="confirmar()" [disabled]="!podeConfirmar()">
        <mat-icon>send</mat-icon>
        Confirmar emissão de {{ previa()?.quantidade ?? 0 }} nota(s) · {{ brl(previa()?.valorTotal ?? 0) }}
      </button>
    </mat-dialog-actions>
  `,
  styles: `
    .sim {
      display: flex;
      align-items: center;
      gap: 6px;
      background: #fde8e7;
      color: #a1251b;
      font-weight: 600;
      border-radius: 8px;
      padding: 8px 10px;
      margin-bottom: 12px;
    }
    .totais {
      display: grid;
      grid-template-columns: 1fr 1fr;
      gap: 12px;
      margin-bottom: 12px;
      div {
        display: grid;
        border: 1px solid var(--borda);
        border-radius: 10px;
        padding: 10px;
      }
      strong {
        font-size: 24px;
      }
    }
    .lista {
      max-height: 260px;
      overflow: auto;
      border: 1px solid var(--borda);
      border-radius: 8px;
    }
    .item {
      display: flex;
      justify-content: space-between;
      gap: 8px;
      padding: 6px 10px;
      border-bottom: 1px solid #eceff3;
    }
    .bloqueio {
      background: #fff4e0;
      border-radius: 8px;
      padding: 8px 12px;
      margin-bottom: 12px;
    }
  `,
})
export class EmitirDialog implements OnInit {
  protected readonly data = inject<{ competenciaId: number; rotulo: string }>(MAT_DIALOG_DATA);
  protected readonly ref = inject(MatDialogRef<EmitirDialog, Lote | undefined>);
  private readonly api = inject(Api);
  private readonly chave = crypto.randomUUID();
  protected readonly previa = signal<PreviaEmissao | null>(null);
  protected readonly enviando = signal(false);
  protected readonly erro = signal<string[]>([]);
  protected readonly brl = brl;
  protected readonly doc = documento;

  ngOnInit(): void {
    this.api.previaEmissao(this.data.competenciaId).subscribe({
      next: (p) => this.previa.set(p),
      error: (e) => this.erro.set([mensagemErro(e).titulo]),
    });
  }

  protected podeConfirmar(): boolean {
    const p = this.previa();
    return !!p && p.quantidade > 0 && p.bloqueios.length === 0 && !this.enviando();
  }

  protected confirmar(): void {
    const p = this.previa();
    if (!p || this.enviando()) return;
    this.enviando.set(true);
    this.erro.set([]);
    this.api.solicitarEmissao(this.data.competenciaId, this.chave, p.itens.map((i) => i.id), p.valorTotal).subscribe({
      next: (lote) => this.ref.close(lote),
      error: (e) => {
        const m = mensagemErro(e);
        this.erro.set([m.titulo, ...m.erros]);
        this.enviando.set(false);
        // A lista pode ter mudado: recarrega a prévia para nova confirmação.
        this.api.previaEmissao(this.data.competenciaId).subscribe((np) => this.previa.set(np));
      },
    });
  }
}
