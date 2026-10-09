import React from 'react';
import { useForm } from 'react-hook-form';
import { FormField } from './form-field.tsx';
import { FormSection } from './form-section.tsx';
import { FormActions } from './form-actions.tsx';
import { TextField } from '../primitives/text-field.tsx';

export default {
  title: 'Forms/FormSystem',
};

interface SampleFormValues {
  name: string;
}

export const SampleForm = () => {
  const { control } = useForm<SampleFormValues>({
    defaultValues: { name: '' },
  });

  return (
    <div style={{ maxWidth: '480px' }}>
      <FormSection title="البيانات الأساسية" description="يرجى تعبئة الحقول المطلوبة لإنشاء الحساب">
        <FormField
          name="name"
          control={control}
          label="الاسم الكامل"
          isRequired
          render={({ field, fieldState }) => (
            <TextField
              label="الاسم التجاري"
              value={field.value}
              onChange={field.onChange}
              errorMessage={fieldState.error?.message}
              placeholder="أدخل الاسم التجاري..."
            />
          )}
        />
      </FormSection>
      <FormActions
        submitLabel="حفظ البيانات"
        cancelLabel="إلغاء"
        onCancel={() => console.log('Cancel')}
      />
    </div>
  );
};
