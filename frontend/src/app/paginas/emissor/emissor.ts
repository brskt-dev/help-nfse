import { Component, inject, OnInit, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatSnackBar } from '@angular/material/snack-bar';
import { Api } from '../../core/api';
import { dataHoraBr, mensagemErro } from '../../core/formato';
import { Emissor, Sistema } from '../../core/modelos';
import { OPCOES_APURACAO, OPCOES_DATA_COMPETENCIA, OPCOES_REGIME_ESPECIAL, OPCOES_SIMPLES } from '../../core/rotulos';
import { formEndereco, limpar } from '../../compartilhado/formularios';

@Component({
  selector: 'app-emissor',
  imports: [ReactiveFormsModule, MatButtonModule, MatFormFieldModule, MatIconModule, MatInputModule, MatSelectModule],
  template: `
    <div class="conteudo">
      <div class="titulo-pagina">
        <h1>Emissor (prestador)</h1>
        @if (emissor()?.atualizadoEm) {
          <span class="suave pequeno">Atualizado em {{ dataHora(emissor()!.atualizadoEm) }}</span>
        }
      </div>

      @if (emissor()?.pendenciasConfirmacao?.length) {
        <div class="cartao pendencias">
          <strong><mat-icon>fact_check</mat-icon> Itens a confirmar com o contador antes da homologação</strong>
          <ul>
            @for (p of emissor()!.pendenciasConfirmacao; track p) {
              <li>{{ p }}</li>
            }
          </ul>
        </div>
      }

      <form [formGroup]="form" (ngSubmit)="salvar()">
        <fieldset class="secao">
          <legend>Dados cadastrais</legend>
          <div class="grade-form">
            <mat-form-field class="largo"><mat-label>Razão social</mat-label><input matInput formControlName="razaoSocial" /></mat-form-field>
            <mat-form-field><mat-label>Nome fantasia</mat-label><input matInput formControlName="nomeFantasia" /></mat-form-field>
            <mat-form-field><mat-label>CNPJ</mat-label><input matInput formControlName="cnpj" /></mat-form-field>
            <mat-form-field>
              <mat-label>Inscrição municipal <span class="selo-confirmar">confirmar</span></mat-label>
              <input matInput formControlName="inscricaoMunicipal" />
            </mat-form-field>
            <mat-form-field><mat-label>E-mail</mat-label><input matInput formControlName="email" type="email" /></mat-form-field>
            <mat-form-field><mat-label>Telefone</mat-label><input matInput formControlName="telefone" /></mat-form-field>
          </div>
        </fieldset>
        <fieldset class="secao" [formGroup]="$any(form.get('endereco'))">
          <legend>Endereço e município</legend>
          <div class="grade-form">
            <mat-form-field><mat-label>CEP</mat-label><input matInput formControlName="cep" /></mat-form-field>
            <mat-form-field class="largo-2"><mat-label>Logradouro</mat-label><input matInput formControlName="logradouro" /></mat-form-field>
            <mat-form-field><mat-label>Número</mat-label><input matInput formControlName="numero" /></mat-form-field>
            <mat-form-field><mat-label>Complemento</mat-label><input matInput formControlName="complemento" /></mat-form-field>
            <mat-form-field><mat-label>Bairro</mat-label><input matInput formControlName="bairro" /></mat-form-field>
            <mat-form-field><mat-label>Código IBGE do município</mat-label><input matInput formControlName="codigoMunicipio" maxlength="7" /></mat-form-field>
            <mat-form-field><mat-label>Município</mat-label><input matInput formControlName="nomeMunicipio" /></mat-form-field>
            <mat-form-field><mat-label>UF</mat-label><input matInput formControlName="uf" maxlength="2" /></mat-form-field>
          </div>
        </fieldset>
        <fieldset class="secao">
          <legend>Regime tributário <span class="selo-confirmar">confirmar com o contador</span></legend>
          <div class="grade-form">
            <mat-form-field>
              <mat-label>Opção pelo Simples Nacional</mat-label>
              <mat-select formControlName="opcaoSimplesNacional">
                @for (o of simples; track o.valor) {
                  <mat-option [value]="o.valor">{{ o.rotulo }}</mat-option>
                }
              </mat-select>
            </mat-form-field>
            <mat-form-field>
              <mat-label>Regime de apuração (ME/EPP)</mat-label>
              <mat-select formControlName="regimeApuracaoSimples">
                @for (o of apuracao; track o.valor) {
                  <mat-option [value]="o.valor">{{ o.rotulo }}</mat-option>
                }
              </mat-select>
            </mat-form-field>
            <mat-form-field>
              <mat-label>Regime especial de tributação</mat-label>
              <mat-select formControlName="regimeEspecialTributacao">
                @for (o of regimeEspecial; track o.valor) {
                  <mat-option [value]="o.valor">{{ o.rotulo }}</mat-option>
                }
              </mat-select>
            </mat-form-field>
          </div>
          <p class="pequeno suave">
            As opções acima organizam os dados; os códigos exatos enviados na DPS serão conferidos no leiaute oficial da NFS-e Nacional
            na etapa de homologação.
          </p>
        </fieldset>
        <fieldset class="secao">
          <legend>Parâmetros da integração</legend>
          <div class="grade-form">
            <mat-form-field>
              <mat-label>Ambiente</mat-label>
              <input matInput [value]="sistema()?.simulacao ? 'Simulação (sem validade fiscal)' : sistema()?.provedor ?? ''" disabled />
              <mat-hint>Nesta versão não há emissão real</mat-hint>
            </mat-form-field>
            <mat-form-field>
              <mat-label>Série da DPS <span class="selo-confirmar">confirmar</span></mat-label>
              <input matInput formControlName="serieDps" maxlength="5" />
            </mat-form-field>
            <mat-form-field>
              <mat-label>Próximo número da DPS</mat-label>
              <input matInput formControlName="proximoNumeroDps" type="number" min="1" />
              <mat-hint>Cada nota recebe um número uma única vez</mat-hint>
            </mat-form-field>
            <mat-form-field>
              <mat-label>Data de competência da DPS <span class="selo-confirmar">confirmar</span></mat-label>
              <mat-select formControlName="regraDataCompetencia">
                @for (o of regrasData; track o.valor) {
                  <mat-option [value]="o.valor">{{ o.rotulo }}</mat-option>
                }
              </mat-select>
            </mat-form-field>
          </div>
        </fieldset>
        <fieldset class="secao">
          <legend>Certificado digital A1</legend>
          <p>
            Situação:
            <strong>{{ sistema()?.certificadoConfigurado ? 'arquivo configurado no servidor' : 'não configurado' }}</strong>
            <span class="suave">(não utilizado na simulação)</span>
          </p>
          <p class="pequeno suave">
            O certificado nunca é enviado por esta interface. Na etapa de homologação ele será carregado pelo backend a partir de um
            secret do servidor (arquivo ou variável protegida), fora do repositório e dos logs.
          </p>
        </fieldset>

        @if (erros().length) {
          <div class="cartao erro-texto" role="alert">
            @for (e of erros(); track e) {
              <div>{{ e }}</div>
            }
          </div>
        }
        <div class="botoes">
          <button mat-flat-button type="submit" [disabled]="salvando()">Salvar emissor</button>
        </div>
      </form>
    </div>
  `,
  styles: `
    .pendencias {
      background: #fff4e0;
      border-color: #f5cf8f;
      margin-bottom: 16px;
      mat-icon {
        vertical-align: middle;
      }
      ul {
        margin: 6px 0 0;
      }
    }
    .largo-2 {
      grid-column: span 2;
    }
    .botoes {
      display: flex;
      justify-content: flex-end;
      margin-bottom: 24px;
    }
    @media (max-width: 600px) {
      .largo-2 {
        grid-column: auto;
      }
    }
  `,
})
export class EmissorPagina implements OnInit {
  private readonly api = inject(Api);
  private readonly fb = inject(FormBuilder);
  private readonly snack = inject(MatSnackBar);
  protected readonly emissor = signal<Emissor | null>(null);
  protected readonly sistema = signal<Sistema | null>(null);
  protected readonly erros = signal<string[]>([]);
  protected readonly salvando = signal(false);
  protected readonly dataHora = dataHoraBr;
  protected readonly simples = OPCOES_SIMPLES;
  protected readonly apuracao = OPCOES_APURACAO;
  protected readonly regimeEspecial = OPCOES_REGIME_ESPECIAL;
  protected readonly regrasData = OPCOES_DATA_COMPETENCIA;

