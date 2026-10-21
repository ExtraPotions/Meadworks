using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace Workshop
{
    /// <summary>
    /// When the editor is started with -workshopOpenPreview, opens the creature preview scene and enters Play mode,
    /// once per editor session (so it doesn't fire again after the domain reload that Play mode causes).
    /// </summary>
    [InitializeOnLoad]
    public static class OpenPreviewOnStart
    {
        private const string Done = "workshopPreviewOpened";

        static OpenPreviewOnStart()
        {
            if (Environment.GetCommandLineArgs().Contains("-workshopOpenPreview") && !SessionState.GetBool(Done, false))
                EditorApplication.update += Open;
        }

        /// <summary>Waits until the editor has finished starting up (Play mode requests made earlier are dropped).</summary>
        private static void Open()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.timeSinceStartup < 5)
                return;
            EditorApplication.update -= Open;
            SessionState.SetBool(Done, true);
            if (!File.Exists(PreviewScene.ScenePath))
                return;
            EditorSceneManager.OpenScene(PreviewScene.ScenePath);
            EditorApplication.EnterPlaymode();
        }
    }
}
