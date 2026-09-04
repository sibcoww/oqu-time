# SchoolScheduler

Система автоматического составления школьного расписания.

## Сборка установщика

Для production-сборки нужен .NET 10 SDK и Inno Setup 6. Команда:

```powershell
.\installer\build-installer.ps1
```

Готовый self-contained установщик x64 создаётся в `artifacts\installer`. Установленный
SchoolScheduler не требует отдельно установленного .NET. Пользовательская база хранится в
`%LocalAppData%\SchoolScheduler\data\school.db`, сохраняется при обновлении, а при удалении
приложения удаляется только после отдельного подтверждения пользователя.

## Структура решения
- Core — доменная модель (сущности)
- Data — EF Core / SQLite 
- Scheduling — алгоритм расписания
- ImportExport — Excel
- App — WPF приложение
