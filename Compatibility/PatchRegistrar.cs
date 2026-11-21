using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;

namespace IssaPlugin.Compatibility
{
    internal static class PatchRegistrar
    {
        // Keep the original Harmony owner so UnpatchSelf and other mod tooling work.
        // A class can have several targets: roll back its newly installed methods if
        // a later target fails, without removing successful classes or other mods.
        internal static int Apply(Harmony harmony, Assembly assembly,
            Action<string> info, Action<string> error)
        {
            Type[] types;
            int failures = 0;
            try { types = assembly.GetTypes(); }
            catch (ReflectionTypeLoadException ex)
            {
                types = ex.Types.Where(t => t != null).ToArray();
                failures++;
                foreach (var loaderError in ex.LoaderExceptions)
                    error($"[Compatibility] Type discovery: {loaderError}");
            }

            int applied = 0;
            foreach (var type in types)
            {
                try
                {
                    // Do not process unannotated optional integration types.
                    if (!type.IsDefined(typeof(HarmonyPatch), false)) continue;
                    var before = OwnedPatches(harmony);
                    try
                    {
                        harmony.CreateClassProcessor(type).Patch();
                        applied++;
                    }
                    catch
                    {
                        foreach (var patch in OwnedPatches(harmony).Except(before))
                            harmony.Unpatch(patch.Original, patch.Patch);
                        throw;
                    }
                }
                catch (Exception ex)
                {
                    failures++;
                    error($"[Compatibility] Patch class {type.FullName} failed: {ex}");
                }
            }
            info($"[Compatibility] Harmony registration: {applied} classes processed, {failures} failures.");
            if (failures != 0)
                error("[Compatibility] Startup will continue, but affected features may be unavailable. Include the failures above in test reports.");
            return failures;
        }

        private static HashSet<(MethodBase Original, MethodInfo Patch)> OwnedPatches(Harmony harmony)
        {
            var result = new HashSet<(MethodBase, MethodInfo)>();
            foreach (var original in harmony.GetPatchedMethods())
            {
                var patches = Harmony.GetPatchInfo(original);
                if (patches == null) continue;
                foreach (var patch in patches.Prefixes.Concat(patches.Postfixes)
                    .Concat(patches.Transpilers).Concat(patches.Finalizers))
                    if (patch.owner == harmony.Id)
                        result.Add((original, patch.PatchMethod));
            }
            return result;
        }
    }
}
