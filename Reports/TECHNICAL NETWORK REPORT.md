# Technical Network Report
**Project:** Campbound — 3D Asymmetric Multiplayer Nature Survival
**Course:** Multiplayer Game Development – Midterm
**Students:** Tolga Dıgdıgoğlu, Yusuf Durak, Ömer Efe Acar
**Date:** April 8, 2026

---

## 1. Project Overview

### What the game is
Campbound is a **2-player, 3D asymmetric co-op survival** game built in Unity. It is inspired by the market gap identified in the team's market research: most survival games suffer from "Role Overlap," where both players do the same tasks and communication becomes meaningless. Campbound solves this with **forced specialization**, splitting the two players into radically different roles:

| Role | Perspective | Core Loop |
|---|---|---|
| **The Keeper** | Camp management & strategy | Maintains the campfire, cooks food, fortifies the base. |
| **The Gatherer** | Action & exploration | Scavenges wood and ingredients, avoids wildlife and hazards. |

Neither player can survive without the other, creating a high-stakes social bond. The game targets a PC (Steam) release at a **$19.99 premium** price point, aiming for a 7.5/10+ user score.

### Player count
**2 players** (1 host + 1 client).

### Unity version
**Unity 6000.3.6f1** (Unity 6, LTS family).

### NGO version
**Netcode for GameObjects (NGO) 2.11.0**, backed by **Unity Transport (UTP) 2.6.0**.

---

## 2. Scene and Prefab Structure

### Scenes

| Scene | Purpose |
|---|---|
| `Assets/Scenes/SampleScene.unity` | **The only gameplay scene.** Contains all in-world objects, the NetworkManager, and the HUD. |
| `Assets/TextMesh Pro/Examples & Extras/Scenes/*.unity` | Third-party TMP demo scenes (31 files). Not part of the game. |

**Contents of `SampleScene`:**

| GameObject | Components |
|---|---|
| `Main Camera` | Camera (disabled/replaced per-player at runtime) |
| `Directional Light` | Light |
| `Global Volume` | URP Post-Processing |
| `NetworkManager` | `NetworkManager`, `UnityTransport` (127.0.0.1:7777) |
| `Plane` | MeshRenderer, MeshCollider (ground) |
| `HUD` Canvas | Unity UI Canvas with **Host** button, **Client** button, TMP labels |
| `GameManager` | `NetworkUI` (handles button callbacks) |
| `EventSystem` | `StandaloneInputModule` (legacy UI input) |

> The **Player** is **not** placed in-scene. It is spawned automatically by NGO from `NetworkManager.PlayerPrefab` when a connection is established.

### Prefabs

| Prefab | Key Components |
|---|---|
| `Assets/Prefabs/Player.prefab` | `NetworkObject`, `PlayerMovement`, `ThirdPersonCamera`, `ClientNetworkTransform`, `CapsuleCollider` |
| `Assets/TextMesh Pro/Examples & Extras/Prefabs/*.prefab` | Third-party TMP demo prefabs (not used in gameplay) |

---

## 3. NetworkObject Inventory

There is **one** networked object in the game: the **Player prefab**.

| Object | NetworkBehaviour Scripts | Ownership Model |
|---|---|---|
| `Player.prefab` | `PlayerMovement`, `ThirdPersonCamera` | **Client-owned** (owner-authoritative) |

### Script details

**`ClientNetworkTransform`** — not a `NetworkBehaviour` directly, but a subclass of NGO's built-in `NetworkTransform`. The single override `OnIsServerAuthoritative() => false` switches the transform sync from server-authoritative to **owner-authoritative**: the owning client writes its position/rotation, and NGO replicates it to all other clients and the host.

**`PlayerMovement`** (extends `NetworkBehaviour`) — enabled only for `IsOwner`. Reads local input axes and directly mutates `transform.position` and `transform.rotation` every frame.

**`ThirdPersonCamera`** (extends `NetworkBehaviour`) — enabled only for `IsOwner`. Spawns a dedicated `Camera` GameObject for the local player, disables the scene's `Main Camera`, and exposes `PlanarForward`/`PlanarRight` vectors for movement to consume. Destroyed when the player object is despawned.

> The `NetworkManager` GameObject in the scene is **not** a `NetworkObject` and carries no `NetworkBehaviour`. It is the NGO bootstrap singleton only.

---

## 4. Data Synchronization

### NetworkVariables
**There are no custom `NetworkVariable` declarations** in this project. All state replication is handled implicitly by `ClientNetworkTransform`, which uses NGO's internal delta-compression and snapshot system over UTP.

### RPCs
**There are no `[ServerRpc]`, `[ClientRpc]`, or `[OwnerRpc]` calls** in this project. The transform is the only data that crosses the network, and it does so via `ClientNetworkTransform`.

| Mechanism | What it syncs | Writer | Readers |
|---|---|---|---|
| `ClientNetworkTransform` (inherits `NetworkTransform`) | Player `position` and `rotation` | **Owning client** | All other clients + host |

### Tick rate
The `NetworkManager` is configured at **30 ticks per second** (`TickRate: 30`).

---

## 5. Connection Flow (Step by Step)

### Joining
1. Both players launch the game and land on `SampleScene` with the HUD visible.
2. **One player presses "Host"** → `NetworkUI.StartHost()` → `NetworkManager.Singleton.StartHost()`.
   - The host starts listening on `127.0.0.1:7777` (UTP).
   - Connection approval is **disabled** (`ConnectionApproval: 0`), so any client is accepted unconditionally.
   - The host's own Player prefab is immediately spawned.
