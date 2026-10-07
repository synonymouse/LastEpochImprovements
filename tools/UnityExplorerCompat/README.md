# UnityExplorerCompat — Last Epoch / Unity 6000.4

MelonLoader-мод совместимости для нативного краша при загрузке UI bundle
UnityExplorer. Проверен с MelonLoader 0.7.3, UnityExplorer 4.13.6,
UniverseLib 1.6.2 и Last Epoch на Unity 6000.4.8f1, Windows x64.

## Исправляемый сбой

```text
System.AccessViolationException (0xC0000005)
  Il2CppInterop.Runtime.IL2CPP.il2cpp_gchandle_get_target
  Il2CppSystem.Span<byte>..ctor
  UnityEngine.AssetBundle.LoadFromMemory_Internal
  UniverseLib.AssetBundle.LoadFromMemory
  UniverseLib.UI.UniversalUI.LoadBundle
```

Обновление UnityExplorer с 4.13.5 до 4.13.6 само по себе не убрало этот
краш на исследованной установке. Отключение только UnityExplorer позволяет
пройти запуск с включённым модом улучшений.

Адаптер заменяет `UniverseLib.AssetBundle.LoadFromMemory(byte[], uint)`
через Harmony prefix. Он закрепляет управляемый массив на время синхронного
вызова и передаёт нативный span (pointer + length) в
`UnityEngine.AssetBundle::LoadFromMemory_Internal_Injected`.
Возвращаемый IL2CPP GC handle преобразуется в указатель объекта.
Боксированный `Il2CppSystem.Span<byte>` и старый memory-load shim не вызываются.

Сигнатура и формат возврата сверены с официальным исходником MelonLoader
0.7.3: `UnityUtilities/UnityEngine.Il2CppAssetBundleManager/Il2CppAssetBundleManager.cs`.
Адаптер применяется только на x64 и Unity `6000.4.*`.

## Сборка и установка

При закрытой игре из корня репозитория:

```bat
dotnet build tools/UnityExplorerCompat/UnityExplorerCompat.csproj -c Release -o "C:\Program Files (x86)\Steam\steamapps\common\Last Epoch\Mods"
```

DLL зависимостей не копируются: используются установленные MelonLoader,
UniverseLib и сгенерированные игровые сборки.
Проект не включён в решение основного мода.

Сообщение успешного включения адаптера:

```text
Unity 6000.4 bundle loading compatibility enabled.
```

В проверенном запуске после него появились `Loaded modern bundle` и
`UniverseLib 1.6.2 initialized`; нативного краша в 45-секундном startup-тесте нет.

## Сценовый API UnityExplorer

Этот адаптер исправляет загрузку bundle. UnityExplorer 4.13.6 всё ещё использует
старые `int` scene handles: в новых bindings `Scene.handle` и `Scene.m_Handle`
имеют тип `SceneHandle`. Поиск DontDestroyOnLoad при инициализации выбрасывает
ошибку преобразования `int` в `SceneHandle`, а fallback `GetAllScenes()`
не unstripped и выбрасывает `NotSupportedException`.
Этот дефект отдельно исправлен инструментами
[RepairExplorerScenes](../RepairExplorerScenes/README.md) и
[SceneApiCompat](../SceneApiCompat/README.md).
После применения обоих исправлений UnityExplorer полностью инициализируется;
пользователь подтвердил открытие интерфейса по F7.
