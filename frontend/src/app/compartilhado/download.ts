import { inject, Injectable } from '@angular/core';
import { MatSnackBar } from '@angular/material/snack-bar';
import { Api } from '../core/api';
import { mensagemErro } from '../core/formato';

/** Baixa documentos pela API autenticada e entrega ao navegador como arquivo. */
@Injectable({ providedIn: 'root' })
export class Downloads {
  private readonly api = inject(Api);
  private readonly snack = inject(MatSnackBar);

  baixar(url: string, nomeArquivo: string): void {
    this.api.baixar(url).subscribe({
      next: (blob) => {
        const link = document.createElement('a');
        link.href = URL.createObjectURL(blob);
        link.download = nomeArquivo;
        link.click();
        setTimeout(() => URL.revokeObjectURL(link.href), 10000);
      },
      error: async (e) => {
        // Erros de blob chegam como Blob; tenta ler a mensagem.
        let msg = mensagemErro(e).titulo;
        try {
          const corpo = JSON.parse(await (e.error as Blob).text());
          msg = corpo.title ?? msg;
        } catch {
          /* mantém mensagem padrão */
        }
        this.snack.open(msg, 'Fechar');
      },
    });
  }
}
