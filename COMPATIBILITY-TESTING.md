# SBG 1.4.0-798 compatibility development build

Status: compiled successfully against the supplied 1.4.0-798 managed DLLs, BepInEx 5.4.23.5 and HarmonyX 2.9.0.0; gameplay is not yet verified.
No release or main-branch update is part of this work.

## Evidence and root cause

The reported startup log identifies BepInEx 5.4.23.5, Unity 6000.3.27,
IssaMod 0.0.49, and ModConfig 0.1.3. The reported Harmony failure targets
BNetworkManager.OnPlayerConfirmedCheatingDetected, a method absent in the new game.
The supplied Default.zip includes LogOutput.log confirming both startup errors.
This is log and assembly inspection, not an in-game reproduction.

Plugin.Awake initializes all configuration and loads the bundle before PatchAll.
A Harmony exception aborts the remaining Awake work: SessionConfig, integrations,
match/config event subscriptions, sync components, overlays and spawner window.
Depending on discovery order, only some attributed patches may have been installed.
Asset-bundle success therefore does not demonstrate successful item registration.

The obsolete SuppressCheatKickPatch is removed. No replacement anti-cheat target
or suppression is installed. Native validation and rate limits remain active.
Rapid-fire items previously relied on global kick suppression; remote rapid fire
must be tested for rate-limit rejection or disconnects before calling this fully
compatible. Changing those call sites requires inspection of the current game API.

Patch classes are now processed independently under the original Harmony owner.
Failures log their class and full exception and do not stop later classes.
Newly installed patches from a failed multi-target class are rolled back. Successful
classes and other mods' patches remain installed. A failed required gameplay patch
still means incomplete functionality; failure counts are printed at startup.
Manual SessionConfig installation remains required and is not silently ignored.

## Item and network trace

- Items/ItemRegistry.cs declares 46 definitions with stable ItemType IDs 100–145.
  Definitions, IDs, configuration keys, defaults, bundle and wire messages are unchanged.
- ItemCollectionInitPatch runs after ItemCollection.Initialize and calls
  InjectCustomItems, extending private items and allItemData collections with ItemData.
  ItemData properties and Initialize are also required; item names have a getter patch
  plus lazy localization registration from Plugin.Update.
- ItemSpawnerSettings.ResetRuntimeData injects weighted enabled items into regular
  and mobility pools through private ItemPool.spawnChances and totalSpawnChanceWeight.
  MatchSetupRules patches also recalculate weights; host spawn configuration is synced.
- The obsolete ServerTryAddItem interception is removed. DirectAddCustomItem now
  calls native ServerTryAddItem, which accepts injected ItemData, checks space,
  creates the slot and registers valid item-use hashes. The old direct slot write
  omitted the new validation ledger. Mirror's generated UserCode_CmdAddItem__ItemType
  custom-item hook and the existing host/hotkey grant permissions are retained.
- TryUseItem and animator/inventory patches dispatch definitions and held models.
  ItemHelper reflects DecrementUseFromSlotAt, RemoveIfOutOfUses and SetCurrentItemUse.
- NetworkIdentity.Awake attaches the same ordered bridge components on every peer.
  BNetworkManager.OnStartClient registers prefabs and custom message serializers and
  handlers; OnStopClient resets registration. Server-ready pushes host configuration
  and weights to late joiners. Network messages bypass Mirror IL weaving.
- All peers need the same development DLL and original bundle. A host-only test
  cannot validate remote serialization, bridge indices or reconnect behaviour.

Inspection of the supplied assemblies confirms the item collection, pool, inventory
add/use/consume, and network lifecycle hooks above are present with the expected
parameter names. A literal-name scan of reflected game fields/properties found no
missing members. Compiling the complete plugin found additional API changes:

- Native ServerTryAddItem now calls ServerAddValidItemUsesForSlot. Grants use this
  native path so the validation ledger matches the slot seed and use count.
- PlayerMovement.CharacterScale is gone. Reads now use its generated
  NetworkcharacterScale property, which returns the same underlying characterScale
  SyncVar used by the existing setters. No new message types or component order changes.
