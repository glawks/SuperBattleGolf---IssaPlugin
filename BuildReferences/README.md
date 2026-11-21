# Compile-only API references

`sbg-1.4.0-798-api.zip` contains generated, non-executable reference assemblies for
reproducible development builds without committing the supplied runtime game DLLs.
The inputs are the supplied 1.4.0-798 managed assemblies, BepInEx 5.4.23.5,
HarmonyX 2.9.0.0 and ModConfig 0.1.3. Assembly identities and signatures are retained.
All method implementations, embedded resources, symbols and static data blobs are
removed. Each assembly is marked ReferenceAssembly and must never be installed in
the game. No reference DLL is included in the development artifact.

Generate with `dotnet run --project BuildTools/ReferenceGenerator -- Assembly output.zip`.
The archive includes input and reference SHA-256 hashes in `provenance.json`.
The plugin can also be built against the actual DLLs in Assembly/ using
`dotnet build -c Release -p:UseInstalledReferences=true`.
