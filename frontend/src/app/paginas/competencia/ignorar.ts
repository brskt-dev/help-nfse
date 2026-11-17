import { Component, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';

@Component({
  selector: 'app-ignorar',
  imports: [FormsModule, MatDialogModule, MatButtonModule, MatFormFieldModule, MatInputModule],
  template: `
    <h2 mat-dialog-title>Ignorar neste mês</h2>
    <mat-dialog-content>
      <p>
        {{ data.nome ? data.nome : data.quantidade + ' linha(s)' }} não será(ão) emitida(s) nesta competência.
        A recorrência continua ativa para os próximos meses e a linha pode ser restaurada.
      </p>
      <mat-form-field style="width: 100%">
        <mat-label>Motivo (opcional)</mat-label>
        <input matInput [(ngModel)]="motivo" maxlength="300" />
      </mat-form-field>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button [mat-dialog-close]="undefined">Cancelar</button>
      <button mat-flat-button [mat-dialog-close]="motivo || null">Ignorar</button>
    </mat-dialog-actions>
  `,
})
export class IgnorarDialog {
  protected readonly data = inject<{ quantidade: number; nome?: string }>(MAT_DIALOG_DATA);
  protected motivo = '';
}