3. **The other player presses "Client"** → `NetworkUI.StartClient()` → `NetworkManager.Singleton.StartClient()`.
   - UTP opens a connection to `127.0.0.1:7777`.
   - After the handshake completes, NGO spawns the client's Player prefab on all connected machines (`AutoSpawnPlayerPrefabClientSide: 1`).
4. **`HideUI()`** is called on both machines — the HUD canvas is deactivated.

### On spawn (`OnNetworkSpawn`)
For each spawned Player object:
- **On the owning client:** `PlayerMovement.OnNetworkSpawn` enables the script and fetches `ThirdPersonCamera`. `ThirdPersonCamera.OnNetworkSpawn` disables `Main Camera`, creates a new `Camera` GameObject, and locks the cursor.
- **On all other machines (non-owner):** Both `PlayerMovement` and `ThirdPersonCamera` call `enabled = false` and return immediately — no local motor, no extra camera.

### On disconnect
- If the **host** disconnects, all clients are dropped (no host migration implemented).
- When a Player object is despawned, `ThirdPersonCamera.OnDestroy` fires: if `IsOwner`, cursor lock is released (`CursorLockMode.None`, `Cursor.visible = true`).
- The HUD canvas is **not** re-enabled automatically on disconnect in the current implementation.

---

## 6. Input and Authority Model

### Who handles input
Input is read **only on the owning client** via Unity's **legacy `UnityEngine.Input` API** (`Input.GetAxis`). There is no Unity Input System package installed.

| Input | Axis | Used by |
|---|---|---|
| Move forward/back | `"Vertical"` | `PlayerMovement.Update()` |
| Move left/right | `"Horizontal"` | `PlayerMovement.Update()` |
| Camera yaw | `"Mouse X"` | `ThirdPersonCamera.Update()` |
| Camera pitch | `"Mouse Y"` | `ThirdPersonCamera.Update()` |

### Where movement is calculated
Movement is calculated **entirely on the owning client**:

```
Client reads Input → calculates direction → mutates transform.position/.rotation locally
  → ClientNetworkTransform detects delta → sends position update to server/other clients
```

The server **never** recalculates movement; it only receives and relays the positions broadcast by each owner.

### Cheating prevention
There is **none** in the current build. The owner-authoritative model (`ClientNetworkTransform`) means a malicious client can teleport or move at any speed and every other machine will display that position faithfully. This is acceptable for a course prototype where both players are trusted, but it would need to be replaced with server-side validation or server-authoritative movement before any public release.

---

## 7. Known Limitations and Future Work

### What doesn't work yet
| Limitation | Detail |
|---|---|
| **LAN-only connectivity** | Transport is hardcoded to `127.0.0.1:7777`. Players on different machines or networks cannot connect. Unity Relay + Lobby would be required for internet play. |
| **No asymmetric roles implemented** | The market research defines The Keeper and The Gatherer, but both players currently use the **same** `Player` prefab with identical movement. Role differentiation, camp management, and resource gathering mechanics have not been implemented. |
| **No HUD restoration on disconnect** | After a player disconnects the connection UI is not shown again; the player is stuck on an empty screen. |
| **No connection approval / matchmaking** | Any process that knows the IP can connect; there is no lobby, password, or session management. |
| **No game state or win/lose condition** | There is no campfire, resource, or health system. The survival loop does not exist yet. |
| **No scene transitions** | Only one scene exists; there is no main menu, loading screen, or end-game screen. |

### What was cut for scope
- **Asymmetric perspective** (first-person for The Gatherer, top-down or third-person management for The Keeper) was not implemented; both players share a third-person view.
- **Environmental hazards** (wildlife, weather) described in the design document are absent.
- **Inventory and resource systems** (dry wood, food ingredients) were not built.
- **Fire mechanics and heat levels** for The Keeper role were not implemented.
- **Unity AI Navigation** is listed as a dependency (`com.unity.ai.navigation 2.0.9`) but no NavMesh agents or AI-driven NPCs/wildlife exist in the scene.

### What would be added with more time
1. **Unity Relay + Lobby** — replace the localhost UTP address with a cloud relay so players on different networks can connect via a shareable room code.
2. **Connection approval + player roles** — assign The Keeper and The Gatherer roles on connection and spawn different prefabs accordingly.
3. **Server-authoritative movement** — switch to a `NetworkTransform` (server-owned) or add server-side position validation to prevent cheating.
4. **NetworkVariable-driven game state** — campfire heat, food stock, resource inventory, and player health would all be natural `NetworkVariable<float>` / `NetworkVariable<int>` fields on a `GameState` NetworkBehaviour.
5. **RPCs for events** — one-time events like "resource picked up", "fire extinguished", and "player death" would be implemented as `[ServerRpc]` calls that update authoritative state and broadcast results via `[ClientRpc]`.
6. **Scene management** — main menu → matchmaking lobby → gameplay → results screen, all handled by NGO's built-in `NetworkSceneManager`.
7. **Unity Input System** — migrate from legacy `Input.GetAxis` to the new Input System for proper per-device input isolation in split-screen and for future controller support.

---

*Report generated from source inspection of the Campbound Unity 6 project repository.*
