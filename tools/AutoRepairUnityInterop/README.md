# AutoRepairUnityInterop

MelonLoader-плагин, повторно применяющий известный обход дублирующихся
типов `<>O` после регенерации игровых сборок.

Установка при закрытой игре:

```bat
dotnet build tools/AutoRepairUnityInterop/AutoRepairUnityInterop.csproj -c Release -o "C:\Program Files (x86)\Steam\steamapps\common\Last Epoch\Plugins"
```

Плагин устанавливается в **Plugins**, поскольку обычные Mods уже не могут
загрузиться с дефектной `UnityEngine.CoreModule.dll`.
`OnPreModsLoaded` вызывается в MelonLoader 0.7.3 после `Il2CppAssemblyGenerator.Run`
и до загрузки модов. Плагин не ссылается на игровые или Unity DLL и читает
метаданные напрямую через `System.Reflection.Metadata`.

Используется тот же `CoreModuleRepair`, что и в CLI
[RepairUnityInterop](../RepairUnityInterop/README.md). Если дубликатов нет,
файлы не изменяются. При известном дефекте меняются только девять байт;
перед заменой сохраняется резервная копия. Неожиданная структура отвергается.

Проверка на 2026-10-05: перед запуском восстановлена заведомо дефектная DLL,
которая воспроизводит `Duplicate type '<>O'`. Во время обычного запуска плагин
автоматически исправил файл, после чего загрузились support module и
UnityExplorer. Исправленная DLL проходит отдельную проверку загрузки .NET 6.

Этот плагин устраняет только данный дефект генерации, а не любые изменения
игровых API в будущих патчах.
