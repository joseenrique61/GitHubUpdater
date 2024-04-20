using GitHubUpdater.Downloader;
using GitHubUpdater.Uploader;
using System;
using System.Diagnostics;
using System.Reflection;
using System.Threading.Tasks;
using System.Linq;

namespace GitHubUpdater
{
    internal class Program
    {
        [STAThread]
        static async Task Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("Operation must be specified.");
                return;
            }

            switch (args[0])
            {
                case "-admin":
                    StartAsAdmin(Assembly.GetExecutingAssembly().Location, args);
                    break;
                case "-upload":
                    await Upload(args);
                    break;
                case "-update":
                    await Update(args);
                    break;
                default:
                    Console.WriteLine("Operation not valid.");
                    break;
            }
        }

        private static async Task Upload(string[] args)
        {
            if (args.Length != 5)
            {
                Console.WriteLine("You must specify the Email, Token, local repo path and version.");
                return;
            }

            using (UpdateUploader updateUploader = new UpdateUploader(args[3])
                {
                    Email = args[1],
                    Token = args[2],
                    Version = new Version(args[4])
                })
            {
                await updateUploader.UploadChanges();
            }
        }

        private static async Task Update(string[] args)
        {
            if (args.Length < 8)
            {
                Console.WriteLine("You must specify the repo name, username, token, installation path, current version, app name and shortcuts paths.");
                return;
            }

            try
            {
                using (UpdateDownloader updateManager = await UpdateDownloader.Initialize(args[1], args[2], args[3], args[4], new Version(args[5]), args[6], args.ToList().GetRange(7, args.Length - 7)))
                {
                    await updateManager.Update();
                }
            }
            catch { }
        }

        public static void StartAsAdmin(string fileName, string[] args)
        {
            string arguments = "";
            for (int i = 1; i < args.Length; i++)
            {
                arguments += args[i] + " ";
            }
            arguments = arguments.TrimEnd();

            Process proc = new Process()
            {
                StartInfo = {
                    FileName = fileName,
                    UseShellExecute = true,
                    Verb = "runas",
                    Arguments = arguments,
                    WindowStyle = ProcessWindowStyle.Hidden
                }
            };

            proc.Start();
        }
    }
}
