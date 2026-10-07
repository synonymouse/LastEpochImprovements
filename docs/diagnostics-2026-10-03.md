# Диагностика после обновления Last Epoch — 2026-10-03

## Установленный блокер

Сгенерированная `MelonLoader/Il2CppAssemblies/UnityEngine.CoreModule.dll`
содержит три определения типа с одним полным именем `<>O`.
Обычная загрузка этой DLL в отдельном процессе воспроизводит ошибку:

```text
System.BadImageFormatException: Duplicate type with name '<>O' in assembly
'UnityEngine.CoreModule, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null'.
```

Ошибка воспроизводится на .NET 6.0.21 и .NET 8.0.27 без запуска игры,
MelonLoader, нашего мода или UnityExplorer. Это проверка конкретного блокера
загрузки DLL; полноценный запуск игры после исправления всё равно необходим.

### Сравнение метаданных

Все пути ниже относительно папки игры.

| Файл | Дублирующиеся полные имена типов |
|---|---|
| `MelonLoader/Dependencies/Il2CppAssemblyGenerator/Cpp2IL/cpp2il_out/UnityEngine.CoreModule.dll` | Нет |
| `MelonLoader/Dependencies/Il2CppAssemblyGenerator/UnityDependencies/UnityEngine.CoreModule.dll` | Нет |
| `MelonLoader/Il2CppAssemblies/UnityEngine.CoreModule.dll` | `<>O`: три определения |

Таким образом, некорректные метаданные появляются на этапе генерации interop-сборок.
Конкретный внутренний метод генератора, создающий дубликаты, локально не исследован.

### Что видно в логе запуска

В `MelonLoader/Latest.log`:

- Строка 20: Unity `6000.4.8f1`; MelonLoader `0.7.2`, runtime `net6`.
- Строка 44: Il2CppInterop `1.5.1+dbaf825aab891ff1e1627bea691bde0a81d1ad98`.
- Строка 577: `Assembly Generation Successful!` — генератор не обнаружил проблему.
- Строки 586–627: ошибка загрузки `UnityEngine.CoreModule`; зависимость недоступна
  и нашему моду, и UnityExplorer.
- Строки 634–639: обнаружены наш мод `1.4.6` и UnityExplorer; сообщение
  `2 Mods loaded` само по себе не подтверждает успешную инициализацию.
- Строка 641: `No Support Module Loaded!`; на этом лог заканчивается.

В архивном `MelonLoader/Logs/26-3-31_19-11-19.log`:

- Unity `6000.0.42f1`, игра `1.4.1.2`, тот же MelonLoader `0.7.2`.
- Строка 74: `Support Module Loaded: .../SupportModules/Il2Cpp.dll`.
- Строки 90–91: загрузка настроек нашего мода и регистрация `CustomIconProcessor`.
- Далее есть ошибки отдельных функций, но окружение запускало код мода.

Текущий блокер возникает раньше проверки совместимости Harmony-патчей
и игровых API нашего проекта.

## Где лежат логи и настройки

Папка игры: `C:\Program Files (x86)\Steam\steamapps\common\Last Epoch`.

- Последний лог загрузчика: `MelonLoader/Latest.log`.
- Архив запусков: `MelonLoader/Logs/`.
- Исследованный запуск: `MelonLoader/Logs/26-10-3_14-6-22.log`.
- Настройки мода: `UserData/kg_LastEpoch_Improvements.cfg`.
- Лог Unity: `C:\Users\user\AppData\LocalLow\Eleventh Hour Games\Last Epoch\Player.log`.
- Предыдущий лог Unity: там же `Player-prev.log`.

При следующем запуске `Latest.log` и `Player.log` могут измениться;
для сравнения использовать архив конкретного запуска.

## Как устроен проект

