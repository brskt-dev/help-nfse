import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { RouterLink } from '@angular/router';
import { Api } from '../../core/api';
import { documento } from '../../core/formato';
import { RecorrenciaResumo, temCampo } from '../../core/modelos';

@Component({
  selector: 'app-recorrencias',
  imports: [FormsModule, RouterLink, MatButtonModule, MatButtonToggleModule, MatFormFieldModule, MatIconModule, MatInputModule, MatProgressBarModule],
  template: `
    <div class="conteudo">
      <div class="titulo-pagina">
        <h1>Recorrências</h1>
        <span class="suave">{{ ativas() }} ativa(s) de {{ lista().length }}</span>
        <span class="espaco"></span>
        <a mat-flat-button routerLink="/recorrencias/nova"><mat-icon>add</mat-icon> Nova recorrência</a>
      </div>
      <div class="filtros">
        <mat-form-field class="busca">
          <mat-icon matPrefix>search</mat-icon>
          <input matInput placeholder="Buscar" [ngModel]="busca()" (ngModelChange)="busca.set($event)" />
        </mat-form-field>
        <mat-button-toggle-group [value]="filtro()" (change)="filtro.set($event.value)">
          <mat-button-toggle value="ativas">Ativas</mat-button-toggle>
          <mat-button-toggle value="inativas">Inativas</mat-button-toggle>
          <mat-button-toggle value="todas">Todas</mat-button-toggle>
        </mat-button-toggle-group>
      </div>
      @if (carregando()) {
        <mat-progress-bar mode="indeterminate" />
      }
      <div class="cartao lista">
        @for (r of filtradas(); track r.id) {
          <a class="item" [routerLink]="['/recorrencias', r.id]" [class.inativa]="!r.ativa">
            <div class="principal">
              <strong>{{ r.identificacao }}</strong>
              <span class="suave pequeno">{{ r.tomadorNome }} · {{ doc(r.tomadorDocumento) }}</span>
              <span class="pequeno descricao">{{ r.descricao }}</span>
            </div>
            <div class="marcas">
              @if (!r.ativa) {
                <span class="situacao s-Ignorada">Inativa</span>
              }
              @if (temCampo(r.camposVariaveis, 'Descricao')) {
                <span class="tag">descrição variável</span>
              }
              @if (temCampo(r.camposVariaveis, 'InformacoesComplementares')) {
                <span class="tag">info. complementares variável</span>
              }
            </div>
            <mat-icon class="suave">chevron_right</mat-icon>
          </a>
        } @empty {
          <p class="suave vazio">Nenhuma recorrência encontrada.</p>
        }
      </div>
    </div>
  `,
  styles: `
    .filtros {
      display: flex;
      gap: 12px;
      align-items: center;
      flex-wrap: wrap;
      margin-bottom: 8px;
      .busca {
        width: 320px;
        max-width: 100%;
      }
    }
    .lista {
      padding: 0;
    }
    .item {
      display: flex;
      align-items: center;
      gap: 12px;
      padding: 10px 14px;
      border-bottom: 1px solid #eceff3;
      color: inherit;
      text-decoration: none;
      &:hover {
        background: #f7f9fc;
      }
      &.inativa {
        opacity: 0.65;
      }
    }
    .principal {
      flex: 1;
      display: grid;
      gap: 2px;
      min-width: 0;
    }
    .descricao {
      white-space: nowrap;
      overflow: hidden;
      text-overflow: ellipsis;
    }
    .marcas {
      display: flex;
      gap: 6px;
      flex-wrap: wrap;
      justify-content: flex-end;
    }
    .tag {
      border: 1px solid var(--borda);
      border-radius: 6px;
      padding: 0 6px;
      font-size: 11px;
    }
    .vazio {
      padding: 16px;
    }
  `,
})
export class RecorrenciasPagina implements OnInit {
  private readonly api = inject(Api);
  protected readonly lista = signal<RecorrenciaResumo[]>([]);
  protected readonly carregando = signal(true);
  protected readonly busca = signal('');
  protected readonly filtro = signal<'ativas' | 'inativas' | 'todas'>('ativas');
  protected readonly doc = documento;
  protected readonly temCampo = temCampo;
  protected readonly ativas = computed(() => this.lista().filter((r) => r.ativa).length);
  protected readonly filtradas = computed(() => {
    const b = this.busca().trim().toLowerCase();
    const f = this.filtro();
    return this.lista().filter(
      (r) =>
        (f === 'todas' || (f === 'ativas') === r.ativa) &&
        (!b || `${r.identificacao} ${r.tomadorNome} ${r.tomadorDocumento} ${r.descricao}`.toLowerCase().includes(b)),
    );
  });

  ngOnInit(): void {
    this.api.recorrencias(true).subscribe({
      next: (l) => {
        this.lista.set(l);
        this.carregando.set(false);
      },
      error: () => this.carregando.set(false),
    });
  }
}
