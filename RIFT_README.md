# Diablo 3 Style Rift Dungeon System (DOLSharp 1.130)
## Project Status, Open Issues & Implementation Roadmap

**Branch:** `feaature/1130`  
**Server Target:** .NET 10 (`Dawn of Light.sln` / `GameServer.csproj`)  
**Client Target:** Dark Age of Camelot 1.130b (`C:\DAoC\game.dll`, patched `connect.exe`)  
**Database:** Local SQLite (`dol.sqlite3.db`)  
**Updated:** October 30, 2026  

---

## 1. Where We Are At (Current State)

### A. Client Connection & 1.130 Protocol Stability
- **Client Encryption Bypass Fixed**: Resolved the 1.130 connection crash where unpatched `game.dll` sent an RSA-encrypted 655-byte `0xF4` RC4 session key packet resulting in `Bad TCP packet checksum` (`0000: 36 01 01 1E 62 8E 05`).
- **Binary Launcher Patched**: Patched `connect.exe` (both `D:\code\DOLSharp\connect.exe` and `C:\DAoC\connect.exe`) to wildcard high-order bytes and force client plaintext state (`g_EncryptionState = 1`).
- **Protocol & Packet Handling**:
  - Implemented 1.130 packet handling in [`PacketLib1130.cs`](file:///D:/code/DOLSharp/GameServer/packets/Server/PacketLib1130.cs) and [`CharacterSelectRequestHandler.cs`](file:///D:/code/DOLSharp/GameServer/packets/Client/168/CharacterSelectRequestHandler.cs).
  - Fixed client build parsing in [`CryptKeyRequestHandler.cs`](file:///D:/code/DOLSharp/GameServer/packets/Client/168/CryptKeyRequestHandler.cs).
  - Fixed packet sizing display and `ObjectDisposedException` bugs in [`GSTCPPacketOut.cs`](file:///D:/code/DOLSharp/GameServer/packets/Server/GSTCPPacketOut.cs) and [`GSUDPPacketOut.cs`](file:///D:/code/DOLSharp/GameServer/packets/Server/GSUDPPacketOut.cs).
  - Fixed dynamic region object removal bounds checking in [`Region.cs`](file:///D:/code/DOLSharp/GameServer/world/Region.cs#L1152-L1156).
  - **Fixed instance region detachment** in [`PlayerPositionUpdateHandler.cs`](file:///D:/code/DOLSharp/GameServer/packets/Client/168/PlayerPositionUpdateHandler.cs): position updates now keep the player's real region ID instead of overwriting it with the skinned static region. This was the root cause of the obelisk target/interact failures and the port-out `RemoveObject` conflicts.

### B. Rift Dungeon System: Iterations 1 & 2 (Complete)
- **Centralized Routing Hub (Cotswold Bridge)**:
  - All new characters created across Albion, Midgard, and Hibernia are automatically routed to Cotswold Bridge:
    - Coordinates: `Region 1`, `X: 558074, Y: 511741, Z: 2336, Heading: 3062` ([`StartupLocations.cs`](file:///D:/code/DOLSharp/GameServerScripts/gameevents/StartupLocations.cs) & [`RiftConstants.cs`](file:///D:/code/DOLSharp/GameServer/rift/RiftConstants.cs)).
- **Cotswold Entrance Portal**:
  - Interactive `RiftPortal` (`GameStaticItem`, Model 2603) spawned in front of the bridge (`X: 558574, Y: 511741, Z: 2336`).
  - Right-clicking the portal opens a private dungeon instance for solo players or warps the entire group.
- **Dynamic Dungeon Instancing (Spindelhalla)**:
  - Switched from Damp Cavern to classic dungeon **Spindelhalla** (`SkinID 125`).
  - Dynamic `RegionInstance` (assigned runtime ID 1000+) isolates player/group instances.
  - Entrance spawn position: `X: 32152, Y: 31841, Z: 16375, Heading: 90`.
  - Authentic mob cloning: loads all template mobs from `RegionData.Mobs` into the dynamic instance, retaining level, equipment, and abilities with respawns disabled.
  - **Status Verified**: Porting in works locally, Spindelhalla loads cleanly, and all dungeon mobs populate the instance as intended.

### C. Iteration 3: Containment, Countdown & Mob Reveal (Complete — Verified Working)
- **Obelisk Interaction Fixed**:
  - `RiftObelisk` now derives from `GameStaticItem` (the same base as `RiftPortal`) instead of `GameNPC`. Right-clicking the obelisk reliably opens the difficulty menu.
  - The menu is sent in the NPC-style popup window (`CL_PopupWindow`) with clickable links: `[Easy] [Normal] [Hard] [Torment]` and `[Exit to Cotswold]`.
  - Added `IWhisperTarget` (dispatched from [`GameLiving.Whisper`](file:///D:/code/DOLSharp/GameServer/gameobjects/GameLiving.cs)) so a non-living prop can receive the popup link whispers.
- **Visible 10-Second Countdown**:
  - Server-driven 1-second ticker with an initial screen-center warning and `Rift opening in 10...9...1` in the system/chat window.
  - The legacy `TimerWindow` UI was removed from the countdown: the 1.130 client fades/queues screen-center text, so the visible sequence lagged behind the actual server timer.
- **Containment Hold**:
  - Real `Mesmerize` spell **3629 "Stunning Shout"** applied for the countdown duration (icon/client effect 3629), so the client sees a normal mez.
  - Server-side containment flag + anchor: movement packets are rejected, speed 0 and position are re-asserted every tick, and the player is pulled back if they drift.
  - Released cleanly at completion; the lingering 60-second mez immunity effect is removed.
- **Dungeon Empty Until the Rift Opens**:
  - All cloned mobs are removed from the world when the instance is created and re-added when the countdown completes. AI is idle during containment and mobs visible at port-in are no longer shown.
- **Stability & Layout**:
  - Null-guarded spell tooltip lookups in [`DetailDisplayHandler.cs`](file:///D:/code/DOLSharp/GameServer/packets/Client/168/DetailDisplayHandler.cs) (fixes the `SpellDelve` NullReferenceException).
  - Dungeon exit portal renamed **"Rift Exit Portal"**; Cotswold portal remains "Rift Portal".
  - Obelisk repositioned into the entrance hall in plain view: `X: 32201, Y: 31813, Z: 16371`, heading 1024.

---

## 2. Resolved Issues (Iteration 3)

| Issue | Resolution |
| :--- | :--- |
| **1. Rift Obelisk Click / Interaction Not Triggering** | `RiftObelisk` refactored to `GameStaticItem`; right-click now always reaches `Interact()`. |
| **2. Difficulty Dialog / Whisper Links Not Displayed** | Menu uses the NPC popup window with bracketed links; `IWhisperTarget` routes the clicked choice back to the obelisk. |
| **3. Iteration 3 Features (Bubble, Alert, Countdown) Not Firing** | Countdown and containment now start from the obelisk interaction, with a visible per-second countdown and a real mez hold. |
| **4. Porting Out of Instance Fails** | Root cause was the player's region being overwritten to the skinned region (125) on movement; now preserved. `Rift Exit Portal` returns players to Cotswold. |
| **5. Obelisk & Portal Coordinates in Entrance Room** | Obelisk moved to `32201, 31813, 16371` in the entrance hall; exit portal renamed for clarity. |

---

## 3. Known Issues / Remaining Work

| Issue | Details | Next Step |
| :--- | :--- | :--- |
| **Native Spindelhalla exit door** | The dungeon's real exit zonepoint (`31992, 31844, 16371`) targets region 100 and has no `ClassType`, so it bypasses `OnInstanceDoor`. The stone `Rift Exit Portal` works, but walking into the native door still zones to region 100. | Override `RiftDungeonInstance.OnZonePoint` to intercept every zonepoint and send the player to Cotswold. |
| **Cleanup when everyone leaves** | Instances stay registered after the last player leaves (`DestroyWhenEmpty` is disabled for `RegionInstance`). | Unregister/collapse the instance when the last player leaves; clear hidden mobs and containment state. |
| **Mob levels are DB-driven** | Cloned mobs keep the Spindelhalla DB level regardless of player level or difficulty tier. | See Phase 3.1 (dynamic mob level scaling). |

---

## 4. What We Need To Do Next (Roadmap)

### Phase 1: Obelisk Interaction & Porting Out (Complete)
1. **Refactor `RiftObelisk`** — done:
   - `GameStaticItem` base, popup difficulty menu, whisper responses wired to `StartCountdown` / exit.
2. **Fix Porting Out** — done for the stone portal:
   - Exit portal placed in the entrance room and renamed `Rift Exit Portal`; clicking returns the player to Cotswold Bridge.
   - Remaining: force the native Spindelhalla exit door through `OnZonePoint` (see Known Issues).
   - Remaining: clean up instance references when all players leave.

### Phase 2: Iteration 3 — Containment & Countdown (Complete)
1. **Entrance hold** — done:
   - Players are mezzed and server-held on activation; movement is rejected and the player is anchored.
2. **Countdown sequence** — done:
   - 10-second ticker, screen-center warning, per-second system-window countdown, mob reveal on completion.
   - Remaining polish: optionally re-add a native timer window if a client-supported format is found.

### Phase 3: Iteration 4 — Mob Level Scaling & Difficulty Modifiers

#### 3.1 Dynamic Mob Level Scaling (new balance requirement)
**Problem:** When a dungeon instance loads, mob levels come straight from the Spindelhalla database records. A level 10 character and a level 50 character therefore face identical mobs, and the selected Rift difficulty currently only changes HP/damage modifiers (not the mobs' actual level).

**Target:** Derive each cloned mob's level from a mixture of:
- the dungeon's base level (from the DB / region template),
- the current player level(s) in the instance,
- the selected Rift difficulty tier (`Easy`, `Normal`, `Hard`, `Torment`).

**Open design questions to settle:**
- **Solo vs group:** scale from the party average, the party leader, the highest member, or a weighted value? How should a mixed-level group be handled?
- **Level spread:** how much should a level 50 player and a level 20 player in the same group pull the dungeon level? Consider clamping to a range around the party average.
- **Difficulty offset:** is difficulty a flat level offset (e.g. Easy −2, Normal +0, Hard +2, Torment +4) on top of the player-based level, or a multiplier?
- **Clamping:** keep mobs within a sane level band (e.g. 1–50) and avoid trivial/lethal outliers for the dungeon's geometry and spawn density.
- **Derived stats:** after changing `Level`, recompute health/power/damage/evade/parry/block and any level-scaled abilities; ensure equipment and loot templates remain valid.
- **Named/boss mobs:** keep bosses a fixed number of levels above the trash mobs rather than scaling independently.
- **When to apply:** at instance creation (before reveal) and re-apply if the difficulty is changed or a player joins/leaves mid-run.
- **Rewards:** scale XP and loot quality with the final mob level and difficulty tier.
- **Templates:** decide whether to scale the cloned mob in place or regenerate it from a template; keep original DB records untouched.

**Implementation notes:**
- Apply in `RiftDungeonInstance` after mob cloning and before `RevealDungeonMobs()`.
- Read the base level from the cloned `GameNPC.Level` before overwriting it.
- Recalculate derived values after setting `Level` (or use a helper on `GameNPC`).
- Make the level band/offsets configurable (e.g. `RiftConstants` or `ServerProperties`) so balance can be tuned without code changes.

#### 3.2 Difficulty Scaling & Combat Modifiers
1. **Dynamic Mob Scaling** (once levels are handled):
   - **Tier 1 (Easy)**: Base stats (1.0x HP, 1.0x Damage)
   - **Tier 2 (Normal)**: 1.25x HP, 1.15x Damage, 5% bonus XP/Loot
   - **Tier 3 (Hard)**: 1.6x HP, 1.35x Damage, 15% bonus XP/Loot
   - **Tier 4 (Torment)**: 2.2x HP, 1.75x Damage, 35% bonus XP/Loot
2. **Elite Pack Affixes** (Diablo style: Molten, Waller, Frozen, Extra Fast).

### Phase 4: Iteration 5 (Rift Progression, Boss Spawn & Rewards)
1. **Progression Tracker**:
   - Hook mob kill events in the instance to increment a completion percentage bar.
2. **Rift Guardian Boss**:
   - Spawns at 100% bar near the party with custom announcement.
3. **Reward Chest & Instance Collapse**:
   - Boss death spawns a high-tier reward chest and an exit portal back to town.
   - 60-second instance shutdown and memory cleanup timer.

---

## 5. Key Files & Reference Table

| Path | Purpose |
| :--- | :--- |
| [`GameServer/rift/RiftConstants.cs`](file:///D:/code/DOLSharp/GameServer/rift/RiftConstants.cs) | All coordinates, IDs, models, spells, and difficulty definitions. |
| [`GameServerScripts/custom/RiftDungeons/RiftManager.cs`](file:///D:/code/DOLSharp/GameServerScripts/custom/RiftDungeons/RiftManager.cs) | Instance creation, group routing, and Cotswold portal lifecycle. |
| [`GameServerScripts/custom/RiftDungeons/RiftDungeonInstance.cs`](file:///D:/code/DOLSharp/GameServerScripts/custom/RiftDungeons/RiftDungeonInstance.cs) | Dynamic Spindelhalla instance, mob cloning/reveal, countdown timer, containment, and scaling hook. |
| [`GameServerScripts/custom/RiftDungeons/RiftObelisk.cs`](file:///D:/code/DOLSharp/GameServerScripts/custom/RiftDungeons/RiftObelisk.cs) | Difficulty selection obelisk inside the entrance chamber (static item + popup menu). |
| [`GameServerScripts/custom/RiftDungeons/RiftPortal.cs`](file:///D:/code/DOLSharp/GameServerScripts/custom/RiftDungeons/RiftPortal.cs) | Cotswold entrance and dungeon exit portals. |
| [`GameServer/gameobjects/IWhisperTarget.cs`](file:///D:/code/DOLSharp/GameServer/gameobjects/IWhisperTarget.cs) | Whisper dispatch for non-living interactive objects (popup links). |
| [`GameServer/packets/Client/168/PlayerPositionUpdateHandler.cs`](file:///D:/code/DOLSharp/GameServer/packets/Client/168/PlayerPositionUpdateHandler.cs) | Keeps instance players in their real region; enforces CC/containment movement locks. |
| [`GameServerScripts/gameevents/StartupLocations.cs`](file:///D:/code/DOLSharp/GameServerScripts/gameevents/StartupLocations.cs) | Routes all newly created characters to Cotswold Bridge. |
| [`Tests/IntegrationTests/PacketLib1130Test.cs`](file:///D:/code/DOLSharp/Tests/IntegrationTests/PacketLib1130Test.cs) | Unit & integration tests for 1.130 packets, preprocessor, and packet buffer formatting. |
| [`Tests/UnitTests/UT_RiftDungeonInstancing.cs`](file:///D:/code/DOLSharp/Tests/UnitTests/UT_RiftDungeonInstancing.cs) | Unit tests for Rift instancing, countdown timers, containment, and scaling. |
