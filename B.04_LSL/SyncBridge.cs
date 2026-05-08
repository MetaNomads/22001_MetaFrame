// SyncBridge.cs — DEPRECATED, kept as a stub for backwards compatibility.
//
// The original LabStreamLayer SyncBridge.cs ran its own UDP listener on port
// 12345. In this project that role is owned by LSLService.cs (in
// MetaFrame.Data) which auto-bootstraps via [InitializeOnLoad] /
// [RuntimeInitializeOnLoadMethod] and handles DISCOVER, CONNECT, ping_NNN,
// __calib_*, REQUEST_DATA, and now SUBJECT_ID:, CMD:, STATE_REQ as well.
//
// Two UDP listeners cannot coexist on the same port — whichever bound second
// fails silently. So this file is intentionally inert. Do NOT add it to a
// scene GameObject; if you do, the (empty) MonoBehaviour will not collide
// with LSLService but it also won't do anything useful.
//
// Code that previously called:
//     SyncBridge.OnHostMessage += handler;
//     SyncBridge.SendToHost(msg);
//     SyncBridge.SendPing();
// should now use:
//     MetaFrame.Data.LSLService.OnHostMessage += handler;
//     MetaFrame.Data.LSLService.SendToHost(msg);
//     (SendPing is intentionally not provided — LSL host drives ping cadence.)
//
// This file can be deleted from the project. It is kept as a stub so any
// stale meta files Unity has cached don't generate "missing script" warnings
// on existing scene objects until the user has a chance to remove them.

using UnityEngine;

public class SyncBridge : MonoBehaviour
{
    void Awake()
    {
        Debug.LogWarning(
            "[SyncBridge] This component is deprecated. UDP duties are owned " +
            "by MetaFrame.Data.LSLService (auto-bootstrapped on play). " +
            "Remove this component from the GameObject.");
        // Disable so even Update / OnEnable never run on this instance.
        enabled = false;
    }
}
