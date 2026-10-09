import React from 'react';
import { Button } from './button.tsx';

export default {
  title: 'Primitives/Button',
  component: Button,
};

export const Primary = () => (
  <Button variant="primary" size="comfortable">
    حفظ التغييرات
  </Button>
);

export const Secondary = () => (
  <Button variant="secondary" size="comfortable">
    إلغاء
  </Button>
);

export const Danger = () => (
  <Button variant="danger" size="comfortable">
    حذف السجل
  </Button>
);

export const Ghost = () => (
  <Button variant="ghost" size="comfortable">
    تصدير البيانات
  </Button>
);

export const Disabled = () => (
  <Button variant="primary" isDisabled>
    غير متاح
  </Button>
);
