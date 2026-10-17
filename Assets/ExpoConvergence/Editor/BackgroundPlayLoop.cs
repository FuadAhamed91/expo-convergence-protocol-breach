using UnityEditor;
using UnityEditorInternal;

namespace ExpoConvergence.EditorTools
{
    /// <summary>
    /// Editor-only: keeps Play mode advancing while the Unity window is in the background, so the game
    /// can be driven and captured from tools (e.g. Unity MCP) without stealing focus.
    /// </summary>
    [InitializeOnLoad]
    static class BackgroundPlayLoop
    {
        static BackgroundPlayLoop()
        {
            EditorApplication.update += Tick;
        }

        static void Tick()
        {
            if (EditorApplication.isPlaying && !EditorApplication.isPaused && !InternalEditorUtility.isApplicationActive)
                EditorApplication.QueuePlayerLoopUpdate();
        }
    }
}
