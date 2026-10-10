import React from 'react';
import { AppShell } from './app-shell.tsx';

export default {
  title: 'App/AppShell',
};

export const DefaultAppShell = () => (
  <div style={{ height: '600px', border: '1px solid #e2e8f0', borderRadius: '8px', overflow: 'hidden' }}>
    <AppShell />
  </div>
);
