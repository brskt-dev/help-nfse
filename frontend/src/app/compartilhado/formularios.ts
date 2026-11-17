import { Component, input } from '@angular/core';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatExpansionModule } from '@angular/material/expansion';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { DadosServico, DadosTomador, Endereco } from '../core/modelos';
import { OPCOES_TRIBUTACAO } from '../core/rotulos';

export function formEndereco(fb: FormBuilder, e?: Endereco | null): FormGroup {
  return fb.group({
    cep: [e?.cep ?? ''],
    logradouro: [e?.logradouro ?? ''],
    numero: [e?.numero ?? ''],
    complemento: [e?.complemento ?? ''],
    bairro: [e?.bairro ?? ''],
    codigoMunicipio: [e?.codigoMunicipio ?? '', Validators.pattern(/^\d{7}$/)],
    nomeMunicipio: [e?.nomeMunicipio ?? ''],
    uf: [e?.uf ?? '', Validators.maxLength(2)],
  });
}

export function formTomador(fb: FormBuilder, t?: DadosTomador | null): FormGroup {
  return fb.group({
    documento: [t?.documento ?? '', Validators.required],
    nome: [t?.nome ?? '', Validators.required],
    email: [t?.email ?? '', Validators.email],
    telefone: [t?.telefone ?? ''],
    inscricaoMunicipal: [t?.inscricaoMunicipal ?? ''],
    endereco: formEndereco(fb, t?.endereco),
  });
}

export function formServico(fb: FormBuilder, s?: DadosServico | null, municipioPadrao?: string | null): FormGroup {
  return fb.group({
    descricao: [s?.descricao ?? '', [Validators.required, Validators.maxLength(2000)]],
    codigoTributacaoNacional: [s?.codigoTributacaoNacional ?? '', Validators.pattern(/^\d{6}$/)],
    codigoTributacaoMunicipal: [s?.codigoTributacaoMunicipal ?? ''],
    codigoNbs: [s?.codigoNbs ?? ''],
    codigoMunicipioPrestacao: [s?.codigoMunicipioPrestacao ?? municipioPadrao ?? '', Validators.pattern(/^\d{7}$/)],
    tributacaoIssqn: [s?.tributacaoIssqn ?? 'NaoInformado'],
    aliquotaIss: [s?.aliquotaIss ?? null, [Validators.min(0), Validators.max(100)]],
    issRetido: [s?.issRetido ?? false],
    informacoesComplementares: [s?.informacoesComplementares ?? ''],
  });
}

@Component({
  selector: 'app-campos-tomador',
  imports: [ReactiveFormsModule, MatFormFieldModule, MatInputModule, MatExpansionModule],
  template: `
    <div class="grade-form" [formGroup]="grupo()">
      <mat-form-field>
        <mat-label>CPF ou CNPJ</mat-label>
        <input matInput formControlName="documento" placeholder="Somente números ou com pontuação" />
        <mat-hint>CNPJ alfanumérico aceito</mat-hint>
      </mat-form-field>
      <mat-form-field class="largo-2">
        <mat-label>Nome / razão social</mat-label>
        <input matInput formControlName="nome" />
      </mat-form-field>
      <mat-form-field>
        <mat-label>E-mail</mat-label>
        <input matInput formControlName="email" type="email" />
      </mat-form-field>
      <mat-form-field>
        <mat-label>Telefone</mat-label>
        <input matInput formControlName="telefone" />
      </mat-form-field>
      <mat-form-field>
        <mat-label>Inscrição municipal</mat-label>
        <input matInput formControlName="inscricaoMunicipal" />
      </mat-form-field>
    </div>
    <mat-expansion-panel class="painel-endereco">
      <mat-expansion-panel-header>
        <mat-panel-title>Endereço do tomador</mat-panel-title>
        <mat-panel-description>Opcional — exigência a confirmar com o contador</mat-panel-description>
      </mat-expansion-panel-header>
      <div class="grade-form" [formGroup]="$any(grupo().get('endereco'))">
        <mat-form-field><mat-label>CEP</mat-label><input matInput formControlName="cep" /></mat-form-field>
        <mat-form-field class="largo-2"><mat-label>Logradouro</mat-label><input matInput formControlName="logradouro" /></mat-form-field>
        <mat-form-field><mat-label>Número</mat-label><input matInput formControlName="numero" /></mat-form-field>
        <mat-form-field><mat-label>Complemento</mat-label><input matInput formControlName="complemento" /></mat-form-field>
        <mat-form-field><mat-label>Bairro</mat-label><input matInput formControlName="bairro" /></mat-form-field>
        <mat-form-field><mat-label>Código IBGE do município</mat-label><input matInput formControlName="codigoMunicipio" maxlength="7" /></mat-form-field>
        <mat-form-field><mat-label>Município</mat-label><input matInput formControlName="nomeMunicipio" /></mat-form-field>
        <mat-form-field><mat-label>UF</mat-label><input matInput formControlName="uf" maxlength="2" /></mat-form-field>
      </div>
    </mat-expansion-panel>
  `,
  styles: `
    .largo-2 {
      grid-column: span 2;
    }
    .painel-endereco {
      margin-top: 12px;
      box-shadow: none !important;
      border: 1px solid var(--borda);
    }
    @media (max-width: 600px) {
      .largo-2 {
        grid-column: auto;
      }
    }
  `,
})
export class CamposTomador {
  readonly grupo = input.required<FormGroup>();
}