  protected readonly form = this.fb.group({
    razaoSocial: ['', Validators.required],
    nomeFantasia: [''],
    cnpj: ['', Validators.required],
    inscricaoMunicipal: [''],
    email: ['', Validators.email],
    telefone: [''],
    endereco: formEndereco(this.fb),
    opcaoSimplesNacional: ['NaoInformado'],
    regimeApuracaoSimples: ['NaoInformado'],
    regimeEspecialTributacao: ['NaoInformado'],
    serieDps: ['1', Validators.required],
    proximoNumeroDps: [1, [Validators.required, Validators.min(1)]],
    regraDataCompetencia: ['NaoDefinida'],
  });

  ngOnInit(): void {
    this.api.sistema().subscribe((s) => this.sistema.set(s));
    this.api.emissor().subscribe((e) => this.preencher(e));
  }

  private preencher(e: Emissor): void {
    this.emissor.set(e);
    this.form.setControl('endereco', formEndereco(this.fb, e.endereco));
    this.form.patchValue({ ...e, endereco: undefined } as never);
  }

  protected salvar(): void {
    this.salvando.set(true);
    this.erros.set([]);
    const v = limpar(this.form.getRawValue());
    this.api.salvarEmissor({ ...(v as object), endereco: v.endereco ?? {}, proximoNumeroDps: Number(v.proximoNumeroDps) } as never).subscribe({
      next: (e) => {
        this.preencher(e);
        this.salvando.set(false);
        this.snack.open('Emissor salvo.');
      },
      error: (err) => {
        const m = mensagemErro(err);
        this.erros.set([m.titulo, ...m.erros]);
        this.salvando.set(false);
      },
    });
  }
}
