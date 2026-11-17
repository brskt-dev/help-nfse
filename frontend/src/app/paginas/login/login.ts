import { Component, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { Router } from '@angular/router';
import { Auth } from '../../core/auth';
import { mensagemErro } from '../../core/formato';

@Component({
  selector: 'app-login',
  imports: [FormsModule, MatButtonModule, MatFormFieldModule, MatInputModule],
  template: `
    <div class="centro">
      <form class="cartao" (ngSubmit)="entrar()">
        <h1>help-nfse</h1>
        <p class="suave">Organização e emissão de NFS-e recorrentes.</p>
        <p class="sim">Ambiente de <strong>SIMULAÇÃO</strong>: nenhuma nota fiscal real é emitida.</p>
        <mat-form-field>
          <mat-label>E-mail</mat-label>
          <input matInput name="email" type="email" [(ngModel)]="email" required autocomplete="username" />
        </mat-form-field>
        <mat-form-field>
          <mat-label>Senha</mat-label>
          <input matInput name="senha" type="password" [(ngModel)]="senha" required autocomplete="current-password" />
        </mat-form-field>
        @if (erro()) {
          <div class="erro-texto" role="alert">{{ erro() }}</div>
        }
        <button mat-flat-button type="submit" [disabled]="entrando()">Entrar</button>
      </form>
    </div>
  `,
  styles: `
    .centro {
      min-height: calc(100vh - 40px);
      display: grid;
      place-items: center;
      padding: 16px;
    }
    form {
      width: min(380px, 100%);
      display: grid;
      gap: 12px;
    }
    h1 {
      margin: 0;
      color: #0b4f9c;
    }
    p {
      margin: 0;
    }
    .sim {
      background: #fde8e7;
      color: #a1251b;
      padding: 8px 10px;
      border-radius: 8px;
      font-size: 13px;
    }
  `,
})
export class LoginPagina implements OnInit {
  private readonly auth = inject(Auth);
  private readonly router = inject(Router);
  protected email = '';
  protected senha = '';
  protected readonly erro = signal<string | null>(null);
  protected readonly entrando = signal(false);

  ngOnInit(): void {
    this.auth.carregar().then(
      (s) => s.autenticado && this.router.navigateByUrl('/'),
      () => this.erro.set('Não foi possível contatar o servidor.'),
    );
  }

  protected async entrar(): Promise<void> {
    this.erro.set(null);
    this.entrando.set(true);
    try {
      await this.auth.entrar(this.email, this.senha);
      await this.router.navigateByUrl('/');
    } catch (e) {
      this.erro.set(mensagemErro(e).titulo);
    } finally {
      this.entrando.set(false);
    }
  }
}