@Component({
  selector: 'app-campos-servico',
  imports: [ReactiveFormsModule, MatFormFieldModule, MatInputModule, MatSelectModule, MatCheckboxModule],
  template: `
    <div class="grade-form" [formGroup]="grupo()">
      <mat-form-field class="largo">
        <mat-label>Descrição do serviço</mat-label>
        <textarea matInput formControlName="descricao" rows="3"></textarea>
        @if (mostrarMarcadores()) {
          <mat-hint>Marcadores: {{ '{competencia}' }} → 11/2026 · {{ '{mes_ano}' }} → novembro/2026 · {{ '{mes}' }} · {{ '{ano}' }}</mat-hint>
        }
      </mat-form-field>
      <mat-form-field>
        <mat-label>Cód. tributação nacional <span class="selo-confirmar">confirmar</span></mat-label>
        <input matInput formControlName="codigoTributacaoNacional" maxlength="6" placeholder="6 dígitos" />
      </mat-form-field>
      <mat-form-field>
        <mat-label>Cód. tributação municipal</mat-label>
        <input matInput formControlName="codigoTributacaoMunicipal" />
      </mat-form-field>
      <mat-form-field>
        <mat-label>Código NBS</mat-label>
        <input matInput formControlName="codigoNbs" />
      </mat-form-field>
      <mat-form-field>
        <mat-label>Município de prestação (IBGE) <span class="selo-confirmar">confirmar</span></mat-label>
        <input matInput formControlName="codigoMunicipioPrestacao" maxlength="7" placeholder="7 dígitos" />
      </mat-form-field>
      <mat-form-field>
        <mat-label>Tributação do ISSQN <span class="selo-confirmar">confirmar</span></mat-label>
        <mat-select formControlName="tributacaoIssqn">
          @for (o of tributacao; track o.valor) {
            <mat-option [value]="o.valor">{{ o.rotulo }}</mat-option>
          }
        </mat-select>
      </mat-form-field>
      <mat-form-field>
        <mat-label>Alíquota ISS (%) <span class="selo-confirmar">confirmar</span></mat-label>
        <input matInput formControlName="aliquotaIss" type="number" step="0.01" min="0" max="100" />
        <mat-hint>Informativa: o sistema não calcula tributos</mat-hint>
      </mat-form-field>
      <div class="check">
        <mat-checkbox formControlName="issRetido">ISS retido pelo tomador</mat-checkbox>
        <span class="selo-confirmar">confirmar</span>
      </div>
      <mat-form-field class="largo">
        <mat-label>Informações complementares</mat-label>
        <textarea matInput formControlName="informacoesComplementares" rows="2"></textarea>
      </mat-form-field>
    </div>
  `,
  styles: `
    .check {
      display: flex;
      align-items: center;
    }
  `,
})
export class CamposServico {
  readonly grupo = input.required<FormGroup>();
  readonly mostrarMarcadores = input(false);
  protected readonly tributacao = OPCOES_TRIBUTACAO;
}

/** Converte o valor do formulário para o DTO, transformando textos vazios em null. */
export function limpar<T>(v: T): T {
  if (v === '' || v === undefined) return null as T;
  if (Array.isArray(v)) return v.map(limpar) as T;
  if (v && typeof v === 'object') {
    return Object.fromEntries(Object.entries(v).map(([k, x]) => [k, limpar(x)])) as T;
  }
  return v;
}
