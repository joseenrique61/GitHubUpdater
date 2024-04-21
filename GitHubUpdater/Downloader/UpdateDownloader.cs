using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using System.Linq;

namespace GitHubUpdater.Downloader
{
    public class UpdateDownloader : IDisposable
    {
        private bool disposed = false;

        private readonly JsonSerializerOptions jsonOptions = new JsonSerializerOptions() { PropertyNameCaseInsensitive = true };

        private bool IsInCorrectPath { get; set; } = true;

        public string InstallationPath { private get; set; }

        private string RepoName { get; set; }

        private string Username { get; set; }

        private string Token { get; set; }

        private HttpClient Client { get; set; }

        private Version CurrentVersion { get; set; }

        private Version NewVersion { get; set; }

        private string NewPath { get; set; }

        private List<FileInformation> LocalFiles { get; set; }

        private List<FileInformation> GitHubFiles { get; set; }

        private List<string> ShortcutPaths { get; set; }

        private string AppName { get; set; }

        private UpdateDownloader() { }

        public static async Task<UpdateDownloader> Initialize(string repoName, string username, string token, string installationPath, Version currentVersion, string appName, List<string> shortcutPaths)
        {
            UpdateDownloader manager = new UpdateDownloader()
            {
                RepoName = repoName,
                Username = username,
                Token = token,
                InstallationPath = installationPath,
                CurrentVersion = currentVersion,
                AppName = appName.Replace(".exe", ""),
                ShortcutPaths = shortcutPaths
            };

            manager.Client = await manager.GetClient();
            manager.SetLocalFiles();

            if (manager.IsInCorrectPath)
            {
                manager.CurrentVersion = new Version($@"{Path.GetFileName(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location))}");
            }

            return manager;
        }

        private async Task<HttpClient> GetClient()
        {
            HttpClient client = new HttpClient();
            await Task.Run(() =>
            {
                client.BaseAddress = new Uri("https://api.github.com");
                client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Updater", GetUpdaterVersion().ToString()));
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Token", Token);
            });
            return client;
        }

        private static Version GetUpdaterVersion()
        {
            return AssemblyName.GetAssemblyName(Assembly.GetExecutingAssembly().GetName().Name + ".dll").Version;
        }

        public async Task Update()
        {
            if (!await CheckUpdates())
            {
                return;
            }

            CreateNewDirectory();

            SetFileStates();
            await UpdateFiles();

            await CreateHashesFile();
            UpdateShortcuts();

            DeleteOldVersion();
        }

        private void SetFileStates()
        {
            if (!IsInCorrectPath)
            {
                LocalFiles = GitHubFiles;
                LocalFiles.ForEach(x => x.FileState = FileState.NEW);
                return;
            }

            foreach (FileInformation file in LocalFiles)
            {
                if (!GitHubFiles.Any(x => x.Path == file.Path))
                {
                    file.FileState = FileState.OLD;
                    continue;
                }

                FileInformation gitHubFile = GitHubFiles.Find(x => x.Path == file.Path);
                if (gitHubFile.Sha != file.Sha)
                {
                    file.DownloadUrl = gitHubFile.DownloadUrl;
                    file.Sha = gitHubFile.Sha;
                    file.FileState = FileState.UPDATED;
                }
            }

            foreach (FileInformation file in GitHubFiles)
            {
                if (!LocalFiles.Any(x => x.Path == file.Path))
                {
                    file.FileState = FileState.NEW;
                    LocalFiles.Add(file);
                    continue;
                }
            }
        }

        private async Task UpdateFiles()
        {
            foreach (FileInformation file in LocalFiles)
            {
                switch (file.FileState)
                {
                    case FileState.NEW:
                    case FileState.UPDATED:
                        await DownloadFile(file);
                        break;
                    case FileState.SAME:
                        CopyFile(file);
                        break;
                }
            }
        }

        private async Task DownloadFile(FileInformation file)
        {
            Directory.CreateDirectory($@"{NewPath}\{Path.GetDirectoryName(file.Path)}");

            string path = $@"{NewPath}\{file.Path}";

            if (File.Exists(path))
            {
                File.Delete(path);
            }

            File.WriteAllText(path, await Client.GetStringAsync(file.DownloadUrl));
        }

