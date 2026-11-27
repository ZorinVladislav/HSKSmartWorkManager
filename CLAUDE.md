# HSK Smart Work Manager — контекст проекта

Мод для **RimWorld 1.6** под сборку **Hardcore SK (HSK)**. Язык: C#, библиотека Harmony.
Мод рассчитывает эффективность колонистов по каждому виду работ и распределяет приоритеты гибридно, не ломая ручные настройки игрока.

Общение с пользователем ведётся **на русском**. Компилятора у пользователя нет: DLL собирает GitHub Actions. Пользователь скачивает готовый zip из Releases и проверяет мод в игре.

---

## 1. Принятые решения

| Параметр | Решение |
|---|---|
| Распределение | Гибридное |
| Первоначальная настройка | Автоматическая |
| Ручные изменения | Сохраняются |
| Закрепление | Отдельно для каждой пары «колонист — профессия» |
| Новые колонисты | Рассчитываются автоматически |
| Новая экипировка | Можно запустить пересчёт |
| Работы из модов | Обнаруживаются автоматически |
| Платформа | HSK на RimWorld 1.6 |

**Главное правило: мод не борется с игроком за управление колонией.**

---

## 2. Архитектура

| Компонент | Задача |
|---|---|
| PawnAnalyzer | Собирает полный отчёт по колонисту |
| SkillAnalyzer | Навыки и страсти, связанные с видом работы (`WorkTypeDef.relevantSkills`) |
| StatAnalyzer | Игровые статы, соответствующие работе |
| EquipmentAnalyzer | Вклад экипировки в статы (**только для объяснения**) |
| HealthAnalyzer | Способности (Manipulation, Sight, Moving, Consciousness, Talking), болезни, недееспособность |
| EfficiencyCalculator | Оценка по профессиям (этап II) |
| HybridDistributor / PriorityManager | Распределение приоритетов (этап III) |
| ManualLockManager | Закрепления «колонист — профессия» (этап III) |
| WorkTab Integration | Интерфейс (этап IV) |
| WorkManagerGameComponent | Сохранение данных в сейве (этап III) |

### Ключевые технические правила

1. **Источником истины служит `pawn.GetStatValue(stat)`.** Статы вроде `ConstructionSpeed` уже включают `WorkSpeedGlobal`, манипуляцию, экипировку и здоровье. Отдельно прибавлять бонусы экипировки или здоровья **нельзя**, иначе они будут учтены дважды. EquipmentAnalyzer и HealthAnalyzer нужны только для объяснения оценки.
2. **Профили задаются данными, а не кодом.** Это кастомный `WorkProfileDef` в XML (`Defs/`): вид работы → категория (Critical / Primary / Auxiliary) → список статов с весами и флагом `inverted`, плюс `skillWeight` и `statWeight`. Новые профессии и поддержка модов добавляются XML-файлом без изменения кода.
3. **Ссылки на `StatDef` и `WorkTypeDef` в профилях хранятся строками.** Они разрешаются через `DefDatabase<T>.GetNamedSilentFail`. Если вида работы или стата нет (не установлен DLC, HSK его переименовал), ошибки загрузки не будет, а анализатор пометит его как «не найден».
4. **Автообнаружение работ.** Для каждого `WorkTypeDef` без профиля создаётся запасной профиль: `relevantSkills` + `WorkSpeedGlobal`, категория Primary, `isFallback = true`.
5. Код, читающий статы модов, оборачивается в try/catch: StatWorker из модов может выбросить исключение.
6. Пешки без `skills` или `workSettings` (животные, механоиды) обрабатываются корректно. Для `GetPriority` нужна проверка `workSettings != null && workSettings.EverWork`.
7. В 1.6 атрибут `DebugAction` находится в пространстве имён `LudeonTK`.

### Производительность

Пользователь играет в HSK, где поздняя игра и так тормозит. Мод не должен добавлять заметной нагрузки.

