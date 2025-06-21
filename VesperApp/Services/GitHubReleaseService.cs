using Octokit;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace VesperApp.Services
{
    /// <summary>
    /// Service for interacting with GitHub releases using Octokit.
    /// </summary>
    public class GitHubReleaseService
    {
        private readonly GitHubClient _client;
        private readonly string _owner;
        private readonly string _repo;

        /// <summary>
        /// Initializes the service with authentication and repository info.
        /// </summary>
        /// <param name="token">Personal access token for GitHub (with repo access).</param>
        /// <param name="owner">Repository owner (user or org).</param>
        /// <param name="repo">Repository name.</param>
        public GitHubReleaseService(string token, string owner, string repo)
        {
            _owner = owner;
            _repo = repo;
            _client = new GitHubClient(new ProductHeaderValue("VesperApp"))
            {
                Credentials = new Credentials(token)
            };
        }

        /// <summary>
        /// Gets all releases for the configured repository.
        /// </summary>
        public async Task<IReadOnlyList<Release>> GetReleasesAsync()
        {
            return await _client.Repository.Release.GetAll(_owner, _repo);
        }

        /// <summary>
        /// Gets assets for a specific release.
        /// </summary>
        /// <param name="releaseId">The ID of the release.</param>
        public async Task<IReadOnlyList<ReleaseAsset>> GetAssetsForReleaseAsync(long releaseId)
        {
            return await _client.Repository.Release.GetAllAssets(_owner, _repo, releaseId);
        }

        /// <summary>
        /// Downloads a release asset to a local file.
        /// </summary>
        /// <param name="asset">The asset to download.</param>
        /// <param name="destinationPath">Local file path to save the asset.</param>
        public async Task DownloadAssetAsync(ReleaseAsset asset, string destinationPath)
        {
            var response = await _client.Connection.Get<object>(new Uri(asset.BrowserDownloadUrl), new Dictionary<string, string>(), "application/octet-stream");
            if (response.HttpResponse.StatusCode == System.Net.HttpStatusCode.OK)
            {
                using var fileStream = File.Create(destinationPath); // Ensure the file is created before writing
                
                if (response.HttpResponse.Body != null)
                {
                    Stream s = (Stream)(response.HttpResponse.Body);
                    s.CopyTo(fileStream);
                }
                fileStream.Flush(); // Ensure all data is written to the file
                fileStream.Close(); // Close the stream to release the file handle
            }
            else
            {
                throw new Exception($"Failed to download asset: {asset.Name}");
            }
        }
    }
}