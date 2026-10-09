import React, { useState } from 'react';
import { Dialog } from './dialog.tsx';
import { Button } from './button.tsx';

export default {
  title: 'Primitives/Dialog',
};

export const DefaultDialog = () => {
  const [isOpen, setIsOpen] = useState(false);
  return (
    <div>
      <Button onPress={() => setIsOpen(true)}>فتح النافذة المنبثقة</Button>
      <Dialog
        isOpen={isOpen}
        onOpenChange={setIsOpen}
        title="تأكيد العملية"
        description="هل أنت متأكد من رغبتك في ترحيل هذا المستند؟"
      >
        <div style={{ display: 'flex', gap: '0.75rem', justifyContent: 'flex-end', marginTop: '1rem' }}>
          <Button variant="secondary" onPress={() => setIsOpen(false)}>إلغاء</Button>
          <Button variant="primary" onPress={() => setIsOpen(false)}>تأكيد الترحيل</Button>
        </div>
      </Dialog>
    </div>
  );
};
