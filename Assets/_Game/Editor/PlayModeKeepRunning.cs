using UnityEditor;
using UnityEngine;

namespace Survivor.EditorTools
{
    /// <summary>
    /// Keeps Play mode running when the Unity window loses focus,
    /// so the game can be tested from Claude (via MCP) without clicking back into Unity.
    /// </summary>
    [InitializeOnLoad]
    static class PlayModeKeepRunning
    {
        static PlayModeKeepRunning()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredPlayMode) Application.runInBackground = true;
            };
        }
    }
}