        private void CopyFile(FileInformation file)
        {
            Directory.CreateDirectory($@"{NewPath}\{Path.GetDirectoryName(file.Path)}");
            File.Copy($@"{InstallationPath}\{CurrentVersion}\{file.Path}", $@"{InstallationPath}\{NewVersion}\{file.Path}");
        }

        private static void UpdateShortcut(string shortcutFullPath, string newTarget)
        {
            if (!File.Exists(shortcutFullPath))
            {
                return;
            }

            Guid CLSID_Shell = Guid.Parse("13709620-C279-11CE-A49E-444553540000");
            dynamic shell = Activator.CreateInstance(Type.GetTypeFromCLSID(CLSID_Shell));
            Shell32.Folder folder = shell.NameSpace(Path.GetDirectoryName(shortcutFullPath));
            Shell32.FolderItem folderItem = folder.Items().Item(Path.GetFileName(shortcutFullPath));

            Shell32.ShellLinkObject currentLink = (Shell32.ShellLinkObject)folderItem.GetLink;

            currentLink.Path = newTarget;
            currentLink.WorkingDirectory = Path.GetDirectoryName(newTarget);

            currentLink.Save();
        }

        private async Task CreateHashesFile()
        {
            using (FileStream fileStream = File.Create($@"{NewPath}\hashes.txt"))
            {
                await JsonSerializer.SerializeAsync(fileStream, LocalFiles.Where(x => x.FileState != FileState.OLD));
            }
        }

        private void UpdateShortcuts()
        {
            foreach (string shortcut in ShortcutPaths)
            {
                UpdateShortcut(shortcut, $@"{NewPath}\{AppName}.exe");
            }
        }

        private void DeleteOldVersion()
        {
            foreach (string path in Directory.GetDirectories(InstallationPath))
            {
                string directoryName = Path.GetFileName(path).ToString();

                if (!Version.TryParse(directoryName, out Version version) || version >= CurrentVersion)
                {
                    continue;
                }

                Directory.Delete(path, true);
            }
        }

        private async Task<bool> CheckUpdates()
        {
            string content = await GetGitHubContents();
            await SetGitHubFiles(content);

            NewVersion = await GetRemoteAppVersion();
            return NewVersion > CurrentVersion;
        }

        private async Task SetGitHubFiles(string content)
        {
            GitHubFiles = JsonSerializer.Deserialize<List<FileInformation>>(content, jsonOptions);
            GitHubFiles = await GetGitHubFilesInFolders(GitHubFiles);
        }

        private async Task<List<FileInformation>> GetGitHubFilesInFolders(List<FileInformation> files)
        {
            List<FileInformation> files1 = new List<FileInformation>();
            foreach (FileInformation file in files)
            {
                if (file.FileType == FileType.dir)
                {
                    string content = await Client.GetStringAsync(file.Url);

                    files1.AddRange(JsonSerializer.Deserialize<List<FileInformation>>(content, jsonOptions));
                }
            }

            files.AddRange(files1);
            return files.Where(x => x.FileType == FileType.file).ToList();
        }

        private async Task<Version> GetRemoteAppVersion()
        {
            return Version.Parse(await Client.GetStringAsync(GitHubFiles.Find(x => x.Path == "version.txt").DownloadUrl));
        }

        private async Task<string> GetGitHubContents()
        {
            string content = "";

            await Task.Run(async () =>
            {
                Uri uri = new Uri($"repos/{Username}/{RepoName}/contents", UriKind.Relative);
                content = await Client.GetStringAsync(uri);
            });

            return content;
        }

        private void CreateNewDirectory()
        {
            NewPath = Directory.CreateDirectory($@"{InstallationPath}\{NewVersion}").FullName;
        }

        private void SetLocalFiles()
        {
            try
            {
                using (StreamReader reader = new StreamReader($@"{InstallationPath}\{CurrentVersion}\hashes.txt"))
                {
                    LocalFiles = JsonSerializer.Deserialize<List<FileInformation>>(reader.ReadToEnd(), jsonOptions);
                }
            }
            catch (Exception)
            {
                IsInCorrectPath = false;
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposed)
            {
                Client.Dispose();
                RepoName = null;
                Username = null;
                Token = null;

                disposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        ~UpdateDownloader()
        {
            Dispose(disposing: false);
        }
    }
}
