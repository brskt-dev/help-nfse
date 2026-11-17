import { Component, inject, input, OnInit, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSnackBar } from '@angular/material/snack-bar';
import { Router, RouterLink } from '@angular/router';
import { Api } from '../../core/api';
import { brl, mensagemErro, mesCurto } from '../../core/formato';
import { camposParaTexto, Recorrencia, temCampo } from '../../core/modelos';
import { CamposServico, CamposTomador, formServico, formTomador, limpar } from '../../compartilhado/formularios';
import { SituacaoBadge } from '../../compartilhado/situacao';

@Component({
  selector: 'app-recorrencia-form',
  imports: [
    ReactiveFormsModule,
    RouterLink,
    MatButtonModule,
    MatCheckboxModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    CamposTomador,
    CamposServico,
    SituacaoBadge,
  ],
  template: `
    <div class="conteudo">
      <div class="titulo-pagina">
        <a mat-icon-button routerLink="/recorrencias" aria-label="Voltar"><mat-icon>arrow_back</mat-icon></a>
        <h1>{{ id() ? 'Recorrência' : 'Nova recorrência' }}</h1>
        @if (rec(); as r) {
          <span class="situacao" [class.s-Emitida]="r.ativa" [class.s-Ignorada]="!r.ativa">{{ r.ativa ? 'Ativa' : 'Inativa' }}</span>
        }
        <span class="espaco"></span>
        @if (rec(); as r) {
          <button mat-stroked-button (click)="alternarAtiva(r)">
            <mat-icon>{{ r.ativa ? 'pause_circle' : 'play_circle' }}</mat-icon> {{ r.ativa ? 'Desativar' : 'Reativar' }}
          </button>
        }
      </div>

      <form [formGroup]="form" (ngSubmit)="salvar()">
        <fieldset class="secao">
          <legend>Identificação</legend>
          <div class="grade-form">
            <mat-form-field class="largo">
              <mat-label>Identificação interna</mat-label>
              <input matInput formControlName="identificacao" placeholder="Ex.: Clínica X — consultoria mensal" />
            </mat-form-field>
          </div>
        </fieldset>
        <fieldset class="secao">
          <legend>Tomador</legend>
          <app-campos-tomador [grupo]="$any(form.get('tomador'))" />
        </fieldset>
        <fieldset class="secao">
          <legend>Serviço e parâmetros fiscais</legend>
          <p class="pequeno suave">
            Campos marcados com <span class="selo-confirmar">confirmar</span> dependem de orientação do contador. O sistema não deduz regras fiscais.
          </p>
          <app-campos-servico [grupo]="$any(form.get('servico'))" [mostrarMarcadores]="true" />
        </fieldset>
        <fieldset class="secao">
          <legend>Campos variáveis por competência</legend>
          <p class="pequeno suave">
            O <strong>valor</strong> é sempre preenchido a cada competência. Marque também os campos que devem ser revisados e podem
            mudar mês a mês: a linha só poderá ser marcada como pronta depois da revisão.
          </p>
          <div class="checks" [formGroup]="variaveis">
            <mat-checkbox [checked]="true" [disabled]="true">Valor (sempre)</mat-checkbox>
            <mat-checkbox formControlName="Descricao">Descrição do serviço</mat-checkbox>
            <mat-checkbox formControlName="InformacoesComplementares">Informações complementares</mat-checkbox>
          </div>
        </fieldset>
        <fieldset class="secao">
          <legend>Observações internas</legend>
          <mat-form-field style="width: 100%">
            <textarea matInput formControlName="observacoes" rows="2"></textarea>
          </mat-form-field>
        </fieldset>
        @if (erros().length) {
          <div class="cartao erro-texto" role="alert">
            @for (e of erros(); track e) {
              <div>{{ e }}</div>
            }
          </div>
        }
        <div class="botoes">
          <p class="pequeno suave">Alterações aqui não modificam competências já abertas nem notas emitidas.</p>
          <span class="espaco"></span>
          <a mat-button routerLink="/recorrencias">Cancelar</a>
          <button mat-flat-button type="submit" [disabled]="salvando()">Salvar</button>
        </div>
      </form>

      @if (rec(); as r) {
        <fieldset class="secao">
          <legend>Histórico desta recorrência</legend>
          @for (h of r.historico; track h.notaId) {
            <a class="hist" [routerLink]="['/competencias', h.ano, h.mes]">
              <span>{{ mesCurto(h.ano, h.mes) }}</span>
              <span>{{ brl(h.valor) }}</span>
              <app-situacao [situacao]="h.situacao" />
              <span class="suave pequeno">{{ h.numeroNfse ?? '' }}</span>
            </a>
          } @empty {
            <p class="suave">Ainda não há linhas geradas para esta recorrência.</p>
          }
        </fieldset>
      }
    </div>
  `,
  styles: `
    .checks {
      display: flex;
      gap: 16px;
      flex-wrap: wrap;
    }
    .botoes {
      display: flex;
      align-items: center;
      gap: 8px;
      flex-wrap: wrap;
      margin-bottom: 24px;
      .espaco {
        flex: 1;
      }
    }
    .hist {
      display: grid;
      grid-template-columns: 90px 140px 180px 1fr;
      align-items: center;
      gap: 8px;
      padding: 6px 4px;
      border-bottom: 1px solid #eceff3;
      color: inherit;
      text-decoration: none;
      &:hover {
        background: #f7f9fc;
      }
    }
  `,
})
export class RecorrenciaFormPagina implements OnInit {
  readonly id = input<string>();
  private readonly api = inject(Api);
  private readonly fb = inject(FormBuilder);
  private readonly router = inject(Router);
  private readonly snack = inject(MatSnackBar);
  protected readonly rec = signal<Recorrencia | null>(null);
  protected readonly erros = signal<string[]>([]);
  protected readonly salvando = signal(false);
  protected readonly brl = brl;
  protected readonly mesCurto = mesCurto;