1. **Ничего не считать на каждом тике.** Рейтинги пересчитываются только по кнопке, при появлении нового колониста и по редкому таймеру (порядка раза в игровые сутки). Не использовать `Tick()` / `GameComponentTick()` для работы, которая может ждать; если таймер нужен, проверять в нём только счётчик.
2. **Не патчить горячие методы:** `Pawn_WorkSettings.GetPriority`, `WorkSettings.WorkGiversInOrder*`, `JobGiver_Work.TryIssueJobPackage`, поиск задач, расчёт статов (`StatWorker.GetValue*`), отрисовку карты. Допустимы патчи на редкие события: `SetPriority`, методы Work Tab для установки приоритета, открытие окон.
3. **Не добавлять свои WorkTypeDef и WorkGiverDef:** поиск работы пешками не должен усложняться.
4. **Кэшировать результаты.** Оценки хранятся до пересчёта; подсказки в интерфейсе читают кэш и ничего не считают при отрисовке. Во время `DoWindowContents` / `DoCell` не вызывать `GetStatValue` и не строить строки отчёта заново на каждый кадр.
5. **Большой пересчёт можно разбивать** по колонистам на несколько тиков, если он заметно дольше ~10 мс.
6. **Без LINQ и лишних аллокаций в часто вызываемом коде** (отрисовка ячеек, патчи). В редком коде (отчёт, пересчёт) LINQ допустим.
7. **Проверка:** в HSK есть Dubs Performance Analyzer. После этапов II–III попросить пользователя посмотреть в нём методы мода и прислать цифры.

### Гибридное распределение (этап III)

| Категория | Правило |
|---|---|
| Critical | Лучший специалист получает приоритет 1, второй — приоритет 2 (резерв), остальные — 3 или отключение по потребностям колонии |
| Primary | Распределяется между наиболее эффективными |
| Auxiliary | Достаётся оставшимся свободным колонистам |

Закрепления хранятся в `GameComponent` по ID пешки и defName `WorkTypeDef`. Отдельный флаг отличает автоматическое назначение от ручного. Ручные изменения отслеживаются Harmony-патчем на `Pawn_WorkSettings.SetPriority` (с защитой от срабатывания на собственные вызовы мода).

---

## 3. План разработки

- **Этап I — анализатор (текущий).** Ничего не меняет в игре. Dev-mode действие «HSK Smart Work → Анализ колонистов» открывает окно с отчётом (прокрутка, кнопка «Копировать») и пишет отчёт в файл `GenFilePaths.SaveDataFolderPath/HSKSmartWorkManager/analysis.txt`. Отчёт содержит:
  - все загруженные `WorkTypeDef`: defName, мод-источник (`modContentPack.Name`), `relevantSkills`, `naturalPriority`, есть ли явный профиль или запасной;
  - не найденные статы и виды работ из профилей;
  - `StatDef` из сторонних модов (не Core и не DLC), чтобы найти рабочие статы HSK;
  - по каждому колонисту: здоровье и способности; затем по каждой работе — отключена ли, текущий приоритет, навыки (уровень, страсть, отключён ли навык), значения статов и вклад экипировки.

  **Цель этапа I:** пользователь присылает отчёт из своей сборки HSK, и по нему пишутся профили для работ HSK (`Defs/WorkProfiles_HSK.xml`).
- **Этап II — расчёт эффективности.** Таблица рейтингов по профессиям, нормализация относительно лучшего колониста, объяснение каждой оценки.
- **Этап III — автораспределение.** Гибридная система, закрепления, сохранение в сейве.
- **Этап IV — интерфейс.** Кнопки, переключатели закреплений, объяснение расчётов, интеграция с вкладкой «Работы».
- **Этап V — проверка в HSK.** Дополнительные виды работ, Odyssey, моды из сборки.

**Принцип:** сначала анализатор с объяснением оценок, и только после проверки расчётов мод получает право менять приоритеты.

---

