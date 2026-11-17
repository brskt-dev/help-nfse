import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSnackBar } from '@angular/material/snack-bar';
import { Auth } from '../core/auth';
import { mensagemErro } from '../core/formato';

@Component({
  selector: 'app-alterar-senha',
  imports: [FormsModule, MatDialogModule, MatButtonModule, MatFormFieldModule, MatInputModule],
  template: `
    <h2 mat-dialog-title>Alterar senha</h2>
    <mat-dialog-content>
      <form id="form-senha" (ngSubmit)="salvar()" style="display: grid; gap: 12px; padding-top: 8px">
        <mat-form-field>
          <mat-label>Senha atual</mat-label>
          <input matInput type="password" name="atual" [(ngModel)]="atual" required autocomplete="current-password" />
        </mat-form-field>
        <mat-form-field>
          <mat-label>Nova senha (mínimo 10 caracteres)</mat-label>
          <input matInput type="password" name="nova" [(ngModel)]="nova" required minlength="10" autocomplete="new-password" />
        </mat-form-field>
        @for (e of erros(); track e) {
          <div class="erro-texto pequeno">{{ e }}</div>
        }
      </form>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button mat-dialog-close>Cancelar</button>
      <button mat-flat-button form="form-senha" type="submit" [disabled]="salvando()">Salvar</button>
    </mat-dialog-actions>
  `,
})
export class AlterarSenhaDialog {
  private readonly auth = inject(Auth);
  private readonly ref = inject(MatDialogRef<AlterarSenhaDialog>);
  private readonly snack = inject(MatSnackBar);
  protected atual = '';
  protected nova = '';
  protected readonly erros = signal<string[]>([]);
  protected readonly salvando = signal(false);

  protected salvar(): void {
    this.salvando.set(true);
    this.auth.alterarSenha(this.atual, this.nova).subscribe({
      next: () => {
        this.snack.open('Senha alterada.');
        this.ref.close();
      },
      error: (e) => {
        const m = mensagemErro(e);
        this.erros.set([m.titulo, ...m.erros]);
        this.salvando.set(false);
      },
    });
  }
}
