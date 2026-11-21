using HarmonyLib;
using Mirror;

namespace IssaPlugin.Patches
{
    // The native deregistration path intentionally defers removal while shutting
    // down. Its manager must not simulate those destroyed mines during that window.
    [HarmonyPatch(typeof(LandmineManager), nameof(LandmineManager.FixedUpdate))]
    static class LandmineTeardownPatch
    {
        static bool Prefix() => !BNetworkManager.IsChangingSceneOrShuttingDown;
    }

    // Wait for the native seat/ownership transfer. Never send an unauthorised
    // input RPC, and never relax Mirror's server-side authority checks.
    [HarmonyPatch(typeof(GolfCartMovement), "BroadcastInputState")]
    static class GolfCartInputAuthorityPatch
    {
        static bool Prefix(GolfCartMovement __instance) =>
            __instance != null && (__instance.isServer || __instance.isOwned);
    }
}
