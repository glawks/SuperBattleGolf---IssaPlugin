# Continuous Minigun r3

The r2 four-shot limiter stopped the weapon for 1.25 seconds after each short burst.
That was a gameplay regression. R3 moves only the Minigun to server-owned shot
processing, retaining the configured 0.4s spin-up and 0.02s interval (50 bullets/s).
There are no artificial burst pauses. The existing 200-use pickup lasts about four
seconds of uninterrupted fire after spin-up.

Install the r3 DLL on EVERY peer. Keep the complete matching asset bundle you
already shared. The package contains DLL, PDB and test notes; no replacement bundle,
game reference DLL or dependency DLL needs installing. This is an unsigned dev build.
Main, releases and upstream PRs remain untouched.

## Why this does not need per-bullet client commands

Clients send only trigger, owned slot/seed and aim direction at up to 20 updates/s.
They do not claim hits, provide world origins, choose ammo counts or submit shot times.
The server checks the connection's player identity, enabled/equipped Minigun, slot
seed, remaining ammunition, match state, frozen/knockout/spectator state, finite unit
aim direction and a replicated-facing cone. Invalid input never grants ammunition.
A missing heartbeat stops fire after 0.45s. Changing the inventory/slot or leaving
also stops the session. Repeated inputs do not restart spin-up or grant extra shots.

A server-time clock emits shots at the HOST's configured interval, bounds catch-up
after stalls and never grants more shots than available ammunition. Each shot uses
the native server raycast filtering/line-of-sight and native Hittable hit behaviour.
Before executing it, the server checks the slot's current hash against the native
registered-use ledger. Native hit validation receives that real hash; the use is
then consumed through ServerValidateAndConsumeItemUse and the existing native
server inventory decrement. Slot replacement during a callback is detected.

No anti-cheat callback, limiter, report function, authority requirement or native
validation method is disabled or patched. Native calls from the authoritative server
use the game's existing server path, as other server-driven items already do.
Clients no longer send the Minigun's per-bullet decrement/hit commands that caused
the reports. Other legacy firearms retain r2's pacing in this test build.

Server-computed tracer/hit visuals are broadcast once per peer, with per-shooter
VFX cadence so two Miniguns do not suppress each other's trails. The server never
uses the host's camera to aim a remote player's shots. Existing configuration keys,
item definitions, bridge order and other message layouts are preserved. The new
MinigunInputMessage means mixing r2 and r3 peers is unsupported.

## Verification and test limits

Unit tests cover continuous default-rate firing at 30/60/144 server update rates,
spin-up, available-ammo limits, invalid intervals and bounded catch-up after a stall.
They do not execute Unity physics, native runtime networking or test real lag.
The build also runs the existing registrar/pacing tests in GitHub Actions.

First test privately with both host and remote on r3:

1. Equip, aim, hold fire through spin-up, and fire continuously. Confirm no recurring
   burst pauses. Check remaining ammo and that the last bullet deselects the item.
2. Repeat as the remote shooter, initially for a short hold. Check both logs before
   a longer hold. Stop at ANY native suspicious/rate/report warning.
3. Release during spin-up (no shots), release while firing (stops), and switch/drop
   the item while firing (server stops). Disconnect while holding (server stops).
4. Aim at several targets, walls and shields. Check visible hits/knockouts and that
   a remote shooter aims where their own camera points, not the host camera.
5. Fire two Miniguns simultaneously. Both peers should see both sets of trails and
   correct remaining uses. Check quick turns for aim-cone rejection/spin-up resets.
6. Confirm Moon and other previously fixed items still work with the shared bundle.

Capture full host AND client startup/session logs. Look for [MinigunServer] Start,
Stop, shots and reason. A missing native-use registration or shot exception is a
failure needing investigation, not a reason to disable validation. Native protection
reports remain release-blocking. Real multiplayer behaviour is not yet verified.
