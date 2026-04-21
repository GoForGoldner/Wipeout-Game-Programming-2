using UnityEngine;

/// <summary>
/// Static overhead spectator camera. Drop this GameObject into each level scene,
/// position it looking down at the playable area, and assign it to GameManager's
/// `spectatorCamera` field. GameManager enables it on eliminated clients.
///
/// This is the simplest option — no logic, no following. Just a good overhead view.
/// Attach to a GameObject with a Camera component.
/// </summary>
public class SpectatorCameraStatic : MonoBehaviour
{
    // Nothing to do. The camera just sits where you placed it.
    // Script exists so you can add tweaks later (slow rotation, shake, etc.).
}
