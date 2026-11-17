import { HttpClient, HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject, Injectable, signal } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { catchError, firstValueFrom, throwError } from 'rxjs';
import { Sessao } from './modelos';

@Injectable({ providedIn: 'root' })
export class Auth {
  private readonly http = inject(HttpClient);
  readonly sessao = signal<Sessao | null>(null);

  /** Consulta a sessão e renova o token antiforgery (cookie XSRF-TOKEN). */
  async carregar(): Promise<Sessao> {
    const s = await firstValueFrom(this.http.get<Sessao>('/api/auth/sessao'));
    this.sessao.set(s);
    return s;
  }

  async entrar(email: string, senha: string): Promise<void> {
    if (!this.sessao()) await this.carregar();
    await firstValueFrom(this.http.post<void>('/api/auth/login', { email, senha }));
    // O token antiforgery é vinculado ao usuário: renovar após o login.
    await this.carregar();
  }

  async sair(): Promise<void> {
    try {
      await firstValueFrom(this.http.post<void>('/api/auth/logout', {}));
    } finally {
      await this.carregar().catch(() => this.sessao.set({ autenticado: false, email: null }));
    }
  }

  alterarSenha(senhaAtual: string, novaSenha: string) {
    return this.http.post<void>('/api/auth/senha', { senhaAtual, novaSenha });
  }
}

export const autenticadoGuard: CanActivateFn = async () => {
  const auth = inject(Auth);
  const router = inject(Router);
  try {
    const s = auth.sessao() ?? (await auth.carregar());
    return s.autenticado ? true : router.parseUrl('/login');
  } catch {
    return router.parseUrl('/login');
  }
};

/** Sessão expirada em qualquer chamada: volta para o login. */
export const sessaoInterceptor: HttpInterceptorFn = (req, next) => {
  const router = inject(Router);
  const auth = inject(Auth);
  return next(req).pipe(
    catchError((e: unknown) => {
      if (e instanceof HttpErrorResponse && e.status === 401 && !req.url.startsWith('/api/auth/')) {
        auth.sessao.set({ autenticado: false, email: null });
        void router.navigateByUrl('/login');
      }
      return throwError(() => e);
    }),
  );
};
