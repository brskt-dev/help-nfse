import { Component, computed, input } from '@angular/core';
import { SituacaoNota } from '../core/modelos';
import { ROTULO_SITUACAO } from '../core/rotulos';

@Component({
  selector: 'app-situacao',
  template: `<span class="situacao s-{{ situacao() }}">{{ rotulo() }}</span>`,
})
export class SituacaoBadge {
  readonly situacao = input.required<SituacaoNota>();
  protected readonly rotulo = computed(() => ROTULO_SITUACAO[this.situacao()]);
}
