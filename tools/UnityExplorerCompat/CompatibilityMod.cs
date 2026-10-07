using System.Reflection;
using System.Runtime.InteropServices;
using HarmonyLib;
using Il2CppInterop.Runtime;
using MelonLoader;

[assembly: MelonInfo(typeof(UnityExplorerCompat.CompatibilityMod), "KG.UnityExplorerCompat", "0.0.1", "Local compatibility")]
[assembly: MelonPriority(-1000)]

namespace UnityExplorerCompat;

public sealed class CompatibilityMod : MelonMod
{
    [StructLayout(LayoutKind.Sequential)]
    private unsafe struct NativeSpan
    {
        public byte* Begin;
        public int Length;
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr LoadFromMemoryInjected(ref NativeSpan bytes, uint crc);

    private static LoadFromMemoryInjected _loadFromMemory;

    public override void OnInitializeMelon()
    {
        if (IntPtr.Size != 8 || !UnityEngine.Application.unityVersion.StartsWith("6000.4.", StringComparison.Ordinal))
            return;

        _loadFromMemory = IL2CPP.ResolveICall<LoadFromMemoryInjected>(
            "UnityEngine.AssetBundle::LoadFromMemory_Internal_Injected");
        var original = typeof(UniverseLib.AssetBundle).GetMethod("LoadFromMemory",
            BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(byte[]), typeof(uint) }, null)
            ?? throw new MissingMethodException("UniverseLib.AssetBundle.LoadFromMemory(byte[], uint)");
        var prefix = typeof(CompatibilityMod).GetMethod(nameof(LoadBundle), BindingFlags.NonPublic | BindingFlags.Static)!;
        HarmonyInstance.Patch(original, prefix: new HarmonyMethod(prefix));
        MelonLogger.Msg("Unity 6000.4 bundle loading compatibility enabled.");
    }

    // Unity 6's native binding consumes a pointer/length span and returns an
    // IL2CPP GC handle. Do not create a boxed Il2CppSystem.Span<byte>: the legacy
    // memory-load shim crashes inside that wrapper on this game build.
    private static unsafe bool LoadBundle(byte[] binary, uint crc, ref UniverseLib.AssetBundle __result)
    {
        ArgumentNullException.ThrowIfNull(binary);
        if (_loadFromMemory == null) throw new InvalidOperationException("Bundle binding is not initialized.");
        fixed (byte* bytes = binary)
        {
            var span = new NativeSpan { Begin = bytes, Length = binary.Length };
            var handle = _loadFromMemory(ref span, crc);
            var pointer = handle == IntPtr.Zero ? IntPtr.Zero : IL2CPP.il2cpp_gchandle_get_target(handle);
            __result = pointer == IntPtr.Zero ? null : new UniverseLib.AssetBundle(pointer);
        }
        return false;
    }
}
