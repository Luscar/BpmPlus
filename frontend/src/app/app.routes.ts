import { Routes } from '@angular/router';

export const routes: Routes = [
  {
    path: '',
    pathMatch: 'full',
    loadComponent: () =>
      import('./features/dashboard/dashboard.component').then(m => m.DashboardComponent),
  },
  {
    path: 'definitions',
    loadComponent: () =>
      import('./features/definitions/definitions-list/definitions-list.component').then(
        m => m.DefinitionsListComponent
      ),
  },
  {
    path: 'definitions/:cle',
    loadComponent: () =>
      import('./features/definitions/definition-detail/definition-detail.component').then(
        m => m.DefinitionDetailComponent
      ),
  },
  {
    path: 'instances/echues',
    loadComponent: () =>
      import('./features/instances/instances-echues/instances-echues.component').then(
        m => m.InstancesEchuesComponent
      ),
  },
  {
    path: 'instances',
    loadComponent: () =>
      import('./features/instances/instances-list/instances-list.component').then(
        m => m.InstancesListComponent
      ),
  },
  {
    path: 'instances/:id',
    loadComponent: () =>
      import('./features/instances/instance-detail/instance-detail.component').then(
        m => m.InstanceDetailComponent
      ),
  },
  { path: '**', redirectTo: '' },
];
