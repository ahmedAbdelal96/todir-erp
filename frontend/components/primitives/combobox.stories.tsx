import React, { useState } from 'react';
import { ComboBox } from './combobox.tsx';

export default {
  title: 'Primitives/ComboBox',
};

const currencies = [
  { id: 'EGP', name: 'جنيه مصري (EGP)' },
  { id: 'SAR', name: 'ريال سعودي (SAR)' },
  { id: 'AED', name: 'درهم إماراتي (AED)' },
  { id: 'USD', name: 'دولار أمريكي (USD)' },
];

export const DefaultComboBox = () => {
  const [selectedKey, setSelectedKey] = useState<string | number | null>('SAR');
  return (
    <div style={{ maxWidth: '320px' }}>
      <ComboBox
        label="العملة الأساسية"
        items={currencies}
        selectedKey={selectedKey}
        onSelectionChange={setSelectedKey}
        placeholder="ابحث عن العملة..."
      >
        {(item: { id: string; name: string }) => <div key={item.id}>{item.name}</div>}
      </ComboBox>
    </div>
  );
};
