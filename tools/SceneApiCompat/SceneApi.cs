using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SceneApiCompat;

public static class SceneApi
{
    public static int ToInt32(SceneHandle handle) => handle.m_Value.m_Data;

    public static SceneHandle FromInt32(int handle) => new()
    {
        m_Value = new EntityId { m_Data = handle }
    };

    public static object BoxHandle(int handle) => FromInt32(handle);

    public static Il2CppStructArray<Scene> GetAllScenes()
    {
        var scenes = new Il2CppStructArray<Scene>(SceneManager.sceneCount);
        for (int index = 0; index < scenes.Length; index++) scenes[index] = SceneManager.GetSceneAt(index);
        return scenes;
    }

    public static GameObject[] GetRootGameObjects(Scene scene)
    {
        var roots = scene.GetRootGameObjects();
        var result = new GameObject[roots.Length];
        for (int index = 0; index < result.Length; index++) result[index] = roots[index];
        return result;
    }

    public static int GetRootCount(Scene scene) => scene.rootCount;
}
