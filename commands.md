# TODIR ERP — أوامر تشغيل المشروع

نفّذ الأوامر من PowerShell، وكل خدمة في نافذة مستقلة.

## 1. تشغيل Docker

```powershell
cd G:\projects\todir-erp
docker compose up -d
```

## 2. تطبيق Database Migrations

```powershell
cd G:\projects\todir-erp
$env:TODIR_MIGRATOR_CONNECTION="Host=127.0.0.1;Port=15432;Database=todir;Username=postgres;Password=postgres"
dotnet run --project backend\DbMigrator\DbMigrator.csproj -- --no-seed
```

لتشغيل Development Seed بدلًا من `--no-seed`:

```powershell
$env:DOTNET_ENVIRONMENT="Development"
$env:SEED_DEFAULT_PASSWORD="ضع-كلمة-مرور-تطوير-قوية-هنا"
dotnet run --project backend\DbMigrator\DbMigrator.csproj -- --seed development
```

## 3. تشغيل Backend API

```powershell
cd G:\projects\todir-erp
dotnet run --project backend\Api\Api.csproj
```

Backend API:

```text
http://127.0.0.1:5080
```

## 4. تشغيل Worker

```powershell
cd G:\projects\todir-erp
dotnet run --project backend\Worker\Worker.csproj
```

الـ Worker مسؤول عن المعالجة الخلفية والرسائل والمهام المجدولة.

## 5. تشغيل Frontend

```powershell
cd G:\projects\todir-erp\frontend
npm run dev
```

Frontend:

```text
http://localhost:5173/
```

## ملاحظات

- شغّل الـ API والـ Worker والـ Frontend في نوافذ PowerShell منفصلة.
- `npm` يُستخدم للـ Frontend فقط؛ الـ Backend والـ Worker يعملان بواسطة .NET.
- لإيقاف أي خدمة اضغط `Ctrl+C` داخل نافذتها.
- لإيقاف Docker بعد الانتهاء:

```powershell
cd G:\projects\todir-erp
docker compose down
```
