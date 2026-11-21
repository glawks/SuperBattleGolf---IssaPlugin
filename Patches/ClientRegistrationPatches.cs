using System.Reflection;
using HarmonyLib;
using Mirror;
using UnityEngine;

namespace IssaPlugin.Patches
{
    // Prefix runs before base.OnClientConnect can Ready/AddPlayer and receive spawns.
    [HarmonyPatch(typeof(BNetworkManager), "OnClientConnect")]
    static class ClientConnectedRegistrationPatch
    {
        static void Prefix() => NetworkManagerRegisterPrefabsPatch.EnsureReady("before client Ready");
    }

    [HarmonyPatch(typeof(BNetworkManager), "OnStartServer")]
    static class ServerStartedRegistrationPatch
    {
        static void Postfix() => NetworkManagerRegisterPrefabsPatch.EnsureReady("OnStartServer");
    }

    [HarmonyPatch]
    static class CustomPrefabSpawnPatch
    {
        static MethodBase TargetMethod() => AccessTools.Method(typeof(NetworkClient), "OnSpawn");
        static void Prefix(SpawnMessage message)
        {
            if (!NetworkManagerRegisterPrefabsPatch.CustomPrefabs.TryGetValue(message.assetId, out var prefab))
                return;
            if (prefab == null)
            {
                IssaPluginPlugin.Log.LogError($"[NetworkPrefab] Template destroyed assetId={message.assetId} netId={message.netId}.");
                return;
            }
            if (!NetworkClient.prefabs.TryGetValue(message.assetId, out var current) || current == null)
            {
                IssaPluginPlugin.Log.LogWarning($"[NetworkPrefab] Repairing lost registration assetId={message.assetId} netId={message.netId} prefab={prefab.name}.");
                NetworkClient.RegisterPrefab(prefab);
            }
        }
        static void Postfix(SpawnMessage message)
        {
            if (!NetworkManagerRegisterPrefabsPatch.CustomPrefabs.ContainsKey(message.assetId)) return;
            bool exists = NetworkClient.spawned.TryGetValue(message.netId, out var identity) && identity != null;
            IssaPluginPlugin.Log.LogInfo($"[NetworkPrefab] spawn assetId={message.assetId} netId={message.netId} instantiated={exists}.");
        }
    }

    [HarmonyPatch]
    static class CustomPrefabDestroyPatch
    {
        static MethodBase TargetMethod() => AccessTools.Method(typeof(NetworkClient), "OnObjectDestroy");
        static void Prefix(ObjectDestroyMessage message)
        {
            if (NetworkClient.spawned.TryGetValue(message.netId, out var identity) && identity != null
                && NetworkManagerRegisterPrefabsPatch.CustomPrefabs.ContainsKey(identity.assetId))
                IssaPluginPlugin.Log.LogInfo($"[NetworkPrefab] destroy assetId={identity.assetId} netId={message.netId}.");
        }
    }
    [HarmonyPatch]
    static class ServerCustomPrefabSpawnPatch
    {
        static MethodBase TargetMethod() => AccessTools.Method(typeof(NetworkServer), "SpawnObject");
        static void Postfix(GameObject obj)
        {
            if (obj == null) return;
            var identity = obj.GetComponent<NetworkIdentity>();
            if (identity == null || !NetworkManagerRegisterPrefabsPatch.CustomPrefabs.ContainsKey(identity.assetId)) return;
            bool exists = identity.netId != 0 && NetworkServer.spawned.ContainsKey(identity.netId);
            IssaPluginPlugin.Log.LogInfo($"[NetworkPrefab] serverSpawn prefab={obj.name} assetId={identity.assetId} netId={identity.netId} accepted={exists}.");
        }
    }

}
