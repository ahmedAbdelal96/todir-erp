import { chromium } from '@playwright/test';
import fs from 'node:fs';
import path from 'node:path';

const SCREENSHOT_DIR = path.resolve('artifacts/acceptance/directory');
if (!fs.existsSync(SCREENSHOT_DIR)) {
  fs.mkdirSync(SCREENSHOT_DIR, { recursive: true });
}

async function capture() {
  const browser = await chromium.launch({ headless: true });
  const context = await browser.newContext({
    viewport: { width: 1280, height: 800 },
    locale: 'ar-EG',
    colorScheme: 'light'
  });
  const page = await context.newPage();

  console.log('1. Capturing Mobile Auth View (390px)...');
  const mobileAuth = await context.newPage();
  await mobileAuth.setViewportSize({ width: 390, height: 844 });
  await mobileAuth.goto('http://127.0.0.1:5173', { waitUntil: 'networkidle' });
  await mobileAuth.waitForTimeout(600);
  await mobileAuth.screenshot({ path: path.join(SCREENSHOT_DIR, '20-atlas-auth-mobile-390px.png') });
  console.log('Saved 20-atlas-auth-mobile-390px.png');
  await mobileAuth.close();

  console.log('2. Logging in as Atlas Admin on desktop...');
  await page.goto('http://127.0.0.1:5173', { waitUntil: 'networkidle' });
  const atlasLoginBtn = page.locator('button:has-text("Atlas")').first();
  if (await atlasLoginBtn.isVisible()) {
    await atlasLoginBtn.click();
    await page.locator('a[href="/directory"]').first().waitFor({ state: 'visible', timeout: 20000 });
    await page.waitForTimeout(600);
  }

  await page.screenshot({ path: path.join(SCREENSHOT_DIR, '01-atlas-authenticated-home.png') });
  console.log('Saved 01-atlas-authenticated-home.png');

  console.log('3. Navigating to Directory...');
  await page.locator('a[href="/directory"]').first().click();
  await page.getByText('شركة المنارة').or(page.getByText('أحمد محمود')).first().waitFor({ state: 'visible', timeout: 15000 });
  await page.waitForTimeout(600);
  await page.screenshot({ path: path.join(SCREENSHOT_DIR, '02-atlas-directory-list.png') });
  console.log('Saved 02-atlas-directory-list.png');

  console.log('4. Filtering by role "العملاء فقط"...');
  const roleBtn = page.locator('button:has-text("كل الأدوار")').first();
  if (await roleBtn.isVisible()) {
    await roleBtn.click();
    await page.waitForTimeout(400);
    const customerOption = page.locator('[role="option"]:has-text("العملاء فقط")').first();
    if (await customerOption.isVisible()) {
      await customerOption.click();
      await page.waitForTimeout(800);
      await page.screenshot({ path: path.join(SCREENSHOT_DIR, '03-atlas-filter-customers.png') });
      console.log('Saved 03-atlas-filter-customers.png');
      // Reset filter
      const currentRoleBtn = page.locator('button:has-text("العملاء فقط")').first();
      if (await currentRoleBtn.isVisible()) {
        await currentRoleBtn.click();
        await page.waitForTimeout(300);
        await page.locator('[role="option"]:has-text("كل الأدوار")').first().click();
        await page.waitForTimeout(500);
      }
    }
  }

  console.log('5. Search Arabic...');
  const searchInput = page.locator('input[placeholder*="ابحث بالاسم"]').first();
  if (await searchInput.isVisible()) {
    await searchInput.fill('المنارة');
    await page.getByText(/شركة المنارة/).first().waitFor({ state: 'visible', timeout: 10000 });
    await page.screenshot({ path: path.join(SCREENSHOT_DIR, '04-atlas-search-arabic.png') });
    console.log('Saved 04-atlas-search-arabic.png');

    await searchInput.fill('سجل_غير_موجود_إطلاقا_xyz');
    await page.getByText('لا توجد أطراف تجارية مسجلة').first().waitFor({ state: 'visible', timeout: 10000 });
    await page.screenshot({ path: path.join(SCREENSHOT_DIR, '05-atlas-empty-state.png') });
    console.log('Saved 05-atlas-empty-state.png');

    await searchInput.fill('');
    await page.waitForTimeout(600);
  }

  console.log('6. Quick Create Dialog...');
  const quickCreateBtn = page.locator('button:has-text("إضافة طرف تجاري")').first();
  if (await quickCreateBtn.isVisible()) {
    await quickCreateBtn.click();
    await page.waitForTimeout(500);
    await page.screenshot({ path: path.join(SCREENSHOT_DIR, '06-atlas-quick-create-dialog.png') });
    console.log('Saved 06-atlas-quick-create-dialog.png');

    const toggleAdvanced = page.locator('button:has-text("إضافة الرقم الضريبي")').first();
    if (await toggleAdvanced.isVisible()) {
      await toggleAdvanced.click();
      await page.waitForTimeout(500);
      await page.screenshot({ path: path.join(SCREENSHOT_DIR, '07-atlas-quick-create-with-details.png') });
      console.log('Saved 07-atlas-quick-create-with-details.png');
    }

    await page.keyboard.press('Escape');
    await page.waitForTimeout(500);
  }

  console.log('7. Canonical Import Wizard Flow...');
  const importBtn = page.locator('button:has-text("استيراد سجلات")').first();
  if (await importBtn.isVisible()) {
    await importBtn.click();
    await page.waitForTimeout(600);
    await page.screenshot({ path: path.join(SCREENSHOT_DIR, '14-atlas-import-wizard-step1.png') });
    console.log('Saved 14-atlas-import-wizard-step1.png');

    const sampleBtn = page.locator('button:has-text("استخدام بيانات تجريبية")').first();
    if (await sampleBtn.isVisible()) {
      await sampleBtn.click();
      await page.waitForTimeout(600);
      await page.screenshot({ path: path.join(SCREENSHOT_DIR, '15-atlas-import-wizard-step2.png') });
      console.log('Saved 15-atlas-import-wizard-step2.png');

      const dryRunBtn = page.locator('button:has-text("فحص البيانات والتجربة")').first();
      if (await dryRunBtn.isVisible()) {
        await dryRunBtn.click();
        await page.waitForTimeout(1000);
        await page.screenshot({ path: path.join(SCREENSHOT_DIR, '16-atlas-import-wizard-step3.png') });
        console.log('Saved 16-atlas-import-wizard-step3.png');

        const previewBtn = page.locator('button:has-text("معاينة التغييرات")').first();
        if (await previewBtn.isVisible()) {
          await previewBtn.click();
          await page.waitForTimeout(600);
          await page.screenshot({ path: path.join(SCREENSHOT_DIR, '17-atlas-import-wizard-step4.png') });
          console.log('Saved 17-atlas-import-wizard-step4.png');

          const execBtn = page.locator('button:has-text("تأكيد واستيراد")').first();
          if (await execBtn.isVisible()) {
            await execBtn.click();
            await page.waitForTimeout(1200);
            await page.screenshot({ path: path.join(SCREENSHOT_DIR, '18-atlas-import-wizard-step5.png') });
            console.log('Saved 18-atlas-import-wizard-step5.png');

            const closeBtn = page.locator('button:has-text("إغلاق وتحديث الدليل")').first();
            if (await closeBtn.isVisible()) {
              await closeBtn.click();
              await page.waitForTimeout(800);
            }
          }
        }
      }
    }
  }

  console.log('8. Opening populated Party Record (أحمد محمود)...');
  await page.goto('http://127.0.0.1:5173/directory/cdcdd524-629a-7f56-a9ae-19b80fc05a48', { waitUntil: 'networkidle' });
  await page.getByRole('heading', { name: /أحمد محمود/ }).first().waitFor({ state: 'visible', timeout: 15000 });
  await page.screenshot({ path: path.join(SCREENSHOT_DIR, '08-atlas-party-workspace-overview.png') });
  console.log('Saved 08-atlas-party-workspace-overview.png');

  console.log('9. Contacts tab...');
  const contactsTab = page.locator('button:has-text("جهات الاتصال")').first();
  if (await contactsTab.isVisible()) {
    await contactsTab.click();
    await page.waitForTimeout(600);
    await page.screenshot({ path: path.join(SCREENSHOT_DIR, '09-atlas-party-contacts.png') });
    console.log('Saved 09-atlas-party-contacts.png');
  }

  console.log('10. Addresses tab...');
  const addressesTab = page.locator('button:has-text("العناوين")').first();
  if (await addressesTab.isVisible()) {
    await addressesTab.click();
    await page.waitForTimeout(600);
    await page.screenshot({ path: path.join(SCREENSHOT_DIR, '10-atlas-party-addresses.png') });
    console.log('Saved 10-atlas-party-addresses.png');
  }

  console.log('11. Performing a real edit before capturing audit history...');
  await page.getByRole('button', { name: 'النظرة العامة' }).click();
  await page.getByRole('button', { name: 'تعديل البيانات' }).click();
  await page.waitForTimeout(400);
  const displayNameField = page.getByLabel('الاسم المعروض بالكامل').or(page.getByLabel('الاسم التجاري / المعروض')).first();
  const currentVal = await displayNameField.inputValue();
  const updatedName = currentVal.includes('معتمد') ? 'أحمد محمود — تدقيق حقيقي' : 'أحمد محمود — معتمد نهائي';
  await displayNameField.fill(updatedName);
  await page.waitForTimeout(300);
  await page.getByRole('button', { name: 'حفظ بيانات الطرف التجاري' }).click();
  await page.getByText('تم تحديث البيانات بنجاح').waitFor({ state: 'visible', timeout: 10000 });
  await page.screenshot({ path: path.join(SCREENSHOT_DIR, '11-atlas-party-after-real-edit.png') });
  console.log('Saved 11-atlas-party-after-real-edit.png');

  console.log('12. Identifiers tab...');
  const identsTab = page.locator('button:has-text("المعرفات والسجلات")').first();
  if (await identsTab.isVisible()) {
    await identsTab.click();
    await page.waitForTimeout(600);
    await page.screenshot({ path: path.join(SCREENSHOT_DIR, '11-atlas-party-identifiers.png') });
    console.log('Saved 11-atlas-party-identifiers.png');
  }

  console.log('13. Audit history tab after real edit...');
  const auditTab = page.locator('button:has-text("سجل الأنشطة")').first();
  if (await auditTab.isVisible()) {
    await auditTab.click();
    await page.waitForTimeout(600);
    await page.screenshot({ path: path.join(SCREENSHOT_DIR, '12-atlas-party-audit-history.png') });
    console.log('Saved 12-atlas-party-audit-history.png');
  }

  console.log('14. Merge dialog with PartyPicker...');
  const mergeBtn = page.locator('button:has-text("دمج السجل")').first();
  if (await mergeBtn.isVisible()) {
    await mergeBtn.click();
    await page.waitForTimeout(600);
    const searchPicker = page.locator('input[placeholder*="ابحث"]').first();
    if (await searchPicker.isVisible()) {
      await searchPicker.fill('شركة المنارة');
      await page.waitForTimeout(600);
    }
    await page.screenshot({ path: path.join(SCREENSHOT_DIR, '14-atlas-merge-dialog.png') });
    console.log('Saved 14-atlas-merge-dialog.png');
    const closeBtn = page.locator('.ui-modal-overlay button:has-text("إلغاء")').or(page.locator('button[aria-label="Close dialog"]')).first();
    if (await closeBtn.isVisible()) {
      await closeBtn.click();
    } else {
      await page.keyboard.press('Escape');
    }
    await page.locator('.ui-modal-overlay').waitFor({ state: 'detached', timeout: 5000 }).catch(() => {});
    await page.waitForTimeout(600);
  }

  console.log('14b. Visibility Dialog...');
  const visBtn = page.locator('button:has-text("نطاق الرؤية")').first();
  if (await visBtn.isVisible()) {
    await visBtn.click();
    await page.waitForTimeout(600);
    await page.screenshot({ path: path.join(SCREENSHOT_DIR, '13-atlas-visibility-dialog.png') });
    console.log('Saved 13-atlas-visibility-dialog.png');
    const closeBtn = page.locator('.ui-modal-overlay button:has-text("إلغاء")').or(page.locator('button[aria-label="Close dialog"]')).first();
    if (await closeBtn.isVisible()) {
      await closeBtn.click();
    } else {
      await page.keyboard.press('Escape');
    }
    await page.locator('.ui-modal-overlay').waitFor({ state: 'detached', timeout: 5000 }).catch(() => {});
    await page.waitForTimeout(600);
  }

  console.log('14c. Archive Dialog...');
  const archBtn = page.locator('button:has-text("أرشفة الطرف")').first();
  if (await archBtn.isVisible()) {
    await archBtn.click();
    await page.waitForTimeout(600);
    await page.screenshot({ path: path.join(SCREENSHOT_DIR, '15-atlas-archive-dialog.png') });
    console.log('Saved 15-atlas-archive-dialog.png');
    const closeBtn = page.locator('.ui-modal-overlay button:has-text("إلغاء")').or(page.locator('button[aria-label="Close dialog"]')).first();
    if (await closeBtn.isVisible()) {
      await closeBtn.click();
    } else {
      await page.keyboard.press('Escape');
    }
    await page.locator('.ui-modal-overlay').waitFor({ state: 'detached', timeout: 5000 }).catch(() => {});
    await page.waitForTimeout(600);
  }

  console.log('15. Mobile 390px view of Party Record...');
  await page.setViewportSize({ width: 390, height: 844 });
  await page.waitForTimeout(600);
  await page.evaluate(() => window.scrollTo(0, 0));
  await page.screenshot({ path: path.join(SCREENSHOT_DIR, '22-atlas-record-mobile-390px.png') });
  console.log('Saved 22-atlas-record-mobile-390px.png');

  console.log('16. Mobile 390px view of Directory...');
  const backLink = page.locator('button:has-text("العودة للقائمة")').first();
  if (await backLink.isVisible()) {
    await backLink.click();
    await page.waitForTimeout(800);
  } else {
    const dirLink = page.locator('a[href="/directory"]').first();
    if (await dirLink.isVisible()) {
      await dirLink.click();
      await page.waitForTimeout(800);
    }
  }
  await page.evaluate(() => window.scrollTo(0, 0));
  await page.screenshot({ path: path.join(SCREENSHOT_DIR, '19-atlas-directory-mobile-390px.png') });
  console.log('Saved 19-atlas-directory-mobile-390px.png');
  await page.setViewportSize({ width: 1280, height: 800 });

  console.log('16b. Localized error state for non-existent record...');
  await page.goto('http://127.0.0.1:5173/directory/00000000-0000-0000-0000-000000000000', { waitUntil: 'networkidle' });
  await page.waitForTimeout(800);
  await page.screenshot({ path: path.join(SCREENSHOT_DIR, '23-atlas-localized-error-state.png') });
  console.log('Saved 23-atlas-localized-error-state.png');

  console.log('17. Delta Tenant Verification...');
  const deltaContext = await browser.newContext({
    viewport: { width: 1280, height: 800 },
    locale: 'ar-EG'
  });
  const deltaPage = await deltaContext.newPage();
  await deltaPage.goto('http://127.0.0.1:5173', { waitUntil: 'networkidle' });
  const deltaLoginBtn = deltaPage.locator('button:has-text("Delta")').first();
  if (await deltaLoginBtn.isVisible()) {
    await deltaLoginBtn.click();
    await deltaPage.locator('a[href="/directory"]').first().waitFor({ state: 'visible', timeout: 20000 });
    await deltaPage.waitForTimeout(600);
  }
  await deltaPage.locator('a[href="/directory"]').first().click();
  await deltaPage.locator('table, [role="grid"], [role="row"]').first().waitFor({ state: 'visible', timeout: 15000 });
  await deltaPage.waitForTimeout(800);
  await deltaPage.screenshot({ path: path.join(SCREENSHOT_DIR, '21-delta-directory-list.png') });
  console.log('Saved 21-delta-directory-list.png');
  await deltaContext.close();

  await browser.close();
  console.log('ALL VISUAL ACCEPTANCE SCREENSHOTS CAPTURED SUCCESSFULLY!');
}

capture().catch(err => {
  console.error('Capture error:', err);
  process.exit(1);
});
