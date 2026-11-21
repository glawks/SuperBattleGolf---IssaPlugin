using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using Mirror;

namespace IssaPlugin.Items
{
    /// <summary>
    /// Heavy automatic gun. Spins up while aimed in, then fires one shot per
    /// fire-rate tick while left-click and aim are held. The shot sound plays
    /// once per trigger pull. Each shot consumes one use. The spin-up consumes none.
    /// </summary>
    public static class MinigunItem
    {
        private static bool _clientRunning;
        internal static void Reset() { _clientRunning = false; Firearm.SetMinigunTrigger(null, false); }

        public static IEnumerator FireLoop(PlayerInventory inventory)
        {
            if (_clientRunning || inventory == null || !inventory.isLocalPlayer) yield break;
            _clientRunning = true;
            bool mouseStartedDown = Mouse.current != null && Mouse.current.leftButton.isPressed;
            double started = Time.timeAsDouble, nextInput = double.NegativeInfinity, nextShake = started;
            bool soundPlayed = false;
            try
            {
                while (NetworkClient.active && inventory != null && inventory.isLocalPlayer
                    && inventory.GetEffectivelyEquippedItem(true) == ItemRegistry.MinigunItemType
                    && inventory.PlayerInfo.Input.IsHoldingAimSwing
                    && (!mouseStartedDown || (Mouse.current != null && Mouse.current.leftButton.isPressed)))
                {
                    double now = Time.timeAsDouble;
                    if (now >= nextInput && ItemRegistry.TryGetSlot(inventory, inventory.EquippedItemIndex, out var slot))
                    {
                        Vector3 origin = inventory.GetElephantGunBarrelEndPosition();
                        Vector3 point = inventory.GetFirearmAimPoint(origin, inventory.GetElephantGunBarrelForward(),
                            ModConfig.Minigun.MaxAimingDistance.Value, GameManager.LayerSettings.GunHittablesMask, out float yaw);
                        if (Mathf.Abs(yaw) > 45f)
                        {
                            inventory.PlayerInfo.Movement.AlignWithCameraImmediately();
                            point = inventory.GetFirearmAimPoint(origin, inventory.GetElephantGunBarrelForward(),
                                ModConfig.Minigun.MaxAimingDistance.Value, GameManager.LayerSettings.GunHittablesMask, out _);
                        }
                        Vector3 direction = point - origin;
                        if (direction.sqrMagnitude > 0.0001f)
                            NetworkClient.Send(new MinigunInputMessage
                            {
                                Held = true, SingleShot = !mouseStartedDown, Slot = inventory.EquippedItemIndex,
                                Seed = slot.validationSeed, Direction = direction.normalized
                            });
                        nextInput = now + 0.05;
                    }
                    float spin = ModConfig.Minigun.SpinUp.Value;
                    if (now - started >= (float.IsFinite(spin) ? Mathf.Max(0, spin) : 0.4f))
                    {
                        Firearm.SetMinigunTrigger(inventory, true);
                        if (!soundPlayed) { Firearm.PlayMinigunStartSound(inventory); soundPlayed = true; }
                        if (now >= nextShake)
                        {
                            ScreenShakeHelper.ApplyScreenShake(ModConfig.Minigun.ScreenShakeIntensity.Value);
                            nextShake = now + MinigunServerFire.Interval();
                        }
                        // Preserve the previous non-mouse single-use behaviour.
                        if (!mouseStartedDown && now - started >= Mathf.Max(0, spin) + 0.15) break;
                    }
                    yield return null;
                }
            }
            finally
            {
                if (NetworkClient.active) NetworkClient.Send(new MinigunInputMessage { Held = false });
                Firearm.SetMinigunTrigger(inventory, false);
                _clientRunning = false;
            }
        }

        internal static FirearmShellProfile ShellProfile() =>
            new FirearmShellProfile
            {
                ItemType = ItemRegistry.MinigunItemType,
                HitResponseType = ItemType.DuelingPistol,
                MaxAimingDistance = ModConfig.Minigun.MaxAimingDistance.Value,
                MaxShotDistance = ModConfig.Minigun.MaxShotDistance.Value,
                Inaccuracy = ModConfig.Minigun.Inaccuracy.Value,
                PelletCount = Mathf.Max(1, Mathf.RoundToInt(ModConfig.Minigun.PelletCount.Value)),
                ScreenShakeIntensity = ModConfig.Minigun.ScreenShakeIntensity.Value,
                VfxInterval = ModConfig.Minigun.VfxInterval.Value,
                UseShotgunVisuals = true,
            };
    }
}