```text
Last Epoch.exe (Unity / IL2CPP)
  → MelonLoader
  → Cpp2IL: дамп описаний игровых типов
  → Il2CppInterop: генерация .NET-обёрток в MelonLoader/Il2CppAssemblies
  → загрузка зависимостей и IL2CPP support module
  → инициализация MelonMod и установка Harmony-патчей
  → обработка событий игры, обновления кадра и интерфейс мода
```

Исходники находятся в `kg_LastEpoch_Improvements/`:

| Файл | Назначение |
|---|---|
| `kg_LastEpoch_Improvements.cs` | Точка входа `MelonMod`, настройки, шаблон иконки, иконки лута, патчи tooltip/filter/settings; дополнительные камера и раскрытие карты в Special |
| `AffixRolls.cs` | Форматирование значений роллов аффиксов |
| `Experimental.cs` | Патч текста названий предметов на земле |
| `PickupItems.cs` | Подбор предметов в радиусе при удержании `F` |
| `BazaarStuff.cs` | Поиск по предмету в Bazaar через `Shift + среднюю кнопку мыши` |
| `Utils.cs` | Вспомогательные функции и добавление элементов в настройки игры |
| `RaresOnMap.cs`, `ShrinesOnMap.cs` | Иконки редких противников и святилищ, только Special |
| `UI_QoL.cs` | Дополнительные кнопки интерфейса, только Special |

Ключевые точки в `kg_LastEpoch_Improvements.cs`:

- `OnInitializeMelon()` (строка 235) создаёт настройки, загружает конфиг
  и вызывает `CreateCustomMapIcon()`.
- `CreateCustomMapIcon()` (строка 167) регистрирует `CustomIconProcessor`
  в IL2CPP и создаёт шаблон иконки.
- `OnUpdate()` (строка 201) вызывает `BazaarStuff.Update()` и `PickupItems.Update()`.
- `[HarmonyPatch(...)]` перехватывают игровые методы для tooltip,
  фильтра лута, появления предметов и настроек.
- `SettingsPanelTabNavigable_Awake_Patch` (строка 398) добавляет UI настроек.

Проект нацелен на `net6.0` и ссылается на DLL из установленной игры.
`Release` — обычная сборка; `Special` включает `SPECIALVERSION`.
`ILRepack.targets` упаковывает `fastJSON` вместе с модом.
В `.csproj` также есть автоматическое копирование итоговой DLL в папку `Mods`.
Сохранённые значения конфига имеют приоритет над значениями по умолчанию в коде.

## Воспроизводимая проверка блокера

Временный диагностический проект сохранён отдельно от репозитория:
`C:\Users\user\AppData\Local\Temp\opencode\le-interop-diagnostic-20261003`.
Он только читает DLL и не меняет файлы игры.

Команда для повторения проверки после любого изменения окружения:

```bat
dotnet "C:\Users\user\AppData\Local\Temp\opencode\le-interop-diagnostic-20261003\bin\Debug\net6.0\InteropProbe.dll" "C:\Program Files (x86)\Steam\steamapps\common\Last Epoch\MelonLoader\Il2CppAssemblies\UnityEngine.CoreModule.dll"
```

Текущий результат:

```text
Runtime: 6.0.21
FAIL: Reproduced MelonLoader's duplicate-type error: Duplicate type with name '<>O' ...
```

Код возврата: `1` — воспроизведён именно этот баг; `0` — DLL загрузилась;
`2` — другая ошибка, результат не подтверждает исправление.
Для просмотра дублей метаданных перед путём DLL добавить `--metadata`.

## Подтверждения из upstream и следующий шаг

