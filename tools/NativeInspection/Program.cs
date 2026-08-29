using AssetRipper.Primitives;
using Iced.Intel;
using LibCpp2IL;

// Read-only, offline inspection: never loads GameAssembly.dll for execution and
// never attaches to or modifies the game process. Output goes only to stdout.
if (args.Length < 2)
    throw new ArgumentException("Usage: NativeInspection <game folder> <Unity version> [Type.Method ...]");

var output = Console.Out;
using (var initializationLog = new StringWriter())
{
    try
    {
        Console.SetOut(initializationLog);
        if (!LibCpp2IlMain.LoadFromFile(Path.Combine(args[0], "GameAssembly.dll"),
                Path.Combine(args[0], "Supermarket Simulator_Data", "il2cpp_data", "Metadata", "global-metadata.dat"),
                UnityVersion.Parse(args[1])))
            throw new InvalidOperationException("Cannot initialize native metadata inspection.");
    }
    catch
    {
        output.WriteLine(initializationLog.ToString());
        throw;
    }
    finally
    {
        Console.SetOut(output);
    }
}

var requested = args.Skip(2).ToHashSet();
if (requested.Count == 0)
{
    requested.UnionWith(new[] { "EmployeeManager.HireRestocker", "EmployeeManager.SpawnRestocker",
        "EmployeeGenerator.SpawnRestocker", "IDManager.RestockerSO" });
}

var methods = LibCpp2IlMain.TheMetadata!.methodDefs;
var boundaries = methods.Select(m => m.MethodPointer).Where(p => p != 0).Distinct().OrderBy(p => p).ToArray();
var binary = LibCpp2IlMain.Binary!;
var content = binary.GetRawBinaryContent();

foreach (var method in methods.Where(m => requested.Contains(m.DeclaringType?.FullName + "." + m.Name)))
{
    var start = method.MethodPointer;
    if (start == 0)
        continue;

    var boundary = Array.BinarySearch(boundaries, start) + 1;
    var end = boundary < boundaries.Length ? Math.Min(boundaries[boundary], start + 8192) : start + 8192;
    var bytes = content.AsSpan((int)method.MethodOffsetInFile, (int)(end - start)).ToArray();
    var decoder = Decoder.Create(64, new ByteArrayCodeReader(bytes));
    decoder.IP = start;
    Console.WriteLine($"\n{method.HumanReadableSignature} RVA=0x{method.Rva:X} VA=0x{start:X}");

    while (decoder.IP < end)
    {
        decoder.Decode(out var instruction);
        var annotations = new List<string>();

        if (instruction.IsCallNear || instruction.IsJmpNear)
        {
            var targets = LibCpp2IlMain.GetManagedMethodImplementationsAtAddress(instruction.NearBranchTarget);
            if (targets != null)
                annotations.AddRange(targets.Take(4).Select(m => m.DeclaringType?.FullName + "." + m.Name));
        }

        if (instruction.IsIPRelativeMemoryOperand)
        {
            try
            {
                var global = LibCpp2IlMain.GetAnyGlobalByAddress(instruction.IPRelativeMemoryAddress);
                if (global != null)
                    annotations.Add(global.ToString() ?? "");
            }
            catch (Exception)
            {
                // Not every RIP-relative load refers to managed metadata.
            }
        }

        Console.WriteLine($"{instruction.IP:X}: {instruction} {(annotations.Count == 0 ? "" : "; " + string.Join(" / ", annotations))}");
    }
}
