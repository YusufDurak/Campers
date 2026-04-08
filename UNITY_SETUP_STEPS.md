# Unity Netcode for GameObjects — Setup Guide

Follow every step in order. When you finish, you will be able to **Build & Run** a second instance and see two capsules moving independently.

---

## 1. Install Netcode for GameObjects

1. Open **Window → Package Manager**.
2. In the top-left dropdown, select **Unity Registry**.
3. Search for **Netcode for GameObjects**.
4. Select it and click **Install** (this also pulls in `Unity Transport` automatically).
5. Wait for compilation to finish.

> If `Unity Transport` does **not** appear after installation, search for it in the Package Manager and install it manually.

---

## 2. Create the NetworkManager

1. In the **Hierarchy**, right-click → **Create Empty**. Name it `NetworkManager`.
2. Select it, then in the Inspector click **Add Component** and add:
   - **NetworkManager** (from `Unity.Netcode`)
   - **Unity Transport** (from `Unity.Netcode.Transports.UTP`)
3. On the **NetworkManager** component, set the **Network Transport** field by dragging the `Unity Transport` component into it (or use the object picker).

---

## 3. Create the Player Prefab

### 3a. Build the Capsule

1. In the Hierarchy, right-click → **3D Object → Capsule**. Name it `Player`.
2. Reset its Transform (Position `0, 1, 0` so it sits on the ground plane).

### 3b. Add Networking Components

With the `Player` object selected, add these components via **Add Component**:

| Component | Purpose |
|---|---|
| **Network Object** | Registers the object with Netcode |
| **Client Network Transform** | Replicates position and rotation — owner authoritative |

> **Do NOT use plain `NetworkTransform` here.** The default `NetworkTransform` is server-authoritative, which means the server overrides the client's position every frame and the client cannot move their own character. `ClientNetworkTransform` (also included with NGO, no extra install) lets the owning client push their own position to everyone else.

### 3c. Attach the Movement and Camera Scripts

1. Still on the `Player` object, click **Add Component → Scripts → PlayerMovement** (or search for it).
2. Click **Add Component → Scripts → ThirdPersonCamera** and add it too.
3. Adjust `Move Speed`, `Rotation Speed`, and camera `Offset` / `Smooth Speed` in the Inspector if desired.

> `ThirdPersonCamera` creates each player's camera at runtime and only activates it for the owner, so no Camera child is needed in the prefab.

### 3d. Save as Prefab

1. In the **Project** window, create a folder called `Assets/Prefabs` (right-click in Project → Create → Folder).
2. **Drag** the `Player` object from the Hierarchy into the `Prefabs` folder. A prefab asset is created.
3. **Delete** the `Player` object from the Hierarchy (the prefab is what Netcode will spawn at runtime).

### 3e. Register the Prefab with NetworkManager

1. Select the `NetworkManager` object in the Hierarchy.
2. In the **NetworkManager** component, find **Player Prefab** and drag the `Player` prefab from `Assets/Prefabs` into that slot.
3. Expand **Network Prefabs List** and verify the Player prefab is listed. If it is not, click **+**, then drag the prefab into the new entry.

---

## 4. Set Up the Connection UI

### 4a. Create the Canvas

1. In the Hierarchy, right-click → **UI → Canvas**. This also creates an **EventSystem** object automatically.
2. On the Canvas, set **UI Scale Mode** (Canvas Scaler) to **Scale With Screen Size**, reference resolution `1920 × 1080`.

### 4b. Fix the EventSystem (Legacy Input Manager)

If you are **not** using Unity's new Input System package, the EventSystem needs the legacy input module:

1. Select the **EventSystem** object in the Hierarchy.
2. If you see a **missing script** warning on any component, remove that broken component (right-click → Remove Component).
3. Make sure the EventSystem has a **Standalone Input Module** component. If it does not, click **Add Component** and search for `StandaloneInputModule` and add it.

> Without this, no UI buttons will respond to clicks.

### 4c. Add the NetworkUI Script

1. On the **Canvas** object (or create an empty child called `ConnectionUI`), click **Add Component → Scripts → NetworkUI**.

### 4d. Create the Host Button

1. Right-click the Canvas → **UI → Button – TextMeshPro** (or **UI → Button** if not using TMP).
2. Rename it `HostButton`.
3. Set its anchored position to the **upper-left** area (e.g., Pos X `−780`, Pos Y `460`).
4. Change the button's child text to **Host**.
5. In the Button's **On Click ()** event:
   - Click **+**.
   - Drag the object that has the `NetworkUI` component into the Object slot.
   - From the function dropdown, choose **NetworkUI → StartHost**.

### 4e. Create the Client Button

1. Duplicate `HostButton` (Ctrl+D) and rename the copy `ClientButton`.
2. Move it slightly below the Host button (e.g., Pos Y `400`).
3. Change its text to **Client**.
4. In its **On Click ()** event, pick **NetworkUI → StartClient** instead.

---

## 5. Quick Test (Same Machine)

1. **File → Build Settings** → add the open scene → **Build And Run**.
2. In the built player window, click **Host**.
3. Back in the Unity Editor, enter **Play Mode** and click **Client**.
4. You should see two capsules. Moving with WASD/Arrow keys in one window should replicate to the other.

---

## Troubleshooting

| Problem | Fix |
|---|---|
| Buttons do nothing | Make sure the `NetworkUI` component is on the object referenced in each button's On Click event. Also check the EventSystem has a **Standalone Input Module** (not a missing script). See step 4b. |
| Second player never spawns | Verify the Player Prefab slot on `NetworkManager` is assigned **and** the prefab has a `NetworkObject` component. |
| Client can't move / snaps back | Replace `NetworkTransform` with **Client Network Transform** on the Player prefab. The default `NetworkTransform` is server-authoritative and overrides client movement. |
| Movement not replicated to others | Confirm the prefab has a `ClientNetworkTransform` component (not plain `NetworkTransform`). |
| Client can't connect | Both instances must use the same IP/port. Default is `127.0.0.1:7777` (localhost). Check `Unity Transport` settings on the `NetworkManager`. |
