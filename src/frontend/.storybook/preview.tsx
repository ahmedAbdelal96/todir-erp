import type { Preview } from '@storybook/react';
import '../src/styles.css';
import React from 'react';

const preview: Preview = {
  parameters: {
    controls: {
      matchers: {
        color: /(background|color)$/i,
        date: /Date$/i,
      },
    },
  },
  globalTypes: {
    direction: {
      name: 'Direction',
      description: 'Document direction (RTL / LTR)',
      defaultValue: 'rtl',
      toolbar: {
        icon: 'globe',
        items: [
          { value: 'rtl', title: 'Arabic (RTL)' },
          { value: 'ltr', title: 'English (LTR)' },
        ],
        showName: true,
      },
    },
    theme: {
      name: 'Theme',
      description: 'Color theme',
      defaultValue: 'light',
      toolbar: {
        icon: 'circlehollow',
        items: [
          { value: 'light', title: 'Light' },
          { value: 'dark', title: 'Dark' },
        ],
        showName: true,
      },
    },
  },
  decorators: [
    (Story, context) => {
      const dir = context.globals.direction || 'rtl';
      const theme = context.globals.theme || 'light';
      return (
        <div dir={dir} data-theme={theme} style={{ padding: '1.5rem', minHeight: '100vh', fontFamily: 'var(--font-family-base)' }}>
          <Story />
        </div>
      );
    },
  ],
};

export default preview;
