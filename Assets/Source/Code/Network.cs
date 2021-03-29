using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace mofrison.Network
{
    public static class Network
    {
        public static async Task<AssetBundle> RequestBundle(string url, CancellationTokenSource cancelationToken, System.Action<float> progress = null)
        {
            UnityWebRequest request;
            CachedAssetBundle cachedAssetBundle = await GetCachedAssetBundle(new System.Uri(url));
            if (!Caching.IsVersionCached(cachedAssetBundle))
            {
                float fileSize = await RequestSize(url);
                if (!ResourceCache.CheckFreeSpace(fileSize))
                {
                    Debug.LogWarning("[Netowrk] error: Not available space to download " + fileSize / ResourceCache.MIB + "Mb");
                    request = UnityWebRequestAssetBundle.GetAssetBundle(url);
                }
                else
                {
                    request = UnityWebRequestAssetBundle.GetAssetBundle(url, cachedAssetBundle, 0);
                }
            }
            else {
                request = UnityWebRequestAssetBundle.GetAssetBundle(url, cachedAssetBundle, 0);
                progress = null; 
            }

            UnityWebRequest uwr = await WebRequest(request, cancelationToken, progress);
            if (uwr != null && !uwr.isHttpError && !uwr.isNetworkError)
            {
                AssetBundle assetBundle = DownloadHandlerAssetBundle.GetContent(uwr);
                // Deleting old versions from the cache
                Caching.ClearOtherCachedVersions(assetBundle.name, cachedAssetBundle.hash);
                return assetBundle;
            }
            else
            {
                throw new Exception("[Netowrk] error: " + uwr.error + " " + uwr.uri);
            }
        }

        public static async Task<Texture2D> RequestTexture(string url, CancellationTokenSource cancelationToken, System.Action<float> progress = null)
        {
            UnityWebRequest request;
            string path = url.GetCachedPath();
            if (path != null) { request = UnityWebRequestTexture.GetTexture("file://" + path); progress = null; }
            else { request = UnityWebRequestTexture.GetTexture(url); }

            UnityWebRequest uwr = await WebRequest(request, cancelationToken, progress);
            if (uwr != null && !uwr.isHttpError && !uwr.isNetworkError)
            {
                Texture2D texture = DownloadHandlerTexture.GetContent(uwr);
                texture.name = Path.GetFileName(uwr.url);

                ResourceCache.Caching(uwr.url, uwr.downloadHandler.data);
                return texture;
            }
            else
            {
                throw new Exception("[Netowrk] error: " + uwr.error + " " + uwr.uri);
            }
        }

        public static async Task<AudioClip> RequestAudioClip(string url, CancellationTokenSource cancelationToken, System.Action<float> progress = null, AudioType audioType = AudioType.OGGVORBIS)
        {
            UnityWebRequest request;
            string path = url.GetCachedPath();
            if (path != null) { request = UnityWebRequestMultimedia.GetAudioClip("file://" + path, audioType); progress = null; }
            else { request = UnityWebRequestMultimedia.GetAudioClip(url, audioType); }

            UnityWebRequest uwr = await WebRequest(request, cancelationToken, progress);
            if (uwr != null && !uwr.isHttpError && !uwr.isNetworkError)
            {
                AudioClip audioClip = DownloadHandlerAudioClip.GetContent(uwr);
                audioClip.name = Path.GetFileName(uwr.url);

                ResourceCache.Caching(uwr.url, uwr.downloadHandler.data);
                return audioClip;
            }
            else
            {
                throw new Exception("[Netowrk] error: " + uwr.error + " " + uwr.uri);
            }
        }

        public static string RequestVideoStream(string url, CancellationTokenSource cancelationToken, System.Action<float> progress = null)
        {
            string path = url.GetCachedPath();
            if (path != null) {  return path; }
            else
            {
                CachingVideo(url, cancelationToken, progress);
                return url;
            }
        }

        public static async void CachingVideo(string url, CancellationTokenSource cancelationToken, System.Action<float> progress = null)
        {
            ResourceCache.Caching(url, await RequestData(url, cancelationToken, progress));
        }

        public static async Task<byte[]> RequestData(string url, CancellationTokenSource cancelationToken, System.Action<float> progress = null)
        {
            UnityWebRequest request = UnityWebRequest.Get(url);
            UnityWebRequest uwr = await WebRequest(request, cancelationToken, progress);
            if (uwr != null && !uwr.isHttpError && !uwr.isNetworkError)
            {
                return (uwr.downloadHandler.data);
            }
            else
            {
                throw new Exception("[Netowrk] error: " + uwr.error + " " + uwr.uri);
            }
        }

        private static async Task<UnityWebRequest> WebRequest(UnityWebRequest request, CancellationTokenSource cancelationToken = null, System.Action<float> progress = null)
        {
            while (!Caching.ready)
            {
                if (cancelationToken != null && cancelationToken.IsCancellationRequested)
                {
                    return null;
                }
                await Task.Yield();
            }

#pragma warning disable CS4014
            request.SendWebRequest();
#pragma warning restore CS4014

            while (!request.isDone)
            {
                if (cancelationToken != null && cancelationToken.IsCancellationRequested)
                {
                    request.Abort();
                    request.Dispose();

                    return null;
                }
                else
                {
                    progress?.Invoke(request.downloadProgress);
                    await Task.Yield();
                }
            }

            progress?.Invoke(1f);
            return request;
        }

        private static async Task<int> RequestSize(string url)
        {
            UnityWebRequest request = await WebRequest(UnityWebRequest.Head(url));
            var contentLength = request.GetResponseHeader("Content-Length");
            if (int.TryParse(contentLength, out int returnValue))
            {
                return returnValue;
            }
            else
            {
                throw new Exception("[Netowrk] error: " + request.error + " " + url);
            }
        }

        private static async Task<string> RequestText(string url)
        {
            var uwr = await WebRequest(UnityWebRequest.Get(url));
            if (uwr != null && !uwr.isHttpError && !uwr.isNetworkError)
            {
                return uwr.downloadHandler.text;
            }
            else
            {
                Debug.LogWarning("[Netowrk] error: " + uwr.error + " " + uwr.url);
                return null;
            }
        }

        private static Hash128 GetHashFromManifest(string manifest)
        {
            var hashRow = manifest.Split("\n".ToCharArray())[5];
            var hash = Hash128.Parse(hashRow.Split(':')[1].Trim());

            return hash;
        }

        private static async Task<CachedAssetBundle> GetCachedAssetBundle(System.Uri uri)
        {
            Hash128 hash = default;
            string manifest = await RequestText(uri + ".manifest");

            if (!string.IsNullOrEmpty(manifest))
            {
                hash = GetHashFromManifest(manifest);
                return new CachedAssetBundle(uri.LocalPath, hash);
            }
            else
            {
                DirectoryInfo dir = new DirectoryInfo(uri.ToString().ConvertToLocalPath());
                if (dir.Exists)
                {
                    System.DateTime lastWriteTime = default;
                    foreach (var item in dir.GetDirectories())
                    {
                        if (lastWriteTime < item.LastWriteTime)
                        {
                            if (hash.isValid && hash != default) Directory.Delete(Path.Combine(dir.FullName, hash.ToString()), true);
                            lastWriteTime = item.LastWriteTime;
                            hash = Hash128.Parse(item.Name);
                        }
                        else { Directory.Delete(Path.Combine(dir.FullName, item.Name), true); }
                    }
                    return new CachedAssetBundle(uri.LocalPath, hash);
                }
                else
                {
                    throw new Exception("[Netowrk] error: Nothing was found in the cache for " + uri);
                }
            }
        }

        public class Exception : System.Exception
        {
            public Exception(string message) : base(message)
            { }
        }
    }
}