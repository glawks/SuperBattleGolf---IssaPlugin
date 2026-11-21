using Mono.Cecil;
using Mono.Cecil.Cil;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;

if (args.Length != 2) throw new ArgumentException("input DLL directory, output ZIP required");
string input = Path.GetFullPath(args[0]);
string output = Path.GetFullPath(args[1]);
var resolver = new DefaultAssemblyResolver();
resolver.AddSearchDirectory(input);
var provenance = new List<object>();
using var zip = ZipFile.Open(output, ZipArchiveMode.Create);
foreach (var path in Directory.GetFiles(input, "*.dll").OrderBy(p => p))
{
    string name = Path.GetFileName(path);
    if (name.StartsWith("System.") || new[] { "System.dll", "mscorlib.dll", "netstandard.dll", "Mono.Security.dll", "0Harmony20.dll" }.Contains(name)) continue;
    using var assembly = AssemblyDefinition.ReadAssembly(path, new ReaderParameters { AssemblyResolver = resolver });
    var module = assembly.MainModule;
    // Metadata signatures only. No game implementation, embedded assets, PDBs or
    // static data blobs. Explicit ReferenceAssembly marker prevents runtime use.
    module.Resources.Clear();
    foreach (var type in AllTypes(module.Types))
    {
        foreach (var field in type.Fields)
            if (field.HasFieldRVA) { field.InitialValue = Array.Empty<byte>(); field.Attributes &= ~FieldAttributes.HasFieldRVA; }
        foreach (var method in type.Methods)
        {
            if (!method.HasBody) continue;
            method.Body = new MethodBody(method);
            method.Body.Instructions.Add(Instruction.Create(OpCodes.Ldnull));
            method.Body.Instructions.Add(Instruction.Create(OpCodes.Throw));
            method.DebugInformation.SequencePoints.Clear();
            method.DebugInformation.Scope = null;
        }
    }
    if (!assembly.CustomAttributes.Any(a => a.AttributeType.FullName == "System.Runtime.CompilerServices.ReferenceAssemblyAttribute"))
    {
        var marker = new TypeReference("System.Runtime.CompilerServices", "ReferenceAssemblyAttribute", module, module.TypeSystem.CoreLibrary);
        var ctor = new MethodReference(".ctor", module.TypeSystem.Void, marker) { HasThis = true };
        assembly.CustomAttributes.Add(new CustomAttribute(ctor));
    }
    using var bytes = new MemoryStream();
    assembly.Write(bytes);
    var entry = zip.CreateEntry(name, CompressionLevel.Optimal);
    using (var stream = entry.Open()) { bytes.Position = 0; bytes.CopyTo(stream); }
    provenance.Add(new { file = name, sourceSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))), referenceSha256 = Convert.ToHexString(SHA256.HashData(bytes.ToArray())) });
}
using (var writer = new StreamWriter(zip.CreateEntry("provenance.json").Open()))
    writer.Write(JsonSerializer.Serialize(provenance, new JsonSerializerOptions { WriteIndented = true }));

static IEnumerable<TypeDefinition> AllTypes(IEnumerable<TypeDefinition> types)
{
    foreach (var type in types)
    {
        yield return type;
        foreach (var nested in AllTypes(type.NestedTypes)) yield return nested;
    }
}
