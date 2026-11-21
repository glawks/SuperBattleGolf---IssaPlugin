using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using IssaPlugin.Compatibility;
using Xunit;

public class PatchRegistrarTests
{
    [Fact]
    public void MissingAndPartiallyAppliedClassesDoNotBlockOtherPatches()
    {
        var harmony = new Harmony("issamod.compatibility.tests");
        var other = new Harmony("another.mod");
        var errors = new List<string>();
        try
        {
            other.Patch(AccessTools.Method(typeof(Target), nameof(Target.First)),
                postfix: new HarmonyMethod(typeof(PatchRegistrarTests), nameof(OtherPostfix)));
            int failures = PatchRegistrar.Apply(harmony, typeof(PatchRegistrarTests).Assembly,
                _ => { }, errors.Add);
            Assert.Equal(2, failures);
            Assert.Equal(42, Target.Working());
            Assert.Equal(1, Target.First()); // failed multi-target class was rolled back
            Assert.Equal(2, Target.Second());
            Assert.Contains(errors, e => e.Contains(nameof(MissingTarget)));
            Assert.Contains(errors, e => e.Contains(nameof(PartialFailure)));
            Assert.Contains(Harmony.GetPatchInfo(AccessTools.Method(typeof(Target), nameof(Target.First))).Owners,
                id => id == other.Id);
            harmony.UnpatchAll(harmony.Id);
            Assert.Equal(0, Target.Working());
        }
        finally { harmony.UnpatchAll(harmony.Id); other.UnpatchAll(other.Id); }
    }

    private static void OtherPostfix() { }

    public static class Target
    {
        [MethodImpl(MethodImplOptions.NoInlining)] public static int Working() => 0;
        [MethodImpl(MethodImplOptions.NoInlining)] public static int First() => 1;
        [MethodImpl(MethodImplOptions.NoInlining)] public static int Second() => 2;
    }

    [HarmonyPatch(typeof(Target), "RemovedInGameUpdate")]
    private static class MissingTarget { static void Postfix() { } }

    [HarmonyPatch(typeof(Target), nameof(Target.Working))]
    private static class WorkingPatch
    {
        static void Postfix(ref int __result) => __result = 42;
    }

    [HarmonyPatch]
    private static class PartialFailure
    {
        static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(Target), nameof(Target.First));
            yield return AccessTools.Method(typeof(Target), nameof(Target.Second));
        }
        static bool Prepare(MethodBase original)
        {
            if (original?.Name == nameof(Target.Second))
                throw new InvalidOperationException("Simulated obsolete second target");
            return true;
        }
        static void Postfix(ref int __result) => __result = 99;
    }
}
