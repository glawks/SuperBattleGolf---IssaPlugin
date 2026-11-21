using System;
using System.Collections.Generic;
using IssaPlugin.Compatibility;
using Mirror;
using UnityEngine;

namespace IssaPlugin.Items
{
    // No client-supplied hit targets, hit positions, use counts or firing times.
    internal static class MinigunServerFire
    {
        private sealed class Session
        {
            internal NetworkConnectionToClient Connection;
            internal PlayerInventory Inventory;
            internal int Slot, Seed, Shots;
            internal bool SingleShot;
            internal double LastInput;
            internal Vector3 Direction;
            internal AutomaticFireClock Clock;
        }
        private static readonly Dictionary<int, Session> Sessions = new();
        private static readonly HashSet<string> MissingLedgerWarnings = new();
        private const double InputTimeout = 0.45;

        internal static void Handle(NetworkConnectionToClient connection, MinigunInputMessage input)
        {
            if (!NetworkServer.active || connection?.identity == null) return;
            if (!input.Held) { Stop(connection.connectionId, "trigger released"); return; }
            var inventory = connection.identity.GetComponent<PlayerInventory>();
            if (!ValidInventory(inventory, input.Slot, input.Seed, out _)
                || !Finite(input.Direction) || input.Direction.sqrMagnitude < 0.5f
                || input.Direction.sqrMagnitude > 1.5f)
            { Stop(connection.connectionId, "invalid input/inventory"); return; }
            var direction = input.Direction.normalized;
            // Allow normal camera/barrel offset and replicated turning latency, but
            // reject directions behind the player's replicated facing hemisphere.
            if (Vector3.Dot(inventory.transform.forward, direction) < -0.35f)
            { Stop(connection.connectionId, "aim outside facing cone"); return; }

            double now = Time.timeAsDouble;
            if (Sessions.TryGetValue(connection.connectionId, out var session))
            {
                if (now - session.LastInput > InputTimeout || session.Inventory != inventory
                    || session.Slot != input.Slot || session.Seed != input.Seed)
                { Stop(connection.connectionId, "slot changed"); session = null; }
                else if (now - session.LastInput < 0.025) return; // bound input floods
            }
            if (session == null)
            {
                session = new Session
                {
                    Connection = connection, Inventory = inventory, Slot = input.Slot, Seed = input.Seed,
                    SingleShot = input.SingleShot,
                    Clock = new AutomaticFireClock(now, SafeSpinUp())
                };
                Sessions[connection.connectionId] = session;
                IssaPluginPlugin.Log.LogInfo($"[MinigunServer] Start conn={connection.connectionId} slot={input.Slot}, interval={Interval():F3}s.");
            }
            session.Direction = direction;
            session.LastInput = now;
        }

        internal static void Update()
        {
            if (!NetworkServer.active) { Clear(); return; }
            foreach (var pair in new List<KeyValuePair<int, Session>>(Sessions))
            {
                var s = pair.Value;
                if (!NetworkServer.connections.TryGetValue(pair.Key, out var active) || active != s.Connection
                    || s.Connection.identity == null || s.Connection.identity.GetComponent<PlayerInventory>() != s.Inventory
                    || Time.timeAsDouble - s.LastInput > InputTimeout
                    || !ValidInventory(s.Inventory, s.Slot, s.Seed, out var slot))
                { Stop(pair.Key, "timeout, state change or empty inventory"); continue; }
                int shots = s.Clock.TakeDue(Time.timeAsDouble, Interval(), slot.remainingUses);
                for (int i = 0; i < shots; i++)
                {
                    if (!ValidInventory(s.Inventory, s.Slot, s.Seed, out slot)) break;
                    ulong hash = slot.GetUsageValidationHash();
                    if (!ItemRegistry.HasRegisteredUse(s.Inventory, hash))
                    {
                        if (MissingLedgerWarnings.Add($"{pair.Key}:{s.Seed}"))
                            IssaPluginPlugin.Log.LogError($"[MinigunServer] Refusing unregistered ammo conn={pair.Key}, slot={s.Slot}; no shot executed.");
                        Stop(pair.Key, "missing native use registration"); break;
                    }
                    // Native hit validation sees the live registered hash, then we
                    // consume exactly that use through the native server ledger.
                    bool failed = false;
                    try { Firearm.FireServerMinigunShell(s.Inventory, MinigunItem.ShellProfile(), s.Direction, hash); }
                    catch (Exception ex)
                    {
                        failed = true;
                        IssaPluginPlugin.Log.LogError($"[MinigunServer] Shot failed conn={pair.Key}: {ex}");
                    }
                    // Charge the use even if a cosmetic/game callback throws. If a
                    // callback removed/replaced the slot, do not touch its replacement
                    // or submit a now-invalid use hash to native validation.
                    if (ItemRegistry.TryGetSlot(s.Inventory, s.Slot, out var current)
                        && current.itemType == slot.itemType && current.validationSeed == slot.validationSeed
                        && current.remainingUses == slot.remainingUses && ItemRegistry.HasRegisteredUse(s.Inventory, hash))
                    {
                        if (s.Inventory.ServerValidateAndConsumeItemUse(hash))
                            ItemHelper.DecrementAndRemove(s.Inventory, s.Slot); // isServer: no client Cmd
                        else failed = true;
                    }
                    else failed = true;
                    if (failed) { Stop(pair.Key, "shot failure or inventory changed during shot"); break; }
                    s.Shots++;
                    if (s.SingleShot) { Stop(pair.Key, "single use completed"); break; }
                }
            }
        }

        private static bool ValidInventory(PlayerInventory inventory, int index, int seed, out InventorySlot slot)
        {
            slot = default;
            if (inventory == null || !inventory.isServer || inventory.PlayerInfo == null
                || !ItemRegistry.GetDefinition(ItemRegistry.MinigunItemType).Enabled
                || CourseManager.MatchState == MatchState.HoleOverview || CourseManager.MatchState == MatchState.Ended
                || (!SingletonBehaviour<DrivingRangeManager>.HasInstance && CourseManager.MatchState <= MatchState.TeeOff)) return false;
            var info = inventory.PlayerInfo;
            if (info.Movement == null || info.AsHittable == null || info.AsSpectator == null
                || info.Movement.IsKnockedOutOrRecovering
                || info.AsHittable?.FrozenState == FrozenState.Frozen || info.AsSpectator.IsSpectating) return false;
            int equipped = inventory.isLocalPlayer ? inventory.EquippedItemIndex : info.NetworkedEquippedItemIndex;
            return equipped == index && ItemRegistry.TryGetSlot(inventory, index, out slot)
                && slot.itemType == ItemRegistry.MinigunItemType && slot.remainingUses > 0 && slot.validationSeed == seed;
        }
        internal static float Interval()
        {
            float value = ModConfig.Minigun.FireRate.Value;
            return float.IsFinite(value) ? Mathf.Max(0.01f, value) : 0.02f;
        }
        private static float SafeSpinUp() => float.IsFinite(ModConfig.Minigun.SpinUp.Value)
            ? Mathf.Max(0, ModConfig.Minigun.SpinUp.Value) : 0.4f;
        private static bool Finite(Vector3 value) => float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
        private static void Stop(int id, string reason)
        {
            if (Sessions.TryGetValue(id, out var session))
                IssaPluginPlugin.Log.LogInfo($"[MinigunServer] Stop conn={id}, shots={session.Shots}, reason={reason}.");
            Sessions.Remove(id);
        }
        internal static void Clear() { Sessions.Clear(); MissingLedgerWarnings.Clear(); }
    }
}
