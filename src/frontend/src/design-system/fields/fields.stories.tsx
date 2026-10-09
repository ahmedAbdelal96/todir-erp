import React, { useState } from 'react';
import { MoneyField } from './money-field.tsx';
import { MoneyDisplay } from './money-display.tsx';
import { QuantityField } from './quantity-field.tsx';
import { QuantityDisplay } from './quantity-display.tsx';
import { LocalDateField } from './local-date-field.tsx';
import { LocalDateDisplay } from './local-date-display.tsx';
import type { MoneyValue, QuantityValue } from '../contracts/domain-values.ts';

export default {
  title: 'Fields/DomainFields',
};

export const MoneyFieldExample = () => {
  const [value, setValue] = useState<MoneyValue>({
    amount: '12500.50',
    currency: 'SAR',
  });

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: '1rem', maxWidth: '320px' }}>
      <MoneyField
        label="مبلغ الفاتورة"
        value={value}
        onChange={setValue}
      />
      <div>
        <strong>القيمة المنسقة (عرض فقط): </strong>
        <MoneyDisplay value={value} />
      </div>
    </div>
  );
};

export const QuantityFieldExample = () => {
  const [qty, setQty] = useState<QuantityValue>({
    value: '120.750',
    unitOfMeasure: 'KGM',
  });

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: '1rem', maxWidth: '320px' }}>
      <QuantityField
        label="الكمية المطلوبة"
        value={qty}
        onChange={setQty}
      />
      <div>
        <strong>العرض: </strong>
        <QuantityDisplay value={qty} />
      </div>
    </div>
  );
};

export const LocalDateFieldExample = () => {
  const [date, setDate] = useState<string>('2026-10-09');
  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: '1rem', maxWidth: '320px' }}>
      <LocalDateField
        label="تاريخ الاستحقاق"
        value={date}
        onChange={setDate}
      />
      <div>
        <strong>العرض: </strong>
        <LocalDateDisplay value={date} />
      </div>
    </div>
  );
};
