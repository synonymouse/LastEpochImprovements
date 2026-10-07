# SceneApiCompat

Минимальный bridge между прежними целочисленными сценовыми handles
UnityExplorer и новыми `SceneHandle`/`EntityId` в Unity 6000.4.
Используется бинарным патчером [RepairExplorerScenes](../RepairExplorerScenes/README.md).

Собрать через `dotnet build tools/SceneApiCompat/SceneApiCompat.csproj -c Release`.
Итоговый `bin/Release/net6.0/SceneApiCompat.dll` устанавливается в `UserLibs`
рядом с исправленной UniverseLib. Основное решение мода не затрагивается.