## 4. Структура репозитория

```text
HSKSmartWorkManager/
├── .github/workflows/build.yml     # сборка и публикация Release
├── About/About.xml
├── Defs/WorkProfiles_Vanilla.xml   # профили ванильных работ (priority 0)
├── Defs/WorkProfiles_HSK.xml       # профили HSK (priority 10, requiresMods)
├── Assemblies/                     # создаётся сборкой, в git не хранится
├── Source/
│   ├── HSKSmartWorkManager.csproj
│   ├── Core/ModInitializer.cs
│   ├── Profiles/WorkProfileDef.cs, WorkProfileRegistry.cs
│   ├── Analysis/PawnAnalyzer.cs, SkillAnalyzer.cs, StatAnalyzer.cs,
│   │            EquipmentAnalyzer.cs, HealthAnalyzer.cs, ReportFormatter.cs
│   └── Debug/DebugActions.cs, Window_AnalysisReport.cs
├── README.md
├── .gitignore                      # bin/ obj/ Assemblies/*.dll
└── CLAUDE.md
```

---

## 5. Сборка (GitHub Actions)

- csproj в SDK-стиле, `net472`, `LangVersion latest`, `OutputPath ../Assemblies/`, без подпапки фреймворка в пути.
- NuGet-пакеты:
  - `Krafs.Rimworld.Ref` версии `1.6.*` — ссылочные сборки RimWorld, файлы игры не нужны;
  - `Lib.Harmony` версии `2.3.*` с `ExcludeAssets="runtime"` — Harmony поставляется модом Harmony, его DLL не копируем;
  - `Microsoft.NETFramework.ReferenceAssemblies` — для сборки net472 на Linux.
- Workflow запускается при push в `main` и вручную (`workflow_dispatch`): `ubuntu-latest` → `actions/setup-dotnet` → `dotnet build -c Release` → упаковка zip (About, Defs, Assemblies) → публикация в **Release с постоянным тегом `latest`** (перезаписывается при каждой сборке), чтобы пользователь всегда скачивал по одной и той же ссылке. Также загружается artifact.
- Если сборка упала, читать лог через `gh run view --log-failed`, исправлять, пушить снова.
- **DLL игры (Assembly-CSharp, UnityEngine) в репозиторий не класть.**

---

## 6. Текущее состояние

Этап I написан и компилируется (`dotnet build` проходит локально, предупреждений нет):

- `About/About.xml`: packageId `Mioko.HSKSmartWorkManager`, author `Mioko`, зависимость `brrainz.harmony`;
- `Source/Profiles`: `WorkProfileDef` и `WorkProfileRegistry`. Профиль из мода, загруженного позже, перекрывает профиль для того же workType; список перекрытий выводится в отчёте;
- `Source/Analysis`: анализаторы и `ReportFormatter`. Отчёт: активные моды с packageId, WorkTypeDef, проблемы профилей, статы сторонних модов, колонисты;
- `Source/Debug`: dev-действия «Анализ колонистов» и «Пересобрать профили», окно отчёта (рисует только видимые строки, чтобы большой текст не обрезался);
- `Defs/WorkProfiles_Vanilla.xml`: профили ванильных работ.
- `Defs/WorkProfiles_HSK.xml`: первая версия профилей HSK, составлена по репозиторию сборки, до получения отчёта из игры.
- У `WorkProfileDef` есть поля `priority` (при нескольких профилях на одну работу побеждает больший, при равенстве — загруженный позже; порядок файлов внутри мода не гарантирован) и `requiresMods` (packageId без учёта регистра; если ни один не активен, профиль не применяется и попадает в отчёт).

## 7. Решения по процессу и открытые вопросы

- Ник автора: **Mioko**.
- Release `latest` публикуется **только из `main`**. На PR workflow запускает проверочную сборку без публикации (плюс artifact). Разработка идёт в ветках `claude/*` через PR.
- В облачном контейнере .NET SDK ставится через `apt-get install dotnet-sdk-8.0`, и сборку можно проверить до push.
- **Следующий шаг:** пользователь присылает `analysis.txt`, по нему пишется `Defs/WorkProfiles_HSK.xml`.

