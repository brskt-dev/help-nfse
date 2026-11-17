import { Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet],
  template: `
    <div class="aviso-simulacao" role="alert">SIMULAÇÃO — SEM VALIDADE FISCAL</div>
    <router-outlet />
  `,
})
export class App {}
