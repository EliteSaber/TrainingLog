# TrainingLog

<div align="center">

  <img src="docs/logo.png" width="180" alt="TrainingLog" />

</div>

[![Release](https://img.shields.io/github/v/release/EliteSaber/TrainingLog.svg)](https://github.com/EliteSaber/TrainingLog/releases/latest)

Приложение журнала тренировок (WPF, .NET 10).

## Скачать

Готовые сборки лежат на странице релизов:
https://github.com/EliteSaber/TrainingLog/releases/latest

- `TrainingLog-1.0.0-win-x64.exe` — один файл, нужен .NET 10 Desktop Runtime;
- `TrainingLog-1.0.0-win-x64-selfcontained.zip` — ничего доустанавливать не надо.

Настройки `settings.json` пишутся рядом с программой и при отсутствии создаются при
первом запуске. База SQLite лежит в `%LOCALAPPDATA%\TrainingLog\exercises.db`,
миграции применяются при старте.

exe не подписан, поэтому Windows может показать предупреждение SmartScreen: «Подробнее» →
«Выполнить».

## Требования

- Windows 10/11
- .NET SDK 10.0.x (`dotnet --version`)
- Для Visual Studio: Visual Studio 2026 с workloads **.NET desktop development**
- Для VS Code: расширения [C# Dev Kit](https://marketplace.visualstudio.com/items?itemName=ms-dotnettools.csdevkit) и [C#](https://marketplace.visualstudio.com/items?itemName=ms-dotnettools.csharp)

## Структура решения

```
TrainingLog.sln
Directory.Build.props        общие свойства сборки (Nullable, ImplicitUsings, анализаторы)
src/TrainingLog.Core/        net10.0 — доменная модель, EF Core и репозитории
src/TrainingLog/             net10.0-windows — WPF-приложение (MVVM, CommunityToolkit.Mvvm)
tests/TrainingLog.Tests/     тесты xUnit
.vscode/                     tasks.json, launch.json, extensions.json
docs/logo.png                логотип для шапки этого файла
docs/releases/               заметки к релизам: их читает публикация на GitHub
.github/workflows/           тесты на каждом коммите и сборка релиза по тегу
```

MVVM: `CommunityToolkit.Mvvm` 8.2.2 — свойства через `[ObservableProperty]`, команды через `[RelayCommand]`.

Зависимости внедряются контейнером `Microsoft.Extensions.DependencyInjection`, сборка —
в `App.OnStartup`. В `App.xaml` нет `StartupUri`: окно создаётся вручную, иначе в него
не попадут зависимости.

## Данные

SQLite, файл `%LOCALAPPDATA%\TrainingLog\exercises.db`. Схема применяется миграциями EF Core.

Таблицы: `Exercises` (справочник упражнений), `TrainingPlans` и `PlanExercises` (планы и
состав с порядком выполнения), `TrainingSessions`, `ExerciseEntries`, `TrainingSets` (журнал
тренировок: день, упражнения дня, подходы).

Дубликаты по регистру блокирует колонка `NameKey` с уникальным индексом: сравнение строк
в SQLite складывает регистр только для ASCII, поэтому «жим» и «Жим» там разные значения.

На дату приходится ровно одна запись журнала — это уникальный индекс по `Date`. Запись хранит
и ссылку на план, и копию его названия (для упражнений так же), поэтому удаление или
переименование плана и упражнений не стирает историю тренировок.

### Работа с миграциями

Инструмент ставится один раз на машину:

```powershell
dotnet tool install --global dotnet-ef --version "10.*"
```

Добавить миграцию после изменения моделей EF:

```powershell
dotnet ef migrations add <Имя> --project src\TrainingLog.Core --startup-project src\TrainingLog.Core
```

Миграции лежат в `TrainingLog.Core\Migrations`.

## Настройки

Файл `settings.json` лежит рядом с программой и копируется туда при сборке
(`CopyToOutputDirectory`). Сейчас в нём одно число — `DaysCount`, сколько дней показывать
в журнале; годные значения от 1 до 366.

Правка: меню-шестерёнка → «Параметры…» (последний пункт, за разделителем). «Сохранить»
записывает значение в файл, и главное окно сразу подхватывает его и перечитывает журнал —
показанных строк становится столько, сколько сохранено. Кнопка «Применить» на главном окне
отдельно применяет число, введённое в её поле, и в файл не пишет.

Если файла нет, он создаётся при первом запуске со значением `3`. Негодный файл (испорченный
JSON или значение вне 1..366) не мешает запуску: приложение берёт `3` и не переписывает файл,
чтобы не затереть то, что правили руками. В папке без права записи (например, под
`Program Files`) сохранение завершается красной надписью в окне параметров, а не ошибкой.

Файл принадлежит папке программы: `dotnet clean` его удалит, и настройка вернётся к трём дням.

## Visual Studio 2026

1. `File → Open → Project/Solution` → `TrainingLog.sln`
2. Стартовый проект — `TrainingLog`, `Ctrl+F5` (без отладки) или `F5`

## VS Code

1. `code .` в корне репозитория и установить рекомендуемые расширения
2. `Ctrl+Shift+B` — сборка решения
3. `F5` — запуск с отладкой (конфигурация `TrainingLog` в `.vscode/launch.json`)

## Командная строка

```powershell
dotnet restore TrainingLog.sln
dotnet build   TrainingLog.sln -c Debug
dotnet test    TrainingLog.sln
dotnet run --project src\TrainingLog
```

Артефакт: `src\TrainingLog\bin\Debug\net10.0-windows\TrainingLog.exe`