  protected readonly variaveis = this.fb.nonNullable.group({ Descricao: false, InformacoesComplementares: false });
  protected readonly form = this.fb.group({
    identificacao: ['', Validators.required],
    tomador: formTomador(this.fb),
    servico: formServico(this.fb),
    observacoes: [''],
  });

  ngOnInit(): void {
    const id = Number(this.id());
    if (id) {
      this.api.recorrencia(id).subscribe((r) => this.preencher(r));
    } else {
      this.api.emissor().subscribe((e) => {
        if (e.endereco.codigoMunicipio) this.form.get('servico.codigoMunicipioPrestacao')?.setValue(e.endereco.codigoMunicipio);
      });
    }
  }

  private preencher(r: Recorrencia): void {
    this.rec.set(r);
    this.form.setControl('tomador', formTomador(this.fb, r.tomador));
    this.form.setControl('servico', formServico(this.fb, r.servico));
    this.form.patchValue({ identificacao: r.identificacao, observacoes: r.observacoes ?? '' });
    this.variaveis.setValue({
      Descricao: temCampo(r.camposVariaveis, 'Descricao'),
      InformacoesComplementares: temCampo(r.camposVariaveis, 'InformacoesComplementares'),
    });
  }

  protected salvar(): void {
    const v = this.form.getRawValue();
    const vars = Object.entries(this.variaveis.getRawValue())
      .filter(([, marcado]) => marcado)
      .map(([k]) => k);
    const dto = {
      identificacao: v.identificacao ?? '',
      tomador: limpar(v.tomador) as never,
      servico: { ...(limpar(v.servico) as object), issRetido: !!(v.servico as { issRetido?: boolean }).issRetido } as never,
      camposVariaveis: camposParaTexto(vars),
      observacoes: v.observacoes || null,
      versao: this.rec()?.versao ?? null,
    };
    this.salvando.set(true);
    this.erros.set([]);
    const r = this.rec();
    (r ? this.api.atualizarRecorrencia(r.id, dto) : this.api.criarRecorrencia(dto)).subscribe({
      next: (salva) => {
        this.salvando.set(false);
        this.snack.open('Recorrência salva.');
        if (!r) void this.router.navigate(['/recorrencias', salva.id]);
        else this.preencher(salva);
      },
      error: (e) => {
        const m = mensagemErro(e);
        this.erros.set([m.titulo, ...m.erros]);
        this.salvando.set(false);
      },
    });
  }

  protected alternarAtiva(r: Recorrencia): void {
    const msg = r.ativa
      ? 'Desativar esta recorrência? Ela deixa de gerar linhas em novas competências. O histórico e as linhas já geradas são preservados.'
      : 'Reativar esta recorrência? Ela voltará a gerar linhas nas próximas aberturas de competência.';
    if (!confirm(msg)) return;
    this.api.definirAtiva(r.id, !r.ativa).subscribe({
      next: () => this.api.recorrencia(r.id).subscribe((x) => this.preencher(x)),
      error: (e) => this.erros.set([mensagemErro(e).titulo]),
    });
  }
}
