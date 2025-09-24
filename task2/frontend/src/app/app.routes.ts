import { Routes } from '@angular/router';
import { GameComponent } from './game/game.component';
import { MultiplayerComponent } from './multiplayer/multiplayer.component';

export const routes: Routes = [
    { path: '', redirectTo: '/home', pathMatch: 'full' },
    { path: 'home', loadComponent: () => import('./home/home.component').then(m => m.HomeComponent) },
    { path: 'game', component: GameComponent },
    { path: 'multiplayer', component: MultiplayerComponent },
];
