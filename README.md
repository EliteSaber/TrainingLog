# TrainingLog

WPF-заготовка (.NET 10) под будущее приложение журнала тренировок.

## Требования

- Windows 10/11
- .NET SDK 10.0.x (`dotnet --version`)
- Для Visual Studio: Visual Studio 2026 с workloads **.NET desktop development**
- Для VS Code: расширения [C# Dev Kit](https://marketplace.visualstudio.com/items?itemName=ms-dotnettools.csdevkit) и [C#](https://marketplace.visualstudio.com/items?itemName=ms-dotnettools.csharp)

## Структура решения

```
TrainingLog.sln
Directory.Build.props        общие свойства сборки (Nullable, ImplicitUsings, анализаторы)
src/TrainingLog.Core/        net10.0 — доменная модель и интерфейсы, без зависимости от WPF
src/TrainingLog/             net10.0-windows — WPF-приложение (MVVM, CommunityToolkit.Mvvm)
.vscode/                     tasks.json, launch.json, extensions.json
```

MVVM: `CommunityToolkit.Mvvm` 8.2.2 — свойства через `[ObservableProperty]`, команды через `[RelayCommand]`.

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
dotnet run --project src\TrainingLog
```

Артефакт: `src\TrainingLog\bin\Debug\net10.0-windows\TrainingLog.exe`