- [MelonLoader #1142](https://github.com/LavaGang/MelonLoader/issues/1142):
  такой же баг при переходе на Unity `6000.4.x`. В комментарии описана
  проблема unstripper и сторонний обходной инструмент; локально он не проверен.
- [MelonLoader #1218](https://github.com/LavaGang/MelonLoader/issues/1218):
  открытый баг именно для Last Epoch / Unity `6000.4.8f1`, с тем же сообщением,
  на MelonLoader `0.7.4-ci.2607` и более новом Il2CppInterop.
- На момент проверки последний стабильный релиз MelonLoader — `0.7.3`.
  Успешность обновления для этого окружения не проверена.

Следующий шаг — устранить дубликаты на уровне генератора/генерируемой DLL
с сохранением оригинала, затем повторить диагностическую команду.
После её успешного результата запустить игру и проверить, что support module
загрузился и появились сообщения инициализации нашего мода.
Только затем оценивать необходимость адаптации нашего кода к новым игровым API.
Исходники мода и файлы установленной игры в ходе этой диагностики не изменялись.

## Повторная проверка после обновления до MelonLoader 0.7.3

Пользователь переустановил MelonLoader через инсталятор и запустил игру.
Новый архив запуска: `MelonLoader/Logs/26-10-3_16-9-34.log`.
После переустановки в папке `Logs` остался только этот файл; упомянутые выше
старые логи были исследованы до переустановки.

В новом `Latest.log`:

- Строка 5: MelonLoader `0.7.3`.
- Строка 46: Il2CppInterop обновился до
  `1.5.1-ci.845+f03c8f4ae507d47ea814f3d11d1ec6b0391c1576`.
- Строка 61: `Assembly Generation Needed!`; сборки действительно пересозданы.
- Строка 386: `Assembly Generation Successful!`.
- Строки 395–436: прежний `Duplicate type with name '<>O'`, зависимость
  `UnityEngine.CoreModule` недоступна обоим модам.
- Строка 450: `[ERROR] No Support Module Loaded!`; лог на этом заканчивается.

Отдельная проверка новой DLL также завершилась кодом `1` и прежней ошибкой.
Чтение метаданных снова обнаружило три определения глобального типа `<>O`.
Таким образом, обновление до стабильной `0.7.3` с новой генерацией
не устранило этот конкретный блокер на нашей установке.

## Применённый локальный обход

Добавлена отдельная утилита
[`tools/RepairUnityInterop`](../tools/RepairUnityInterop/README.md).
В трёх конфликтующих TypeDef нет полей, методов, свойств, событий,
вложенных типов, generic-параметров, интерфейсов или custom attributes.
Они помечены `NestedPublic`, но не имеют родителя.

Утилита исправила видимость этих пустых типов на `NotPublic` и использовала
существующие namespace-строки, чтобы получить разные полные имена:
`<>O`, `UnityEngine.<>O` и `UnityEngine.Rendering.<>O`.
Изменились только девять байт полей Flags/Namespace трёх строк TypeDef;
остальные байты DLL и все metadata tokens сохранились.

Исправленный файл установлен в `MelonLoader/Il2CppAssemblies/UnityEngine.CoreModule.dll`.
Оригинал сохранён рядом:

```text
UnityEngine.CoreModule.dll.interop-repair-20261003-162639-876.bak
```

Проверки после установки:

- Оригинальная резервная копия воспроизводит `Duplicate type '<>O'`, код `1`.
- Исправленная установленная DLL загружается в процессе .NET 6.0.21, код `0`.
- В исправленной копии нет дублирующихся полных имён типов.
- Повторный запуск утилиты возвращает `No duplicate full type names found; no files changed.`
- `force_regeneration` в `UserData/Loader.cfg` остаётся `false`.

Подтверждено устранение конкретного блокера загрузки DLL.
Следующий шаг — запуск игры пользователем и проверка нового лога на успешную
инициализацию support module и нашего мода. Результат этой runtime-проверки
на момент применения обхода ещё не получен.

## Запуск после обхода: загрузчик работает, выявлены устаревшие игровые API

В запуске от 16:41:33:

- Строка 76: `Support Module Loaded: .../SupportModules/Il2Cpp.dll`.
- Строка 122: наш мод загрузил настройки.
- При группировке ошибок `TooltipItemManager.get_instance()` встретился
  22 553 раза. Стек: `BazaarStuff.Update()` → `OnUpdate()`.
- Инициализация иконки падает на отсутствующем
  `EpochExtensions.AddComponent<T>(UnityEngine.Component)`.
- Не найдены прежние сигнатуры Harmony-целей:
  `GroundItemLabel.SetGroundTooltipText(bool)` и две перегрузки
  `GroundItemVisuals.initialise(...)`.
- UnityExplorer также не завершает инициализацию: новый `SceneHandle`
  несовместим со старым кодом поиска сцен, а fallback `GetAllScenes()`
  бросает `NotSupportedException: Method unstripping failed`.

Компиляция исходников против новых сборок через `dotnet msbuild ... -t:Compile
-p:Configuration=Special` обнаружила 16 ошибок совместимости. Среди них
прежние singleton-свойства `instance`, `GroundItemLabel.all`, поля панелей
Bazaar/MultiPicker, `FilterUI.ResetUI`, `clearFilterButton` и `InputRange.min`.
Compile-only проверка не заменяла установленную DLL мода.

Для получения точных runtime-сигнатур подготовлен временный диагностический
мод `Mods/KG_ApiProbe.dll`. Он только отражает нужные типы и выводит свойства,
поля и сигнатуры методов с префиксом `[LE-ApiProbe-20261003]`; значения
свойств и игровые методы не вызываются. Исходник находится отдельно от
репозитория в `C:\Users\user\AppData\Local\Temp\opencode\le-api-probe-20261003`.
Сборка диагностического мода успешна, без ошибок и предупреждений.

Нужен следующий запуск до главного меню для получения этого дампа.
После использования удалить временные `KG_ApiProbe.dll`,
`KG_ApiProbe.deps.json` и `KG_ApiProbe.pdb` из `Mods`.

## Адаптация мода к текущему API

Следующий запуск получил runtime-дамп нужных типов. Дополнительно тот же
инспектор адаптирован для отдельного .NET-процесса: через reflection он читает
реальные C#-сигнатуры сгенерированных сборок без вызова игровых методов.
Для поиска доступны CLI-режимы `type`, `method`, `assetmethod` и просмотр
одного типа по полному имени. Служебные поля `Native*` исключены из вывода.

Подтверждённые изменения и применённые замены:

| Старый API | Текущий API |
|---|---|
| `TooltipItemManager.instance.activeParameters.Item` | `UIBase.instance.TooltipSystem.ActiveItemTooltipItem` |
| `ItemList.instance`, `AffixList.instance` | `ItemList.get()`, `AffixList.get()` |
| `GroundItemLabel.all` | `GroundItemVisuals.all`, затем `visuals.label` |
| `GroundItemLabel.SetGroundTooltipText(bool)` | `SetGroundTooltipText()` |
| `GroundItemVisuals.initialise` с `GroundItemRarityVisuals` | Единственная перегрузка с `GroundItemRarityVisualsV2` |
| Поля `_bazaarPanel`, `_multiPickerModal` | `PanelSystem.GetPanelIfOpen<BazaarPanel/MultiPickerModal>()` |
| `FilterUI.ResetUI()` | `ResetUI(bool attemptLoadFilter, Nullable<BazaarStallType> stallType, bool skipAutoSubmitRequest)` |
| Запись в `InputRange.min.text` | Присваивание `InputRange.MinValue` |
| `TooltipItemManager.GetItemSprite(...)` | Статический `ItemData.GetItemSprite(...)`, возвращающий `SoftRef<Sprite>` |
| Игровой extension `textComponent.AddComponent<Outline>()` | Стандартный `textChild.AddComponent<Outline>()` |

Иконка теперь создаёт `LoadRef<Sprite>` через `SoftRefExtensions.CreateLoadRef`,
назначает спрайт после статуса `Loaded` и сохраняет handle до уничтожения
иконки. `OnDestroy` освобождает handle через `Dispose`.
Добавлена ссылка проекта на `Il2CppLE.Addressables.dll`.
При поиске в Bazaar корутина ожидает открытия динамически загружаемых панелей.

### Проверки и установленный файл

- `dotnet build kg_LastEpoch_Improvements.sln -c Release`: успешно.
- `dotnet build kg_LastEpoch_Improvements.sln -c Special`: успешно;
  единственное предупреждение — прежняя неиспользуемая `ex` в `RaresOnMap.cs`.
- Проверка JIT через `RuntimeHelpers.PrepareMethod` воспроизводит четыре
  `MissingMethodException` на сохранённой старой DLL: Bazaar Update,
  PickupAllItemsInRange, CreateCustomMapIcon и ShowItemOnMap.
- Те же четыре метода установленной новой DLL проходят подготовку JIT.
- Проверка Harmony-атрибутов старой DLL обнаруживает три отсутствующие цели
  из 14; в новой DLL все 13 целей однозначно существуют с заданными сигнатурами.
- Новая DLL в `Mods` совпадает со сборкой `Special` по SHA-256:
  `18A651D9B1324F73615999BC5CEC85E739E847940FB1D9ADBDECC175455E7B1B`.
- Временные `KG_ApiProbe.dll`, `.deps.json` и `.pdb` удалены из `Mods`.
- `git diff --check`: ошибок whitespace нет.

Оригинальная DLL мода сохранена рядом в `Mods`:

```text
kg_LastEpoch_Improvements.dll.before-api-20261003-172258-876.bak
```

CLI-инспектор и проверки сохранены в отмеченной выше временной папке.
Пример команды (в качестве аргумента передать путь к DLL мода):

```bat
dotnet "C:\Users\user\AppData\Local\Temp\opencode\le-api-probe-20261003\bin\Release\net6.0\KG_ApiProbe.dll" --jit "C:\Program Files (x86)\Steam\steamapps\common\Last Epoch\Mods\kg_LastEpoch_Improvements.dll"
```

Режим `--patches` вместо `--jit` проверяет цели Harmony по атрибутам.
Эти проверки не выполняют игровой код и не заменяют проверку в игре.
Следующий запуск должен проверить инициализацию мода, иконки и подписи лута,
подбор по F и поиск в Bazaar. UnityExplorer в ходе этой адаптации не обновлялся;
его отдельный сбой поиска сцен всё ещё требует собственной проверки/исправления.

## Нативный краш при включённом UnityExplorer

После установки адаптированной DLL модов Windows зарегистрировал повторяющиеся
сбои `Last Epoch.exe`: модуль `coreclr.dll` 6.0.2123.36311, код `0xc0000005`,
смещение `0x1d2699`. Отдельных Unity crash dump файлов не найдено.

Автоматический обычный запуск игры воспроизвёл краш и вывел аварийный стек:
`il2cpp_gchandle_get_target` → `Il2CppObjectBase.Pointer` →
`Il2CppSystem.Span<byte>..ctor` → `AssetBundle.LoadFromMemory_Internal` →
`UniverseLib.AssetBundle.LoadFromMemory` → `UniversalUI.LoadBundle`.
Без UnityExplorer та же сборка основного мода проходит 45 секунд запуска.

UnityExplorer обновлён официальным пакетом `UnityExplorer.MelonLoader.IL2CPP.CoreCLR.zip`
до 4.13.6, UniverseLib — до 1.6.2. Само обновление воспроизвело тот же краш.
Старые файлы сохранены:

```text
Mods/UnityExplorer.ML.IL2CPP.CoreCLR.dll.disabled-for-crash-check
UserLibs/UniverseLib.ML.IL2CPP.Interop.dll.before-explorer-4136-20261003-185054-178.bak
```

Добавлен и установлен отдельный
[`tools/UnityExplorerCompat`](../tools/UnityExplorerCompat/README.md).
Он обходит создание боксированного `Il2CppSystem.Span<byte>` и загружает
UI bundle через Unity 6 `_Injected` API с закреплённым массивом.

Результат проверки с включёнными основным модом и новым UnityExplorer:

- Startup-тест проходит 45 секунд без native access violation; окно — `Last Epoch`.
- Лог: `Unity 6000.4 bundle loading compatibility enabled.`
- Лог: `Loaded modern bundle for Unity 6000.4.8f1`.
- Лог: `UniverseLib 1.6.2 initialized.`
- Новых Windows событий 1000 с access violation за этот запуск нет.
- После bundle-загрузки остаётся отдельная managed-ошибка UnityExplorer:
  `Scene.handle`/`Scene.m_Handle` теперь `SceneHandle`, а старый код использует
  `int`. Fallback `SceneManager.GetAllScenes()` не работает.

Для повторения startup-проверки сохранён скрипт:

```bat
powershell -NoProfile -File "C:\Users\user\AppData\Local\Temp\opencode\LE-startup-check-20261003.ps1"
```

Он отказывается запускать второй экземпляр игры, проверяет код выхода
`0xC0000005` и сообщает, жив ли процесс через 45 секунд. Проверка не подтверждает
игровые функции после входа в персонажа или все функции UnityExplorer.

## 2026-10-04: интерфейс UnityExplorer восстановлен

Пользователь подтвердил, что F7 не открывал интерфейс. Новый лог показывал
обрыв `LateInit` из-за `SceneHandle` и неработающего `GetAllScenes` fallback.
Reflection актуальных bindings подтвердила:

- `Scene.handle` и `Scene.m_Handle` имеют тип `SceneHandle`.
- Handle содержит `SceneHandle.m_Value: EntityId`, а `EntityId.m_Data: int`.
- `Scene.GetNameInternal` принимает `SceneHandle`.

Добавлены [SceneApiCompat](../tools/SceneApiCompat/README.md) и
[RepairExplorerScenes](../tools/RepairExplorerScenes/README.md).
Патчер создаёт отдельные DLL, адаптируя семь getter-вызовов, две записи поля,
boxed аргумент reflection и fallback перечисления сцен.
Два старых метода UniverseLib для корневых объектов заменены современным API.

Проверки:

- Исходная UnityExplorer: JIT-проверка 69 методов выявляет пять отсутствующих
  getter/field обращений.
- Исправленная UnityExplorer: те же 69 методов проходят без ошибок.
- Исправленная UniverseLib: 12 проверенных методов проходят без ошибок.
- Обычный startup-тест: 45 секунд без access violation.
- В логе: `UnityExplorer 4.13.6 (IL2CPP) initialized.`
- Ошибки `SceneHandle`, `GetAllScenes` fallback и `onInitialized` исчезли.
- Пользователь подтвердил: **интерфейс появился после нажатия F7**.

Установлены исправленные Explorer/UniverseLib и `UserLibs/SceneApiCompat.dll`.
Оригиналы сохранены:

```text
Mods/UnityExplorer.ML.IL2CPP.CoreCLR.dll.before-scenes-20261004-185558-518.bak
UserLibs/UniverseLib.ML.IL2CPP.Interop.dll.before-scenes-20261004-185558-649.bak
```

`UnityExplorerCompat.dll` для native bundle-загрузки сохраняется в `Mods`.
Исправления локальные: переустановка Explorer/UniverseLib может их перезаписать.

## 2026-10-05: патч игры повторно создал дефектный CoreModule

Запуск `26-10-5_2-24-41.log` выполнил полную регенерацию interop-сборок.
Версии Unity (6000.4.8f1) и MelonLoader (0.7.3) прежние.
Генератор снова записал три глобальных `<>O`, после чего возникли
`BadImageFormatException` и `No Support Module Loaded!`.
Установленные DLL нашего мода, Explorer и локальные адаптеры сохранились.

После повторного применения девятибайтового обхода:

- CoreModule проходит отдельную загрузку на .NET 6.
- Все четыре проверенных метода нашего мода проходят JIT.
- Все 13 целей Harmony по-прежнему существуют.
- Compile-only проверка `Special` против новых bindings проходит.
- Проверки 69 методов Explorer и 12 методов UniverseLib проходят.

Добавлен и установлен
[`Plugins/AutoRepairUnityInterop.dll`](../tools/AutoRepairUnityInterop/README.md).
Он применяет общий `CoreModuleRepair` в `OnPreModsLoaded`, после генерации
и перед загрузкой модов. Порядок сверён с исходником `MelonLoader/Core.cs` v0.7.3.

Для проверки автоматизации восстановлена заведомо дефектная DLL из резервной
копии. Отдельная проверка до запуска снова падает на `Duplicate type '<>O'`.
Затем обычный запуск автоматически исправляет файл:

- `Unity CoreModule metadata checked before mod loading.`
- `Support Module Loaded`.
- `UnityExplorer 4.13.6 (IL2CPP) initialized.`
- 45-секундный startup-тест без access violation.
- Отдельная загрузка CoreModule после запуска — PASS.
- В новом логе нет ERROR; осталось одно прежнее предупреждение кэша типов UIElements.

Святилища: пользователь подтвердил, что значок есть, пропала только подпись.
Настройка `Show Shrines On Map` включена. Для дальнейшего исправления ещё нужен
runtime-результат предложенного C# Console запроса компонентов
`DisplayInformation`/`BaseDisplayInformation` и локализованного описания.

## 2026-10-07: маркеры святилищ и редких противников

Runtime-запрос пользователя подтвердил, что описания святилищ существуют
в `BaseDisplayInformation`, включая `GetLocalizedDescription()`.
Ручная попытка создания маркера падала с NullReferenceException.
Проверка клона UI-шаблона дала:

```text
processor=False image=True children=2 mapRect=True
text=True
```

Основная причина — Unity не сохраняет наш injected `CustomIconProcessor`
при native-клонировании шаблона. Шаблон теперь содержит только UI;
`AddIconProcessor()` явно добавляет новый обработчик к каждой копии.
Это используется для лута, святилищ и редких противников.

Святилища:

- Получение описания через дочерний `BaseDisplayInformation` и локализацию.
- Создание после `PlaceClientShrine`/`SetShrineObject` с ожиданием объекта,
  карты и текста, дедупликацией ожидающих запросов.
- Временная неактивность визуального объекта не удаляет маркер.
- Клиентское использование святилища отменяет ожидание и удаляет маркер.
- Пользователь подтвердил: **подписи и дальняя видимость святилищ работают**.

Редкие противники:

- Старый `ReceiveInitDisplayInformation` немедленно обращался к
  `actorVisuals.gameObject`; лог показывал `<null visuals>` и Stage: 3.
- Новый обработчик ожидает появления `actorVisuals` и готовности карты до
  создания GameObject маркера. Ожидание ограничено 15 секундами и прекращается
  для уничтоженного/мёртвого actor либо выключенной настройки.
- Ошибка создания удаляет незавершённый маркер и записывает полный exception.
- Пользователь подтвердил: **ошибок нет, маркеры есть**.
- Дополнительная выборка последнего лога: ноль ERROR/Stage: 3 строк.

Установленная Special-сборка:
`59A690AF6F1B0C16F0938DD994AAE19C882A2EEEFBB61CDB4A2B7C3D67908978`.
Сборка без ошибок/предупреждений; все 15 целей Harmony существуют.
Оригинал перед последним исправлением сохранён:

```text
Mods/kg_LastEpoch_Improvements.dll.before-api-20261007-131957-932.bak
```

Последующее ревью усилило жизненный цикл маркеров: ожидание редких visuals
ограничено жизнью actor, а не таймером; добавлены ownership/deduplication,
удаление по смерти и безопасная отмена по generation ID. См.
[`review-2026-10-07.md`](review-2026-10-07.md).