- PlayerMovement.NetworksyncedVelocity is gone. Grappling still writes the existing
  Velocity property, whose setter writes Rigidbody.linearVelocity plus anchor velocity.
  The obsolete duplicate sync write is removed; the game now reads remote motion
  from PlayerInfo.AsEntity.NetworkRigidbody. Remote grappling remains a test priority.
- The generated upper-body helper moved from ordinal 153_0 to 170_0. Resolve its
  semantic name prefix and require a unique instance bool/no-argument method.
- Four existing null-conditional assignments require a newer C# compiler. They are
  replaced with equivalent explicit null checks so the .NET 8 build works.

These checks do not execute Harmony patches inside Unity; changed runtime semantics
and validation still require the checklist below.

## Dependency assessment and build

The project targets netstandard2.1 and BepInEx 5.x, matching the reported loader's
major version. Its Unity compile package is 6000.2.4, while the log reports 6000.3.27;
the test DLL is compiled directly against the supplied engine assemblies. The default build also requires
untracked Assembly/*.dll game, Mirror, UI, ModConfig and ItemSpawner references.
The development build supports -p:UseInstalledReferences=true with the supplied
managed, BepInEx core and ModConfig DLLs in Assembly/. It excludes duplicate legacy
0Harmony20 and framework assemblies. ItemSpawner interoperability now resolves the
loaded plugin instance type; it no longer requires an ItemSpawner compile reference.
No ItemSpawner mod is installed in the supplied profile.

ModConfig 0.1.3's FullScreenMessage.ShowInternal parameter mismatch is a separate
plugin failure. This patch does not modify, unpatch or repair ModConfig. IssaMod's
own configuration file remains available without that optional UI, but the installed
ModConfig UI cannot be claimed compatible until its own fix is tested. For isolation,
first test without ModConfig and ItemSpawner, then re-enable each separately.

The development Actions workflow runs registrar regression tests without game DLLs.
To compile the plugin, supply a HTTPS ZIP URL via workflow_dispatch references_url
(or repository variable ISSAMOD_REFERENCES_URL). The ZIP needs the managed game DLLs,
BepInEx core DLLs and ModConfig DLL (the same inputs as the local successful build). The workflow fails if any reference is missing, records SHA-256 hashes, builds,
and uploads only the plugin, PDB, unchanged bundle, these notes and commit ID.
It does not publish a release or upload game reference assemblies as artifacts.
Use an authorized reference source; no game binaries need be committed to Git.

## In-game test checklist

1. Back up the existing plugin folder/config. Replace IssaPlugin.dll with the dev DLL;
   retain the original IssaPluginBundle. Do not reuse the upstream .dll.sig: it signs
   different bytes. This fork's test build is unsigned.
2. Launch without ModConfig/ItemSpawner. Capture the full BepInEx LogOutput.log.
   Check the compatibility registration summary and final loaded message. Any patch
   failure is actionable, even when startup continues.
3. Host a private lobby. Confirm '[ItemRegistry] Injected 46 custom items', regular
   and mobility pool injection, and prefab/message-handler registration logs.
4. Exercise all 46 items using the existing spawner/hotkeys, then normal item boxes.
   Check names, icons, models, use counts, last-use deselection and inventory sizes.
5. Test remote client grants with AllowHotkeyItemGiving both disabled and enabled,
   pickup/use/drop, rapid-fire ammo/damage/VFX, and host-controlled spawn settings.
6. Verify late join, reconnect, second lobby, hole transitions and match-end cleanup.
   Check remote visuals and host config sync without overwriting client config files.
7. Repeat with ModConfig 0.1.3 and ItemSpawner enabled separately, recording their
   own errors. Confirm every existing setting remains available in the .cfg file.

Review: item definitions/configuration/network messages/bundle are preserved. Inventory
  grants now use native validation, movement calls use current members, animation
  helper discovery tolerates compiler ordinal changes, and global kick suppression
  is removed. Registrar isolation has regression tests
for missing targets, partial-class rollback, other-owner preservation and unload.
The complete plugin compiles with zero warnings and zero errors. In-game checks remain required before merge or release.
