using System.Buffers.Binary;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;

namespace LastEpochInteropRepair;

public static class CoreModuleRepair
{
    public static int Run(string[] args)
    {
        if (args.Length != 2 || args[0] is "--help" or "-h")
        {
            Console.Error.WriteLine("Usage: RepairUnityInterop <input-dll> <output-file | --apply>");
            return 2;
        }

        var input = Path.GetFullPath(args[0]);
        var apply = args[1] == "--apply";
        var output = apply ? input + ".interop-repair-" + Guid.NewGuid().ToString("N") + ".pending" : Path.GetFullPath(args[1]);
        if (string.Equals(input, output, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Use a separate output path, or --apply to create a backup and replace the DLL.");

        var original = File.ReadAllBytes(input);
        var patched = (byte[])original.Clone();
        var allowedChanges = new HashSet<int>();
        using var pe = new PEReader(new MemoryStream(original));
        var reader = pe.GetMetadataReader();
        string FullName(TypeDefinitionHandle handle)
        {
            var type = reader.GetTypeDefinition(handle);
            var parent = type.GetDeclaringType();
            var name = reader.GetString(type.Name);
            if (!parent.IsNil) return FullName(parent) + "+" + name;
            var ns = reader.GetString(type.Namespace);
            return ns.Length == 0 ? name : ns + "." + name;
        }

        var groups = reader.TypeDefinitions.GroupBy(FullName).Where(group => group.Count() > 1).ToList();
        if (groups.Count == 0)
        {
            Console.WriteLine("No duplicate full type names found; no files changed.");
            return 0;
        }
        if (reader.GetString(reader.GetAssemblyDefinition().Name) != "UnityEngine.CoreModule" ||
            groups.Count != 1 || groups[0].Key != "<>O" || groups[0].Count() != 3)
            throw new InvalidOperationException("This tool only handles the verified three-orphan <>O bug.");
        if (pe.PEHeaders.CorHeader!.StrongNameSignatureDirectory.Size != 0)
            throw new InvalidOperationException("Unexpected signed assembly.");

        var broken = groups[0].ToList();
        foreach (var handle in broken)
        {
            var type = reader.GetTypeDefinition(handle);
            if (!type.GetDeclaringType().IsNil || reader.GetString(type.Namespace).Length != 0 ||
                (type.Attributes & TypeAttributes.VisibilityMask) != TypeAttributes.NestedPublic ||
                type.GetFields().Count != 0 || type.GetMethods().Count != 0 ||
                type.GetProperties().Count != 0 || type.GetEvents().Count != 0 ||
                type.GetNestedTypes().Length != 0 || type.GetGenericParameters().Count != 0 ||
                type.GetInterfaceImplementations().Count != 0 || type.GetCustomAttributes().Count != 0)
                throw new InvalidOperationException("Unexpected data in an orphan type; refusing this workaround.");
        }

        // Reuse existing namespace strings instead of resizing the metadata heaps.
        // These empty orphan types have no behavior. Give them distinct full names and
        // correct their invalid nested visibility, preserving every row/token and RVA.
        var namespaces = new[] { "", "UnityEngine", "UnityEngine.Rendering" };
        var namespaceHandles = namespaces.Select(ns => ns.Length == 0 ? default(StringHandle) :
            reader.TypeDefinitions.Select(handle => reader.GetTypeDefinition(handle).Namespace)
                .First(handle => reader.GetString(handle) == ns)).ToArray();
        var occupied = reader.TypeDefinitions.Where(handle => !broken.Contains(handle)).Select(FullName).ToHashSet();
        if (namespaces.Any(ns => occupied.Contains(ns.Length == 0 ? "<>O" : ns + ".<>O")))
            throw new InvalidOperationException("A replacement full name is already occupied.");

        var metadataStart = pe.PEHeaders.MetadataStartOffset;
        var tableStart = metadataStart + reader.GetTableMetadataOffset(TableIndex.TypeDef);
        var rowSize = reader.GetTableRowSize(TableIndex.TypeDef);
        var stringIndexSize = reader.GetHeapSize(HeapIndex.String) > ushort.MaxValue ? 4 : 2;
        for (var index = 0; index < broken.Count; index++)
        {
            var handle = broken[index];
            var type = reader.GetTypeDefinition(handle);
            var offset = tableStart + (MetadataTokens.GetRowNumber(handle) - 1) * rowSize;
            if (BinaryPrimitives.ReadUInt32LittleEndian(original.AsSpan(offset, 4)) != (uint)type.Attributes ||
                ReadIndex(original, offset + 4, stringIndexSize) != MetadataTokens.GetHeapOffset(type.Name) ||
                ReadIndex(original, offset + 4 + stringIndexSize, stringIndexSize) != MetadataTokens.GetHeapOffset(type.Namespace))
                throw new InvalidOperationException("Unexpected TypeDef layout.");

            var attributes = type.Attributes & ~TypeAttributes.VisibilityMask;
            BinaryPrimitives.WriteUInt32LittleEndian(patched.AsSpan(offset, 4), (uint)attributes);
            var namespaceOffset = offset + 4 + stringIndexSize;
            var heapOffset = MetadataTokens.GetHeapOffset(namespaceHandles[index]);
            if (stringIndexSize == 4)
                BinaryPrimitives.WriteInt32LittleEndian(patched.AsSpan(namespaceOffset, 4), heapOffset);
            else
                BinaryPrimitives.WriteUInt16LittleEndian(patched.AsSpan(namespaceOffset, 2), checked((ushort)heapOffset));
            for (var pos = offset; pos < offset + 4; pos++) allowedChanges.Add(pos);
            for (var pos = namespaceOffset; pos < namespaceOffset + stringIndexSize; pos++) allowedChanges.Add(pos);
            Console.WriteLine($"TypeDef 0x{MetadataTokens.GetToken(handle):X8}: {(index == 0 ? "<>O" : namespaces[index] + ".<>O")} (NotPublic)");
        }

        using (var verification = new PEReader(new MemoryStream(patched)))
        {
            var verified = verification.GetMetadataReader();
            foreach (var handle in reader.TypeDefinitions)
            {
                var before = reader.GetTypeDefinition(handle);
                var after = verified.GetTypeDefinition(handle);
                var index = broken.IndexOf(handle);
                if (before.Name != after.Name || before.BaseType != after.BaseType ||
                    !before.GetFields().SequenceEqual(after.GetFields()) || !before.GetMethods().SequenceEqual(after.GetMethods()) ||
                    (index < 0 && (before.Namespace != after.Namespace || before.Attributes != after.Attributes)) ||
                    (index >= 0 && (after.Namespace != namespaceHandles[index] ||
                        after.Attributes != (before.Attributes & ~TypeAttributes.VisibilityMask))))
                    throw new InvalidOperationException("TypeDef verification failed.");
            }
        }
        var changedBytes = 0;
        for (var pos = 0; pos < original.Length; pos++)
        {
            if (original[pos] == patched[pos]) continue;
            if (!allowedChanges.Contains(pos)) throw new InvalidOperationException("Bytes outside the intended metadata changed.");
            changedBytes++;
        }
        Console.WriteLine($"PASS: {changedBytes} metadata bytes changed; all tokens, heaps, MVID, IL and native caches preserved.");

        if (File.Exists(output)) throw new IOException($"Output already exists: {output}");
        try
        {
            using (var stream = new FileStream(output, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                stream.Write(patched);
            if (apply)
            {
                if (!File.ReadAllBytes(input).SequenceEqual(original))
                    throw new IOException("The input DLL changed during repair; replacement cancelled.");
                var backup = input + ".interop-repair-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff") + ".bak";
                File.Replace(output, input, backup);
                Console.WriteLine($"Installed: {input}");
                Console.WriteLine($"Backup: {backup}");
            }
            else Console.WriteLine($"Output: {output}");
        }
        finally
        {
            if (apply && File.Exists(output)) File.Delete(output);
        }
        return 0;

        static int ReadIndex(byte[] bytes, int offset, int size) => size == 4
            ? BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(offset, 4))
            : BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset, 2));
    }
}
