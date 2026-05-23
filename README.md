# Campbound

A minimal co-op survival prototype built in Unity with Netcode for GameObjects.

This project was developed as the final submission for the Multiplayer Game Development course. The original concept was a 3D first-person asymmetric co-op survival game with complex resource management and environmental threats. During development we hit serious scope problems and had to cut most of the original features. What you see in this repository is the trimmed-down, working prototype that still demonstrates the networking requirements asked for in the assignment.

---

## What the Game Does

Two players connect (one as Host, one as Client) and join a lobby. When both players press Ready (R key), the game loads the main scene. The goal is simple: keep the campfire burning for 3 minutes.

- The campfire's heat decreases over time.
- Players collect wood objects scattered around the map.
- Players bring the wood to the campfire and add it to keep the heat above zero.
- If the fire heat drops to 0, both players lose.
- If 3 minutes pass with the fire still burning, both players win.

Both players see all changes in real time. When one player picks up a wood object, it disappears for the other player too. When the heat changes, both UIs update simultaneously.

---

## Assignment Requirements Checklist

- **Working Host/Client connection:** Yes. Lobby scene has Host and Client buttons.
- **NetworkVariable in use:** Yes. `FireHeat`, `MatchTimer`, `GameState`, `HostReady`, `ClientReady`, `WoodCount` are all NetworkVariables.
- **RPC in use:** Yes. `SetReadyServerRpc`, `AddWoodServerRpc`, `PickupWoodServerRpc`, `AddWoodToFireServerRpc`, `WoodAddedToFireClientRpc`, `EndGameClientRpc`.
- **Real-time synchronization:** Yes. All player movements, wood pickups, fire heat changes, and game state changes are visible to all connected players.
- **Clear win/end condition:** Yes. Win if the fire survives 3 minutes. Lose if the fire heat reaches 0.
- **Simple lobby/ready system:** Yes. After connecting, both players must press R to ready up. The game starts when both are ready.

---

## How to Run

1. Open the project in Unity 6000.3.6f1 or later.
2. Open the `LobbyScene` from `Assets/Scenes/`.
3. Build & Run for the host machine.
4. In the built application, click Host.
5. In the Editor (or a second build instance), click Client.
6. Both players press R to ready up.
7. The game scene loads automatically when both are ready.

For local testing, ParrelSync was used to run two Editor instances on the same machine.

---

## Project Structure

```
Assets/
├── Scenes/
│   ├── LobbyScene.unity
│   └── GameScene.unity
├── Scripts/
│   ├── Network/
│   │   ├── GameManager.cs
│   │   ├── LobbyManager.cs
│   │   └── NetworkUI.cs
│   ├── Player/
│   │   ├── PlayerMovement.cs
│   │   ├── PlayerInventory.cs
│   │   ├── PlayerInteraction.cs
│   │   ├── ThirdPersonCamera.cs
│   │   └── ClientNetworkTransform.cs
│   ├── Gameplay/
│   │   ├── Campfire.cs
│   │   ├── WoodPickup.cs
│   │   └── WoodSpawner.cs
│   └── UI/
│       ├── LobbyUI.cs
│       ├── GameHUD.cs
│       └── EndPanelUI.cs
└── Prefabs/
    ├── Player.prefab
    └── Wood.prefab
```

---

## Built With

- Unity 6000.3.6f1
- Netcode for GameObjects
- Unity Transport (UTP)
- TextMeshPro
- ParrelSync (for local multiplayer testing)