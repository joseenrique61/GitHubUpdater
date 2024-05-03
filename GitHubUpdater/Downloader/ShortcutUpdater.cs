using System.IO;
using System.Runtime.InteropServices;

namespace GitHubUpdater.Downloader
{
    public static class ShortcutUpdater
    {
        public static void UpdateShortcut(string shortcutFullPath, string newTarget, string appName, string installationPath)
        {
            if (!File.Exists(shortcutFullPath))
            {
                return;
            }

            string shortcutTempPath = $@"{installationPath}\{appName}.lnk";

            var wshShell = new IWshRuntimeLibrary.WshShell();
            try
            {
                IWshRuntimeLibrary.WshShortcut shortcut = wshShell.CreateShortcut(shortcutTempPath);

                try
                {
                    shortcut.TargetPath = newTarget;
                    shortcut.IconLocation = $@"{installationPath}\app.ico";
                    shortcut.Save();
                    MoveShortcut(shortcutFullPath, appName, shortcutTempPath);
                }
                finally
                {
                    Marshal.FinalReleaseComObject(shortcut);
                }
            }
            finally
            {
                Marshal.FinalReleaseComObject(wshShell);
            }
        }

        private static void MoveShortcut(string shortcutFullPath, string appName, string shortcutTempPath)
        {
            File.Delete(shortcutFullPath);
            File.Move(shortcutTempPath, $@"{Path.GetDirectoryName(shortcutFullPath)}\{appName}.lnk");
        }
    }
}
