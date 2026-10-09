import React from 'react';
import { ListPage } from './list-page.tsx';
import { RecordPage } from './record-page.tsx';
import { DocumentPage } from './document-page.tsx';
import { Button } from '../primitives/button.tsx';

export default {
  title: 'Pages/Archetypes',
};

export const ListPageExample = () => (
  <ListPage
    title="دليل الأطراف التجارية"
    subtitle="إدارة العملاء والموردين والشركاء التجاريين"
    primaryAction={{
      label: 'إضافة طرف جديد',
      onClick: () => console.log('Add new'),
    }}
  >
    <div style={{ padding: '2rem', background: '#f8fafc', border: '1px dashed #cbd5e1', borderRadius: '8px', textAlign: 'center' }}>
      محتوى جدول البيانات والفلترة وقائمة السجلات
    </div>
  </ListPage>
);

export const RecordPageExample = () => (
  <RecordPage
    title="شركة الأفق للتجارة"
    recordCode="CUST-001"
    status="ACTIVE"
    headerActions={
      <>
        <Button variant="secondary">تعديل</Button>
        <Button variant="primary">إنشاء فاتورة</Button>
      </>
    }
  >
    <div style={{ padding: '2rem', background: '#f8fafc', border: '1px dashed #cbd5e1', borderRadius: '8px' }}>
      بيانات الطرف، العناوين، جهات الاتصال، والمرفقات
    </div>
  </RecordPage>
);

export const DocumentPageExample = () => (
  <DocumentPage
    documentNumber="INV-2026-0042"
    documentType="فاتورة مبيعات ضريبية"
    statuses={{
      lifecycle: 'ACTIVE',
      approval: 'ACTIVE',
      accounting: 'ACTIVE',
      compliance: 'ACTIVE',
      settlement: 'DRAFT',
      isLocked: true,
    }}
    headerActions={
      <>
        <Button variant="secondary">طباعة PDF</Button>
        <Button variant="secondary">إرسال بريد</Button>
      </>
    }
    metadataSection={
      <div style={{ padding: '1rem', background: '#fff', border: '1px solid #e2e8f0', borderRadius: '6px' }}>
        بيانات العميل، تاريخ الإصدار، وشروط الدفع
      </div>
    }
    linesSection={
      <div style={{ padding: '1rem', background: '#fff', border: '1px solid #e2e8f0', borderRadius: '6px' }}>
        جدول بنود الفاتورة والكميات والأسعار والضرائب
      </div>
    }
    summaryItems={[
      { label: 'المجموع قبل الضريبة', value: '10,000.00 SAR' },
      { label: 'ضريبة القيمة المضافة (15%)', value: '1,500.00 SAR' },
      { label: 'المجموع الإجمالي', value: '11,500.00 SAR', isEmphasized: true },
    ]}
  />
);

export const WizardPageExample = () => (
  <div style={{ padding: '1rem' }}>
    <div style={{ padding: '1.5rem', background: '#fff', border: '1px solid #e2e8f0', borderRadius: '8px' }}>
      <h3>معالج العمليات متعددة الخطوات (WizardPage)</h3>
      <p style={{ color: '#64748b' }}>إدارة تدفقات استيراد البيانات والإعدادات المرحلية</p>
    </div>
  </div>
);