---

## 8. Сборка HSK: факты для разработки

Источник: https://github.com/HardcoreSK/Hardcore-SK, ветка `alpha` (hcsk 1.6.4518 rev 84, RimWorld 1.6.4871, все 5 DLC). Репозиторий весит несколько ГБ; для просмотра клонировать с `--depth 1 --filter=blob:none --no-checkout` и извлекать нужные файлы через `git checkout alpha -- <путь>`.

- **Ядро:** `skyarkhangel.hsk` (папка `Mods/Core_SK`), `skyarkhangel.hsk.core`, `skyarkhangel.hsk.dlc`. Harmony: `brrainz.harmony`. Все они в `loadAfter`.
- **Work Tab** (`arof.fluffy.worktab.continued`) входит в сборку: приоритеты 1–9, приоритеты по WorkGiver и по часам. Интерфейс этапа IV встраивается в Work Tab, а не в ванильную вкладку. Перехват ручных изменений (этап III) нужно проверить по DLL Work Tab: его методы установки приоритета могут не вызывать ванильный `SetPriority`.
- **Виды работ из модов:** `HaulingUrgent`, `FinishingOff` (Allow Tool), `BiotechHauling` (ядро HSK), `Diplomat` (Hospitality), `QuarryMining` (Quarry), `NuclearWork` (Rimatomics, без навыков).
- **Рабочие статы HSK:** `SmithingSpeed`, `TailoringSpeed`, `StonecuttingSpeed`, `ElectronicCraftingSpeed`, `CollectingSpeed` (сбор глины, песка, торфа; навык Mining).
- **Survival Tools Lite** (`skyarkhangel.survivaltoolslite`): `MiningYieldDigging`, `PlantHarvestingSpeed`, `TreeFellingSpeed` — учитывают инструменты через `StatPart_SurvivalTool`, то есть уже входят в `GetStatValue`.
- **Combat Extended** меняет боевые статы (важно для охоты и добивания).
- Моды, тоже влияющие на работу: Pick Up And Haul, Cleaning Priority, Common Sense, WVC Work Modes.

---

## 9. Описание репозитория

**Description:** RimWorld 1.6 mod for Hardcore SK: rates colonists' work efficiency and auto-assigns work priorities without overriding your manual choices. / Мод для RimWorld 1.6 и Hardcore SK: оценка эффективности колонистов и автораспределение работ с сохранением ручных настроек.

**Topics:** `rimworld` `rimworld-mod` `hardcore-sk` `harmony` `csharp`

**Вступление README:**

> **HSK Smart Work Manager**
>
> A RimWorld 1.6 mod built for the Hardcore SK modpack. It analyzes each colonist's skills, passions, work stats, equipment and health, calculates their efficiency for every work type, and distributes priorities using a hybrid approach: critical jobs get a main specialist and a backup, primary jobs go to the most efficient workers, and auxiliary jobs fill the remaining capacity. Manual changes are never overwritten: you can lock any colonist–work pair, and recalculation will leave it untouched.
>
> Мод для RimWorld 1.6, созданный под сборку Hardcore SK. Он анализирует навыки, страсти, рабочие характеристики, экипировку и здоровье каждого колониста, рассчитывает его эффективность в каждой профессии и распределяет приоритеты гибридно. Критические работы получают основного специалиста и резервного, основные достаются самым эффективным колонистам, а вспомогательные распределяются между остальными. Ручные изменения никогда не перезаписываются: любую пару «колонист — профессия» можно закрепить, и пересчёт её не тронет.
>
> ⚠️ Status: early development (stage I — read-only analyzer). / Статус: ранняя разработка (этап I — анализатор, ничего не меняет в игре).
