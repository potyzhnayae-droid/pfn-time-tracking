# Интеграция большой готовой БД

Приложение поддерживает 2 режима:

- `SQLite` (локально, по умолчанию)
- `SQL Server` (готовая внешняя БД)

## 1) Включить режим готовой БД

В `appsettings.json` заполните:

- `ConnectionStrings:ReadyDbConnection`
- `DatabaseIntegration:UseReadyDb = true`
- `DatabaseIntegration:SeedDemoData = false` (для готовой БД обычно обязательно)

Пример:

```json
"ConnectionStrings": {
  "DefaultConnection": "Data Source=Data\\pfn.db;Cache=Shared",
  "ReadyDbConnection": "Server=SERVER\\INSTANCE;Database=BigPayrollDb;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true"
},
"DatabaseIntegration": {
  "UseReadyDb": true,
  "SeedDemoData": false
}
```

## 2) Подтянуть схему Database First (если структура отличается)

Запустите в каталоге проекта:

```powershell
dotnet ef dbcontext scaffold "Server=SERVER\INSTANCE;Database=BigPayrollDb;Trusted_Connection=True;TrustServerCertificate=True" Microsoft.EntityFrameworkCore.SqlServer `
  --context ReadyDbScaffoldContext `
  --output-dir Models/Scaffold `
  --schema dbo `
  --use-database-names `
  --no-onconfiguring `
  --force
```

Для очень большой БД лучше ограничить таблицы:

```powershell
dotnet ef dbcontext scaffold "<connection>" Microsoft.EntityFrameworkCore.SqlServer `
  --context ReadyDbScaffoldContext `
  --output-dir Models/Scaffold `
  --table Departments `
  --table Employees `
  --table WorkDays `
  --table Overtime `
  --table WorkScheduleExceptions `
  --use-database-names `
  --no-onconfiguring `
  --force
```

## 3) Что уже сделано в проекте

- Добавлен SQL Server provider в проект.
- Поддержка переключения провайдера через `DatabaseIntegration:UseReadyDb`.
- Для SQL Server включены:
  - Retry policy (`EnableRetryOnFailure`)
  - увеличенный timeout (`CommandTimeout=180`).

