# RepairExplorerScenes

Адаптация бинарных UnityExplorer 4.13.6 и UniverseLib 1.6.2 к сценовому API
Unity 6000.4.8f1 в Last Epoch. Используется вместе с `SceneApiCompat`.

Из корня репозитория сначала собрать bridge:

```bat
dotnet build tools/SceneApiCompat/SceneApiCompat.csproj -c Release
```

CLI патчера:

```text
RepairExplorerScenes <input-dll>
RepairExplorerScenes <input-dll> <new-output-dll> <SceneApiCompat.dll>
```

Первый режим выводит фактические обращения к сценовому API для диагностики.
Второй создаёт отдельную исправленную DLL; исходник не перезаписывается.
Патчер проверяет ожидаемую форму изменений, рассчитанную на указанные версии.

Изменения UnityExplorer:

- Семь старых `Scene.get_handle(): int` заменены новым getter и конвертацией
  `SceneHandle` в прежнее целочисленное представление.
- Две записи в `Scene.m_Handle` используют конвертацию `int` → `SceneHandle`.
- Reflection-вызов `GetNameInternal` получает boxed `SceneHandle`, а не `int`.
- Fallback `GetAllScenes()` заменён перечислением `sceneCount/GetSceneAt`.

В UniverseLib два метода работы с корневыми объектами сцен заменены
современными вызовами `Scene.GetRootGameObjects()` и `Scene.rootCount`.
В bridge используются фактические поля `SceneHandle.m_Value` и `EntityId.m_Data`.

Для установки с закрытой игрой сохранить оригиналы, заменить обе DLL
исправленными копиями и поместить `SceneApiCompat.dll` в `UserLibs`.
Это отдельная библиотека без MelonMod, которая не зависит от UnityExplorer
или UniverseLib и не создаёт циклических зависимостей.

Проверенная установка завершает `UnityExplorer ... initialized`;
пользователь подтвердил открытие интерфейса по F7.
Native bundle compatibility из `UnityExplorerCompat` также остаётся необходимой.
