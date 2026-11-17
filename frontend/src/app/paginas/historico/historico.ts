import { Component, inject, OnInit, signal } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { RouterLink } from '@angular/router';
import { Api } from '../../core/api';
import { brl, rotuloCompetencia } from '../../core/formato';
import { CompetenciaResumo, SituacaoNota } from '../../core/modelos';
import { ROTULO_SITUACAO } from '../../core/rotulos';

@Component({
  selector: 'app-historico',
  imports: [RouterLink, MatIconModule, MatProgressBarModule],
  template: `
    <div class="conteudo">
      <div class="titulo-pagina">
        <h1>Histórico por mês de emissão</h1>
      </div>
      <p class="suave">
        Os meses abaixo são meses de emissão (quando as notas saíram). Abra um mês para consultar os dados usados em cada nota, as tentativas de emissão, as mensagens de rejeição e os
        documentos. Desativar uma recorrência não apaga o seu histórico.
      </p>
      @if (carregando()) {
        <mat-progress-bar mode="indeterminate" />
      }
      <div class="cartao lista">
        @for (c of lista(); track c.id) {
          <a class="item" [routerLink]="['/competencias', c.ano, c.mes]">
            <div class="mes">
              <strong>{{ rotulo(c.ano, c.mes) }}</strong>
              <span class="suave pequeno">{{ c.quantidade }} linha(s)</span>
            </div>
            <div class="chips">
              @for (r of c.resumo; track r.situacao) {
                <span class="situacao s-{{ r.situacao }}">{{ rotuloSituacao[r.situacao] }}: {{ r.quantidade }}</span>
              }
            </div>
            <div class="total">
              <span class="suave pequeno">Emitido</span>
              <strong>{{ brl(c.totalEmitido) }}</strong>
            </div>
            <mat-icon class="suave">chevron_right</mat-icon>
          </a>
        } @empty {
          @if (!carregando()) {
            <p class="suave vazio">Nenhum mês aberto ainda.</p>
          }
        }
      </div>
    </div>
  `,
  styles: `
    .lista {
      padding: 0;
    }
    .item {
      display: grid;
      grid-template-columns: 200px 1fr 160px 24px;
      align-items: center;
      gap: 12px;
      padding: 12px 14px;
      border-bottom: 1px solid #eceff3;
      color: inherit;
      text-decoration: none;
      &:hover {
        background: #f7f9fc;
      }
    }
    .mes {
      display: grid;
      strong {
        text-transform: capitalize;
      }
    }
    .chips {
      display: flex;
      gap: 6px;
      flex-wrap: wrap;
    }
    .total {
      display: grid;
      text-align: right;
    }
    .vazio {
      padding: 16px;
    }
    @media (max-width: 700px) {
      .item {
        grid-template-columns: 1fr auto;
      }
      .chips {
        grid-column: 1 / -1;
        grid-row: 2;
      }
      mat-icon {
        display: none;
      }
    }
  `,
})
export class HistoricoPagina implements OnInit {
  private readonly api = inject(Api);
  protected readonly lista = signal<CompetenciaResumo[]>([]);
  protected readonly carregando = signal(true);
  protected readonly brl = brl;
  protected readonly rotulo = rotuloCompetencia;
  protected readonly rotuloSituacao: Record<SituacaoNota, string> = ROTULO_SITUACAO;

  ngOnInit(): void {
    this.api.competencias().subscribe({
      next: (l) => {
        this.lista.set(l);
        this.carregando.set(false);
      },
      error: () => this.carregando.set(false),
    });
  }
}
