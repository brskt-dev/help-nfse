import { Component, inject } from '@angular/core';
import { MAT_DIALOG_DATA, MatDialogModule } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { ResultadoLote } from '../core/modelos';

export interface DadosResultadoLote {
  titulo: string;
  resultado: ResultadoLote;
}

@Component({
  selector: 'app-resultado-lote',
  imports: [MatDialogModule, MatButtonModule],
  template: `
    <h2 mat-dialog-title>{{ data.titulo }}</h2>
    <mat-dialog-content>
      <p>
        <strong>{{ data.resultado.sucessos }}</strong> concluída(s).
        <strong class="erro-texto">{{ data.resultado.falhas }}</strong> não puderam ser alteradas:
      </p>
      <ul class="lista">
        @for (i of falhas; track i.id) {
          <li>
            <strong>{{ i.identificacao || 'Nota ' + i.id }}</strong>
            <ul>
              @for (e of i.erros.length ? i.erros : [i.mensagem]; track $index) {
                <li class="erro-texto">{{ e }}</li>
              }
            </ul>
          </li>
        }
      </ul>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-flat-button mat-dialog-close>Entendi</button>
    </mat-dialog-actions>
  `,
  styles: `
    .lista {
      padding-left: 18px;
      li {
        margin-bottom: 6px;
      }
    }
  `,
})
export class ResultadoLoteDialog {
  protected readonly data = inject<DadosResultadoLote>(MAT_DIALOG_DATA);
  protected readonly falhas = this.data.resultado.itens.filter((i) => !i.sucesso);
}
