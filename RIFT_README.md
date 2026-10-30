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

### B. Rift Dungeon System: Iteration 1 & 2 (Complete & Verified Working)
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

---

## 2. Issues Currently Being Fixed

| Issue | Root Cause | Fix Required |
| :--- | :--- | :--- |
| **1. Rift Obelisk Click / Interaction Not Triggering** | `RiftObelisk` inherits `GameNPC`. In DAoC client protocol, right-clicking an NPC with a static stone model (2603) does not reliably trigger `ObjectInteractRequest`. In contrast, `RiftPortal` inherits `GameStaticItem` and interacts 100% reliably. | Refactor `RiftObelisk` to inherit `GameStaticItem` (or implement hybrid interactable) so right-clicks immediately invoke `Interact(GamePlayer player)`. |
| **2. Difficulty Dialog / Whisper Links Not Displayed** | `RiftObelisk.Interact` sends dialog text to `eChatLoc.CL_PopupWindow`. In DAoC 1.130, popup dialogue windows do not render clickable `[Whisper]` bracket hyperlinks. | Send interactive whisper links via standard chat (`CL_ChatWindow` or `CL_SystemWindow`), or invoke DAoC's native `player.Out.SendCustomDialog()`. |
| **3. Iteration 3 Features (Bubble, Alert, Countdown) Not Firing** | The golden containment sphere (`SpellID 7036`), immobilize/stun hold, "Make sure you're all buffed up!" alert, and 10s countdown timer window are wired inside `StartCountdown()`, which is triggered by the obelisk interaction. Because the obelisk click failed, the sequence never started. | Once obelisk interaction is restored, the countdown and containment triggers fire. Also add an entrance containment hold upon entering instance until difficulty is selected. |
| **4. Porting Out of Instance Fails** | Inside the dungeon, the exit portal coordinates (`X: 31992, Y: 31844, Z: 16371`) are either slightly out of position/LOS, or `player.MoveTo(ExitPosition)` needs instance detachment handling when jumping from dynamic Region 1000+ back to static Region 1. | Verify exit portal positioning in the entrance room and ensure `MoveTo` cleanly migrates the player back to Cotswold Bridge. |
| **5. Obelisk & Portal Coordinates in Entrance Room** | Obelisk coordinates (`X: 32250, Y: 31841, Z: 16375`) need fine-tuning relative to the entrance hall geometry so the object is visually placed on the floor in plain view. | Adjust coordinates and heading in `RiftConstants.cs`. |

---

## 3. What We Need To Do Next (Roadmap)

### Phase 1: Fix Obelisk Interaction & Porting Out (Immediate Next Step)
1. **Refactor `RiftObelisk`**:
   - Change base class to `GameStaticItem`.
   - Update `Interact(GamePlayer player)` to display clickable options in chat (`CL_ChatWindow` / `CL_SystemWindow`):
     ```text
     Rift Obelisk: Make sure you're all buffed up before activating!
     Choose Rift Difficulty: [Easy] [Normal] [Hard] [Torment] | [Exit to Cotswold]
     ```
   - Connect whisper responses (`Easy`, `Normal`, `Hard`, `Torment`, `Exit`) to trigger `StartCountdown(diff, player)` or teleport back to Cotswold.
2. **Fix Porting Out**:
   - Ensure the inside `ExitPortal` is placed directly behind the entrance spawn point and clicking it returns the player to Cotswold Bridge (`Region 1`).
   - Clean up instance references when all players leave.

### Phase 2: Finalize Iteration 3 (Containment & Countdown)
1. **Entrance Buffer Zone**:
   - Stun/freeze player upon entering until difficulty is chosen, or apply the containment effect around the starting platform.
2. **Countdown Sequence**:
   - Trigger 10-second native timer window (`SendTimerWindow("Rift Opening in...", 10)`).
   - Display prominent screen center warning (`CT_ScreenCenter`): *"WARNING: Make sure you're all buffed up! Rift opening in 10 seconds!"*
   - Play golden containment visual orb (`SpellID 7036`).
   - On completion: close timer window, play combat start horn (`SoundID 11`), release hold, and broadcast combat commencement.

### Phase 3: Iteration 4 (Difficulty Scaling & Combat Modifiers)
1. **Dynamic Mob Scaling**:
   - When difficulty is selected ([Easy], [Normal], [Hard], [Torment]), apply stat modifiers to all cloned Spindelhalla mobs:
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

## 4. Key Files & Reference Table

| Path | Purpose |
| :--- | :--- |
| [`GameServer/rift/RiftConstants.cs`](file:///D:/code/DOLSharp/GameServer/rift/RiftConstants.cs) | All coordinates, IDs, models, spells, and difficulty definitions. |
| [`GameServerScripts/custom/RiftDungeons/RiftManager.cs`](file:///D:/code/DOLSharp/GameServerScripts/custom/RiftDungeons/RiftManager.cs) | Instance creation, group routing, and Cotswold portal lifecycle. |
| [`GameServerScripts/custom/RiftDungeons/RiftDungeonInstance.cs`](file:///D:/code/DOLSharp/GameServerScripts/custom/RiftDungeons/RiftDungeonInstance.cs) | Dynamic Spindelhalla instance, mob cloning, countdown timer, and containment. |
| [`GameServerScripts/custom/RiftDungeons/RiftObelisk.cs`](file:///D:/code/DOLSharp/GameServerScripts/custom/RiftDungeons/RiftObelisk.cs) | Difficulty selection obelisk inside the entrance chamber. |
| [`GameServerScripts/custom/RiftDungeons/RiftPortal.cs`](file:///D:/code/DOLSharp/GameServerScripts/custom/RiftDungeons/RiftPortal.cs) | Cotswold and dungeon exit teleportation portal. |
| [`GameServerScripts/gameevents/StartupLocations.cs`](file:///D:/code/DOLSharp/GameServerScripts/gameevents/StartupLocations.cs) | Routes all newly created characters to Cotswold Bridge. |
| [`Tests/IntegrationTests/PacketLib1130Test.cs`](file:///D:/code/DOLSharp/Tests/IntegrationTests/PacketLib1130Test.cs) | Unit & integration tests for 1.130 packets, preprocessor, and packet buffer formatting. |
| [`Tests/UnitTests/UT_RiftDungeonInstancing.cs`](file:///D:/code/DOLSharp/Tests/UnitTests/UT_RiftDungeonInstancing.cs) | Unit tests for Rift instancing, countdown timers, and difficulty scaling. |
