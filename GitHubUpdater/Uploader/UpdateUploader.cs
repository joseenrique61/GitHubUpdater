using LibGit2Sharp;
using System.IO;
using System.Threading.Tasks;
using System;

namespace GitHubUpdater.Uploader
{
    public class UpdateUploader : IDisposable
    {
        private bool disposed = false;

        private Repository Repository { get; set; }

        public string Email { private get; set; }

        public string Token { private get; set; }

        private string LocalRepoPath { get; set; }

        public System.Version Version { private get; set; }

        public UpdateUploader(string localRepoPath)
        {
            Repository = new Repository(localRepoPath);
            LocalRepoPath = localRepoPath;
        }

        public async Task UploadChanges()
        {
            await Task.Run(async () =>
            {
                CreateVersionFile();
                await StageChanges();
                await CommitChanges();
                await PushToGithub();
            });
        }

        private void CreateVersionFile()
        {
            string file = $@"{Path.GetDirectoryName(LocalRepoPath)}\version.txt";

            File.WriteAllText(file, Version.ToString());
        }

        private async Task StageChanges()
        {
            await Task.Run(() =>
            {
                try
                {
                    Commands.Stage(Repository, "*");
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
            });
        }

        private async Task CommitChanges()
        {
            await Task.Run(() =>
            {
                try
                {
                    Signature signature = new Signature(Email, Email, DateTimeOffset.Now);
                    Repository.Commit("Update version.", signature, signature);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
            });
        }

        private async Task PushToGithub()
        {
            await Task.Run(() =>
            {
                try
                {
                    Remote remote = Repository.Network.Remotes["origin"];
                    PushOptions options = new PushOptions()
                    {
                        CredentialsProvider = (url, usernameFromUrl, types) => new UsernamePasswordCredentials()
                        {
                            Username = Email,
                            Password = Token
                        }
                    };

                    string pushRefSpec = @"refs/heads/main";
                    Repository.Network.Push(remote, pushRefSpec, options);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
            });
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposed)
            {
                Repository.Dispose();
                Email = null;
                Token = null;
                LocalRepoPath = null;

                disposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        ~UpdateUploader()
        {
            Dispose(disposing: false);
        }
    }
}
