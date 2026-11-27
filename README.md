HSK Smart Work Manager

A RimWorld 1.6 mod built for the Hardcore SK modpack. It analyzes each colonist's skills, passions, work stats, equipment and health, calculates their efficiency for every work type, and distributes priorities using a hybrid approach: critical jobs get a main specialist and a backup, primary jobs go to the most efficient workers, and auxiliary jobs fill the remaining capacity. Manual changes are never overwritten: you can lock any colonist–work pair, and recalculation will leave it untouched.

Мод для RimWorld 1.6, созданный под сборку Hardcore SK. Он анализирует навыки, страсти, рабочие характеристики, экипировку и здоровье каждого колониста, рассчитывает его эффективность в каждой профессии и распределяет приоритеты гибридно. Критические работы получают основного специалиста и резервного, основные достаются самым эффективным колонистам, а вспомогательные распределяются между остальными. Ручные изменения никогда не перезаписываются: любую пару «колонист — профессия» можно закрепить, и пересчёт её не тронет.

⚠️ Status: early development (stage I — read-only analyzer). / Статус: ранняя разработка (этап I — анализатор, ничего не меняет в игре).

---

## Installation / Установка

1. Download `HSKSmartWorkManager.zip` from [Releases → latest](https://github.com/ZorinVladislav/HSKSmartWorkManager/releases/tag/latest). / Скачайте `HSKSmartWorkManager.zip` из [Releases → latest](https://github.com/ZorinVladislav/HSKSmartWorkManager/releases/tag/latest).
2. Extract it into `RimWorld/Mods`. / Распакуйте в папку `RimWorld/Mods`.
3. Enable the mod **after Harmony**. / Включите мод **после Harmony**.

## Stage I: analyzer / Этап I: анализатор

1. Enable Dev Mode (Options → Development mode). / Включите режим разработчика (Настройки → Режим разработчика).
2. Load a colony, open the debug actions menu and run **HSK Smart Work → Анализ колонистов**. / Загрузите колонию, откройте меню отладочных действий и запустите **HSK Smart Work → Анализ колонистов**.
3. The report opens in a window (button «Копировать» copies it) and is saved to: / Отчёт откроется в окне (кнопка «Копировать») и сохранится в файл:
   - Windows: `%USERPROFILE%\AppData\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios\HSKSmartWorkManager\analysis.txt`

The analyzer never changes the game state. / Анализатор ничего не меняет в игре.

## Work profiles / Профили работ

Profiles live in `Defs/` as `HSKSmartWorkManager.WorkProfileDef` and reference work types and stats by `defName`, so missing DLCs or mods cause no load errors. Work types without a profile get an automatic fallback profile (relevant skills + `WorkSpeedGlobal`).

Профили лежат в `Defs/` (`HSKSmartWorkManager.WorkProfileDef`) и ссылаются на виды работ и статы по `defName`, поэтому отсутствующие DLC и моды не вызывают ошибок. Для работ без профиля создаётся запасной (навыки работы + `WorkSpeedGlobal`).

## Building / Сборка

GitHub Actions builds the DLL on every push to `main` and publishes the `latest` release; pull requests get a check build. Locally: `dotnet build Source/HSKSmartWorkManager.csproj -c Release` (no game files required).
