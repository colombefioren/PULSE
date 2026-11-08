/**
 * The signed-in person and workspace shown in the shell.
 * Until accounts ship (M1) this is the demo workspace; the shell reads it through this module only,
 * so replacing it with the real session touches one file.
 */
export interface Session {
  user: { name: string; initials: string }
  org: { name: string; role: 'Owner' | 'Admin' | 'Viewer' }
}

export const demoSession: Session = {
  user: { name: 'Colombe', initials: 'C' },
  org: { name: 'Pulse Demo', role: 'Owner' },
}
