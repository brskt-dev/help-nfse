import { Component, DestroyRef, inject, OnInit, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { MatTooltipModule } from '@angular/material/tooltip';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { Api } from '../core/api';
import { Auth } from '../core/auth';
import { competenciaAtual, dataHoraBr } from '../core/formato';
import { Sistema } from '../core/modelos';
import { AlterarSenhaDialog } from './alterar-senha';

@Component({
  selector: 'app-shell',
  imports: [RouterOutlet, RouterLink, RouterLinkActive, MatButtonModule, MatIconModule, MatMenuModule, MatTooltipModule],
  template: `
    <header class="barra">
      <a class="marca" [routerLink]="linkAtual()">help-nfse</a>
      <nav class="links">
        <a mat-button [routerLink]="linkAtual()" routerLinkActive="ativo" [routerLinkActiveOptions]="{ exact: false }">
          <mat-icon>calendar_month</mat-icon> Mês atual
        </a>
        <a mat-button routerLink="/recorrencias" routerLinkActive="ativo"><mat-icon>repeat</mat-icon> Recorrências</a>
        <a mat-button routerLink="/historico" routerLinkActive="ativo"><mat-icon>history</mat-icon> Histórico</a>
        <a mat-button routerLink="/emissor" routerLinkActive="ativo"><mat-icon>store</mat-icon> Emissor</a>
      </nav>
      <span class="espaco"></span>
      @if (sistema(); as s) {
        @let w = s.workers[0];
        <span class="worker" [class.ok]="w?.ativo" [matTooltip]="w ? 'Último sinal: ' + dataHora(w.ultimoSinal) : 'O worker nunca se conectou.'">
          <mat-icon>{{ w?.ativo ? 'check_circle' : 'error' }}</mat-icon>
          {{ w?.ativo ? 'Worker ativo' : 'Worker sem sinal' }}
        </span>
      }
      <button mat-icon-button [matMenuTriggerFor]="menu" aria-label="Menu">
        <mat-icon>account_circle</mat-icon>
      </button>
      <mat-menu #menu="matMenu">
        <div class="menu-usuario suave pequeno">{{ auth.sessao()?.email }}</div>
        <a mat-menu-item class="so-movel" [routerLink]="linkAtual()"><mat-icon>calendar_month</mat-icon> Mês atual</a>
        <a mat-menu-item class="so-movel" routerLink="/recorrencias"><mat-icon>repeat</mat-icon> Recorrências</a>
        <a mat-menu-item class="so-movel" routerLink="/historico"><mat-icon>history</mat-icon> Histórico</a>
        <a mat-menu-item class="so-movel" routerLink="/emissor"><mat-icon>store</mat-icon> Emissor</a>
        <button mat-menu-item (click)="alterarSenha()"><mat-icon>key</mat-icon> Alterar senha</button>
        <button mat-menu-item (click)="sair()"><mat-icon>logout</mat-icon> Sair</button>
      </mat-menu>
    </header>
    <main>
      <router-outlet />
    </main>
  `,
  styles: `
    .barra {
      display: flex;
      align-items: center;
      gap: 4px;
      padding: 4px 12px;
      background: #fff;
      border-bottom: 1px solid var(--borda);
      position: sticky;
      top: 30px;
      z-index: 900;
    }
    .marca {
      font-weight: 700;
      font-size: 18px;
      color: #0b4f9c;
      text-decoration: none;
      margin-right: 12px;
    }
    .links a.ativo {
      background: #e3f0ff;
    }
    .espaco {
      flex: 1;
    }
    .worker {
      display: inline-flex;
      align-items: center;
      gap: 4px;
      font-size: 12px;
      color: #a1251b;
      mat-icon {
        font-size: 18px;
        height: 18px;
        width: 18px;
      }
      &.ok {
        color: #1b6b35;
      }
    }
    .menu-usuario {
      padding: 8px 16px;
    }
    .so-movel {
      display: none;
    }
    @media (max-width: 900px) {
      .links {
        display: none;
      }
      .so-movel {
        display: flex;
      }
    }
  `,
})
export class Shell implements OnInit {
  protected readonly auth = inject(Auth);
  private readonly api = inject(Api);
  private readonly router = inject(Router);
  private readonly dialog = inject(MatDialog);
  protected readonly sistema = signal<Sistema | null>(null);
  protected readonly dataHora = dataHoraBr;

  constructor() {
    const id = setInterval(() => this.carregarSistema(), 15000);
    inject(DestroyRef).onDestroy(() => clearInterval(id));
  }

  ngOnInit(): void {
    this.carregarSistema();
  }

  protected linkAtual(): unknown[] {
    const { ano, mes } = competenciaAtual();
    return ['/competencias', ano, mes];
  }

  private carregarSistema(): void {
    this.api.sistema().subscribe({ next: (s) => this.sistema.set(s), error: () => {} });
  }

  protected alterarSenha(): void {
    this.dialog.open(AlterarSenhaDialog, { width: '420px' });
  }

  protected async sair(): Promise<void> {
    await this.auth.sair();
    await this.router.navigateByUrl('/login');
  }
}
