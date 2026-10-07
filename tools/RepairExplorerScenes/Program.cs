using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;

if (args.Length is not (1 or 3))
    throw new ArgumentException("Usage: RepairExplorerScenes <input> [<output> <SceneApiCompat.dll>]");
var root = @"C:\Program Files (x86)\Steam\steamapps\common\Last Epoch";
using var resolver = new DefaultAssemblyResolver();
resolver.AddSearchDirectory(Path.Combine(root, @"MelonLoader\Il2CppAssemblies"));
resolver.AddSearchDirectory(Path.Combine(root, @"MelonLoader\net6"));
resolver.AddSearchDirectory(Path.Combine(root, "UserLibs"));
using var assembly = AssemblyDefinition.ReadAssembly(args[0], new ReaderParameters { AssemblyResolver = resolver, InMemory = true });
if (args.Length == 1)
{
    foreach (var type in assembly.MainModule.GetTypes())
    foreach (var method in type.Methods.Where(method => method.HasBody))
    foreach (var instruction in method.Body.Instructions)
        if (instruction.Operand is MemberReference member && member.DeclaringType?.FullName.StartsWith("UnityEngine.SceneManagement.", StringComparison.Ordinal) == true)
            Console.WriteLine(method.FullName + ": " + instruction.OpCode + " " + member.FullName);
    return;
}

if (assembly.Name.Name is not ("UnityExplorer.ML.IL2CPP.CoreCLR" or "UniverseLib.ML.IL2CPP.Interop"))
    throw new InvalidOperationException("Unexpected target assembly.");
if (Path.GetFullPath(args[0]) == Path.GetFullPath(args[1]) || File.Exists(args[1]))
    throw new IOException("Use a new output file; preserve the input.");
using var bridge = AssemblyDefinition.ReadAssembly(args[2], new ReaderParameters { AssemblyResolver = resolver });
using var core = AssemblyDefinition.ReadAssembly(Path.Combine(root, @"MelonLoader\Il2CppAssemblies\UnityEngine.CoreModule.dll"));
var scene = core.MainModule.GetType("UnityEngine.SceneManagement.Scene");
var api = bridge.MainModule.GetType("SceneApiCompat.SceneApi");
var module = assembly.MainModule;
var getter = module.ImportReference(scene.Methods.Single(method => method.Name == "get_handle"));
var field = module.ImportReference(scene.Fields.Single(field => field.Name == "m_Handle"));
MethodReference Import(string name) => module.ImportReference(api.Methods.Single(method => method.Name == name));
var toInt = Import("ToInt32");
var fromInt = Import("FromInt32");
var box = Import("BoxHandle");
int getters = 0, fields = 0, boxed = 0, roots = 0, fallback = 0;

foreach (var type in module.GetTypes())
foreach (var method in type.Methods.Where(method => method.HasBody))
{
    if (type.FullName == "UniverseLib.Runtime.Il2Cpp.Il2CppProvider" &&
        method.Name is "Internal_GetRootGameObjects" or "Internal_GetRootCount")
    {
        method.Body = new MethodBody(method);
        var body = method.Body.GetILProcessor();
        body.Emit(method.HasThis ? OpCodes.Ldarg_1 : OpCodes.Ldarg_0);
        body.Emit(OpCodes.Call, Import(method.Name == "Internal_GetRootCount" ? "GetRootCount" : "GetRootGameObjects"));
        body.Emit(OpCodes.Ret);
        roots++;
        continue;
    }
    method.Body.SimplifyMacros();
    var il = method.Body.GetILProcessor();
    foreach (var instruction in method.Body.Instructions.ToList())
    {
        if (instruction.Operand is MethodReference target && target.DeclaringType.FullName == "UnityEngine.SceneManagement.Scene" &&
            target.Name == "get_handle" && target.ReturnType.FullName == "System.Int32")
        {
            instruction.Operand = getter;
            il.InsertAfter(instruction, il.Create(OpCodes.Call, toInt));
            getters++;
        }
        else if (instruction.Operand is FieldReference targetField && targetField.DeclaringType.FullName == "UnityEngine.SceneManagement.Scene" &&
            targetField.Name == "m_Handle" && targetField.FieldType.FullName == "System.Int32")
        {
            instruction.Operand = field;
            if (instruction.OpCode == OpCodes.Stfld)
                il.InsertBefore(instruction, il.Create(OpCodes.Call, fromInt));
            else if (instruction.OpCode == OpCodes.Ldfld)
                il.InsertAfter(instruction, il.Create(OpCodes.Call, toInt));
            else throw new InvalidOperationException("Unexpected scene field access: " + instruction.OpCode);
            fields++;
        }
        else if (type.FullName == "UnityExplorer.ObjectExplorer.SceneHandler" && method.Name == "Init" &&
            instruction.OpCode == OpCodes.Box && instruction.Operand is TypeReference boxedType && boxedType.FullName == "System.Int32" &&
            instruction.Previous.OpCode == OpCodes.Ldc_I4 && Equals(instruction.Previous.Operand, -12))
        {
            instruction.OpCode = OpCodes.Call;
            instruction.Operand = box;
            boxed++;
        }
        else if (instruction.Operand is MethodReference manager && manager.DeclaringType.FullName == "UnityEngine.SceneManagement.SceneManager" &&
            manager.Name == "GetAllScenes")
        {
            instruction.Operand = Import("GetAllScenes");
            fallback++;
        }
    }
    method.Body.OptimizeMacros();
}
if (assembly.Name.Name == "UnityExplorer.ML.IL2CPP.CoreCLR" && (getters != 7 || fields != 2 || boxed != 1 || fallback != 1))
    throw new InvalidOperationException($"Unexpected Explorer patch shape: getters={getters}, fields={fields}, boxed={boxed}, fallback={fallback}");
if (assembly.Name.Name == "UniverseLib.ML.IL2CPP.Interop" && roots != 2)
    throw new InvalidOperationException("Unexpected UniverseLib root-object API.");
assembly.Write(args[1]);
Console.WriteLine($"Patched {assembly.Name.Name}: getters={getters}, fields={fields}, boxed handles={boxed}, scene fallback={fallback}, root methods={roots}");
