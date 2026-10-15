using System.IO;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEngine;

namespace FartRaft.EditorTools
{
    // Adds Unity Relay so two phones can share a room code. The game code stays
    // off until the packages are actually installed, so a normal race still compiles.
    [InitializeOnLoad]
    public static class FartRaftOnlineInstaller
    {
        const string Define = "FART_RAFT_ONLINE";

        static FartRaftOnlineInstaller()
        {
            Events.registeredPackages += _ => EditorApplication.delayCall += TryEnable;
            EditorApplication.delayCall += TryEnable;
        }

        [MenuItem("Fart Raft/4. Install Online Play")]
        public static void Install()
        {
            if (!PackagesReady())
            {
                Client.Add("com.unity.services.authentication");
                Client.Add("com.unity.services.relay");
                Client.Add("com.unity.transport");
            }
            EditorUtility.DisplayDialog(
                "Fart Raft online",
                "Unity is installing Relay. When the progress bar finishes, online play turns on by itself.\n\nThen, once: Edit > Project Settings > Services, link this project, and turn Relay on in the Unity dashboard.\n\nIn the lobby, tap ONLINE. One phone taps HOST and reads the code. The other types it and taps JOIN.",
                "OK");
            TryEnable();
        }

        static bool PackagesReady()
        {
            string cache = Path.Combine(Directory.GetCurrentDirectory(), "Library/PackageCache");
            if (!Directory.Exists(cache)) return false;
            bool relay = false, auth = false, transport = false;
            foreach (var dir in Directory.GetDirectories(cache))
            {
                string name = Path.GetFileName(dir);
                if (name.StartsWith("com.unity.services.relay@")) relay = true;
                if (name.StartsWith("com.unity.services.authentication@")) auth = true;
                if (name.StartsWith("com.unity.transport@")) transport = true;
            }
            return relay && auth && transport;
        }

        static void TryEnable()
        {
            if (!PackagesReady()) return;
            foreach (var group in new[] { BuildTargetGroup.Standalone, BuildTargetGroup.Android, BuildTargetGroup.iOS })
            {
                string defines = PlayerSettings.GetScriptingDefineSymbolsForGroup(group);
                if (defines.Contains(Define)) continue;
                if (defines.Length > 0) defines += ";";
                defines += Define;
                PlayerSettings.SetScriptingDefineSymbolsForGroup(group, defines);
            }
        }
    }
}
