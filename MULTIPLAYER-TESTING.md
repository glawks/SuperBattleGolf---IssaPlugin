# Multiplayer compatibility test r2 — SBG 1.4.0-798

Development only. All peers must install the SAME new DLL and keep the existing
issamod asset bundle. No public release, main merge or upstream PR is authorised.
Replace the DLL in the existing IssaMod plugin folder. This DLL is unsigned; the
upstream .dll.sig does not sign it. Back up the old DLL and .cfg first.

## Confirmed evidence

Host logs 15:27/15:37 show registered items/prefabs and two obsolete patch failures.
The later host log confirms ammo-decrement rate violations, reports and kicks;
there is also a per-target hit warning. The partial remote log shows six failed
spawns for four custom prefab IDs, subsequent missing sync targets, absent
Teleporter VFX and a KickedFromLobby disconnect. It lacks client startup, so the
original cause/timing of its missing registrations and VFX state is not established.

The supplied game's actual limiter implementation uses cumulative violation counts:
five violations trigger suspicion; ten trigger confirmed cheating. A long-enough
quiet interval resets that counter; simply slowing down after violations does not.
No rate checker, kick/report callback, or server authority check is disabled here.

## Implemented changes and remaining checks

| Area | Change | What must still be verified |
| --- | --- | --- |
| Firearms | Shared sender-side gates across weapon switches; ammo requests >=0.15s apart with a 1.25s quiet interval after four. Native damage >=0.65s per target with 2.25s quiet intervals after four. Native gun VFX and sound have independent gates. Applies to Minigun, AK47, AA12, Remington870 and Sniper through shared Firearm code. | Source configuration remains intact, but effective sustained fire/damage is lower. Confirm ammo, deselection, aiming, hit effects, knockback and cancellation. No 50Hz gameplay claim. |
| Native protection | Native limits inspected: decrement 0.1s/reset1s, target hit 0.5s/reset2s, hit/miss VFX 0.25s/reset2s, elephant shot sound 0.2s/reset1s. Quiet windows and four-call bursts limit accumulated violations from ordinary packet bunching. | Automated tests model four-message bursts with 200ms extra delivery delay. Arbitrary transport stalls and traffic from other mods are not proven safe. Any suspicious/confirmed warning remains a release blocker. |
| Prefabs | Verify actual Mirror.prefabs entries, retain custom entries in manager.spawnPrefabs, register before Ready, initialise server handlers on server start, repair missing entries immediately before OnSpawn. No sticky registration boolean. | Determine whether missing client entries were timing, another clear, destroyed templates or a mismatched bundle. Existing logs cannot distinguish them. |
| Network diagnostics | Verified registration, lost-entry repair, server spawn accepted, client instantiation and destruction report asset ID/net ID. Bundle SHA256 and raw visual availability are logged at load. | Compare full startup logs on every peer. Uninstantiated or colliding IDs are failures, not successful spawns. |
| Moon | Log Begin/End reception; create/animate visuals without needing local player readiness or owned bridge ticking. Attach local physics when the player becomes available. Disconnect/hole cleanup restores gravity and clears sessions/sky. | Remote visual, delayed player readiness, pull/explosion and cleanup require Unity testing. No late-join snapshot protocol is added; a player joining midway through an already-running Moon may still miss it. |
| Teleporter | Log received VFX, raw prefab availability and local visual toggle separately. An intentional VFX opt-out is no longer reported as missing assets. | Missing raw bundle assets are still an error; disabled local cosmetics remain respected. Warping is independent of particles. |
| Burger speed | Discover both generated target-speed helpers by semantic prefix and signature, requiring unique matches; only fresh targets are scaled. | Both movement modes and stable final speed need in-game verification. |
| Transport | Remove obsolete FizzyFacepunch exception-suppression patch; no EOS/KCP patch or warning suppression added. | Confirm zero IssaMod patch failures on startup. |
| Equipment | Do not run native local-equipment setters or local equip hooks on remote inventories. Remote SyncVar model hooks remain. | Remote held models and animations after equip/death/respawn. |
| Donut layer | Use named Donut layer only if valid; otherwise retain authored layer. | Collision/terrain behaviour and no invalid-layer errors. |
| Cart | Preserve native prefab authority configuration; native seating transfers authority. Apply remote Joyride impulse only after owned identity exists. Input broadcasts wait for native ownership. | Remote Joyride and later entering/exiting/driving carts; ownership timeout diagnostic is a failure. |
| Landmine teardown | Do not run manager physics jobs while the game is changing scenes or shutting down, matching the native deferred-deregistration lifetime. | The supplied transform exception follows disconnect; this does not prove it caused the kick. Check teardown logs and subsequent lobby. No catch-all exception finalizer. |
| ModConfig | No dependency DLL changes. 0.1.3 still needs its own FullScreenMessage signature update. | Disable ModConfig for first isolation test; .cfg settings remain available. |

All 46 definitions, ItemType IDs 100–145, config keys/defaults, wire message types
and bridge component order are retained. No new NetworkBehaviour components are
added to prefabs. No game runtime binaries/assets are needed in CI: generated
ReferenceAssembly-marked metadata-only APIs are committed for compilation, and
excluded from the downloadable plugin artifact.

## Focused validation order

1. **Offline host smoke test first.** Launch with r2, temporarily without ModConfig.
   Confirm zero patch failures, 46 item registrations, VFX enabled and raw assets
   present. Compare bundle SHA256 between peers. Never replace the asset bundle or
   install any BuildReferences DLL in the game.
2. **Controlled two-person private test.** Before extended firing, shoot one shell
   and one short burst of at most four shells at empty terrain. Then one burst at
   one target. Inspect BOTH logs after each stage. Stop at any rate warning,
   suspicious-activity line, missing registration, unexpected disconnect or report.
3. **Minigun and other firearms.** Confirm the deliberate paced cadence, correct
   decrements and last-use deselection. Release during a pacing pause and verify no
   delayed shot. Switch between Minigun/AK47/shotguns; the shared gate must survive
   switching. Only extend duration after short tests and logs are clean.
4. **Moon.** Both peers present before activation; use once as host and once as remote.
   Check Begin received, visual/session started, End received and matching lifetime
   on BOTH sides. If invisible, include startup VFX state and bundle SHA256. Check
   hole transition, disconnect and a fresh Moon after reconnect. Mid-session late
   joining is a separate unresolved limitation.
5. **Teleporter.** Use as remote; verify actual warp and origin/destination particles
   on both peers. With CustomVfxEnabled=false on one peer, warping must still work
   and its log must identify the intentional visual opt-out.
6. **Network objects.** Spawn/use Black Hole Grenade, drop/pick up a custom item,
   Orb Bomber and Placeable Wall. Match server/client asset IDs and net IDs. Require
   accepted=true and instantiated=true and correct destruction. Any missing sync
   target repetition is a failure; retain the original warning and diagnostic lines.
7. **Lifecycle.** Repeat object/Moon tests after a hole change, client reconnect and
   second lobby. Check remote held models and one Joyride/normal cart boarding cycle.
   Include the full startup log from each peer, not just the end of the session.

Do not publish if a native protection warning/report remains. Do not stress-test on
public servers. This is a bounded development test, not a claim of complete runtime
or arbitrary-network-latency verification.
