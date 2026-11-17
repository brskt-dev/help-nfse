import { inject } from '@angular/core';
import { Router, Routes } from '@angular/router';
import { autenticadoGuard } from './core/auth';
import { competenciaAtual } from './core/formato';

export const routes: Routes = [
  { path: 'login', loadComponent: () => import('./paginas/login/login').then((m) => m.LoginPagina), title: 'Entrar — help-nfse' },
  {
    path: '',
    canActivate: [autenticadoGuard],
    loadComponent: () => import('./paginas/shell').then((m) => m.Shell),
    children: [
      {
        path: '',
        pathMatch: 'full',
        redirectTo: () => {
          const { ano, mes } = competenciaAtual();
          return inject(Router).createUrlTree(['/competencias', ano, mes]);
        },
      },
      {
        path: 'competencias/:ano/:mes',
        loadComponent: () => import('./paginas/competencia/competencia').then((m) => m.CompetenciaPagina),
        title: 'Competência — help-nfse',
      },
      {
        path: 'recorrencias',
        loadComponent: () => import('./paginas/recorrencias/recorrencias').then((m) => m.RecorrenciasPagina),
        title: 'Recorrências — help-nfse',
      },
      {
        path: 'recorrencias/nova',
        loadComponent: () => import('./paginas/recorrencias/recorrencia-form').then((m) => m.RecorrenciaFormPagina),
        title: 'Nova recorrência — help-nfse',
      },
      {
        path: 'recorrencias/:id',
        loadComponent: () => import('./paginas/recorrencias/recorrencia-form').then((m) => m.RecorrenciaFormPagina),
        title: 'Recorrência — help-nfse',
      },
      {
        path: 'emissor',
        loadComponent: () => import('./paginas/emissor/emissor').then((m) => m.EmissorPagina),
        title: 'Emissor — help-nfse',
      },
      {
        path: 'historico',
        loadComponent: () => import('./paginas/historico/historico').then((m) => m.HistoricoPagina),
        title: 'Histórico — help-nfse',
      },
    ],
  },
  { path: '**', redirectTo: '' },
];
