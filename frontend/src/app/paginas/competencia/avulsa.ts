import { Component, inject, OnInit, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { Api } from '../../core/api';
import { deslocarMes, mensagemErro, opcoesMesReferencia, textoParaValor } from '../../core/formato';
import { CenarioSimulacao } from '../../core/modelos';
import { CENARIOS } from '../../core/rotulos';
import { CamposServico, CamposTomador, formServico, formTomador, limpar } from '../../compartilhado/formularios';

@Component({
  selector: 'app-avulsa',
  imports: [ReactiveFormsModule, MatDialogModule, MatButtonModule, MatFormFieldModule, MatInputModule, MatSelectModule, CamposTomador, CamposServico],
  template: `
    <h2 mat-dialog-title>Nota avulsa — emissão {{ data.rotulo }}</h2>
    <mat-dialog-content>
      <p class="suave pequeno">Nota sem recorrência, apenas neste mês de emissão. Nasce como Pendente.</p>
      <form [formGroup]="form" id="form-avulsa" (ngSubmit)="salvar()">
        <div class="grade-form">
          <mat-form-field class="largo">
            <mat-label>Identificação interna</mat-label>
            <input matInput formControlName="identificacao" />
          </mat-form-field>
        </div>
        <fieldset class="secao"><legend>Tomador</legend><app-campos-tomador [grupo]="$any(form.get('tomador'))" /></fieldset>
        <fieldset class="secao"><legend>Serviço</legend><app-campos-servico [grupo]="$any(form.get('servico'))" /></fieldset>
        <div class="grade-form">
          <mat-form-field>
            <mat-label>Valor (R$)</mat-label>
            <input matInput formControlName="valor" inputmode="decimal" placeholder="0,00" />
          </mat-form-field>
          <mat-form-field>
            <mat-label>Mês de referência do serviço</mat-label>
            <mat-select formControlName="mesReferencia">
              @for (o of opcoesMes; track o.valor) {
                <mat-option [value]="o.valor">{{ o.rotulo }}</mat-option>
              }
            </mat-select>
          </mat-form-field>
          <mat-form-field>
            <mat-label>Cenário de simulação</mat-label>
            <mat-select formControlName="cenarioSimulacao">
              @for (c of cenarios; track c.valor) {
                <mat-option [value]="c.valor">{{ c.rotulo }}</mat-option>
              }
            </mat-select>
          </mat-form-field>
        </div>
      </form>
      @for (e of erros(); track e) {
        <div class="erro-texto pequeno">{{ e }}</div>
      }
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button mat-dialog-close>Cancelar</button>
      <button mat-flat-button type="submit" form="form-avulsa" [disabled]="salvando()">Adicionar</button>
    </mat-dialog-actions>
  `,
})
export class AvulsaDialog implements OnInit {
  protected readonly data = inject<{ competenciaId: number; rotulo: string; ano: number; mes: number }>(MAT_DIALOG_DATA);
  protected readonly opcoesMes = opcoesMesReferencia(this.data.ano, this.data.mes);
  private readonly mesAnterior = deslocarMes(this.data.ano, this.data.mes, -1);
  private readonly ref = inject(MatDialogRef<AvulsaDialog>);
  private readonly api = inject(Api);
  private readonly fb = inject(FormBuilder);
  protected readonly cenarios = CENARIOS;
  protected readonly erros = signal<string[]>([]);
  protected readonly salvando = signal(false);
  protected readonly form = this.fb.group({
    identificacao: ['', Validators.required],
    tomador: formTomador(this.fb),
    servico: formServico(this.fb),
    valor: [''],
    cenarioSimulacao: ['Sucesso' as CenarioSimulacao],
    mesReferencia: [`${this.mesAnterior.ano}-${String(this.mesAnterior.mes).padStart(2, '0')}-01`],
  });

  ngOnInit(): void {
    // Sugere o município do emissor como local de prestação (pode ser alterado).
    this.api.emissor().subscribe((e) => {
      const c = this.form.get('servico.codigoMunicipioPrestacao');
      if (c && !c.value && e.endereco.codigoMunicipio) c.setValue(e.endereco.codigoMunicipio);
    });
  }

  protected salvar(): void {
    const v = this.form.getRawValue();
    const valor = textoParaValor(v.valor);
    if (valor !== null && Number.isNaN(valor)) {
      this.erros.set(['Valor inválido. Use o formato 1.234,56.']);
      return;
    }
    this.salvando.set(true);
    this.api
      .criarAvulsa(this.data.competenciaId, {
        identificacao: v.identificacao ?? '',
        tomador: limpar(v.tomador) as never,
        servico: { ...(limpar(v.servico) as object), issRetido: !!(v.servico as { issRetido?: boolean }).issRetido } as never,
        valor,
        cenarioSimulacao: v.cenarioSimulacao,
        mesReferencia: v.mesReferencia,
      })
      .subscribe({
        next: () => this.ref.close(true),
        error: (e) => {
          const m = mensagemErro(e);
          this.erros.set([m.titulo, ...m.erros]);
          this.salvando.set(false);
        },
      });
  }
}
