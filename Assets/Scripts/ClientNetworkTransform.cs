using Unity.Netcode.Components;
using UnityEngine;

/// <summary>
/// Owner-authoritative NetworkTransform.
/// The owning client drives its own position/rotation and replicates to everyone else.
/// Drop this on the Player prefab instead of the default NetworkTransform.
/// </summary>
[DisallowMultipleComponent]
public class ClientNetworkTransform : NetworkTransform
{
    protected override bool OnIsServerAuthoritative() => false;
}
