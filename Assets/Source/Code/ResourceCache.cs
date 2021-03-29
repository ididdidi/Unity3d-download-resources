using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace mofrison.Network
{
    public static class ResourceCache
    {
        public const float MIB = 1048576f;
        public static string cachingDirectory = "data";
        public static void ConfiguringCaching(string directoryName)
        {
            cachingDirectory = directoryName;
            var path = Path.Combine(Application.persistentDataPath, cachingDirectory);
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
            UnityEngine.Caching.currentCacheForWriting = UnityEngine.Caching.AddCache(path);
        }

        public static string GetCachedPath(this string url) {
            string path = url.ConvertToLocalPath();
            if (File.Exists(path)) return path;
            else return null;
        }

        public static void Caching(string url, byte[] data)
        {
            if (url.Contains("file://")) return;

            if (CheckFreeSpace(data.Length))
            {
                string path = url.ConvertToLocalPath();

                DirectoryInfo dirInfo = new DirectoryInfo(Application.persistentDataPath);
                if (!dirInfo.Exists)
                {
                    dirInfo.Create();
                }
                dirInfo.CreateSubdirectory(Directory.GetParent(path).FullName);
                File.WriteAllBytes(path, data);
            }
            else { throw new Exception("[Caching] error: Not available space to download " + data.Length / MIB + "Mb"); }
        }

        public static bool CheckFreeSpace(float sizeInBytes) // 1048576f
        {
#if UNITY_EDITOR_WIN
            var logicalDrive = Path.GetPathRoot(Application.persistentDataPath);
            var availableSpace = SimpleDiskUtils.DiskUtils.CheckAvailableSpace(logicalDrive);
#elif UNITY_EDITOR_OSX
        var availableSpace = SimpleDiskUtils.DiskUtils.CheckAvailableSpace();
#elif UNITY_IOS
        var availableSpace = SimpleDiskUtils.DiskUtils.CheckAvailableSpace();
#elif UNITY_ANDROID
        var availableSpace = SimpleDiskUtils.DiskUtils.CheckAvailableSpace(true);
#endif
            return availableSpace > sizeInBytes / MIB;
        }

        public static Hash128 GetHashFromCache(string url)
        {
            if (string.IsNullOrEmpty(url)) {
                throw new Exception("[Caching] error: Url address was entered incorrectly " + url);
            }
            List<Hash128> listOfCachedVersions = new List<Hash128>();;
            var uri = new System.Uri(url);
            UnityEngine.Caching.GetCachedVersions(uri.LocalPath, listOfCachedVersions);
            if (listOfCachedVersions.Count > 0) return listOfCachedVersions[listOfCachedVersions.Count - 1];
            else return default;
        }

        public static string ConvertToLocalPath(this string url)
        {
            try
            {
                if (!string.IsNullOrEmpty(url))
                {
                    var path = Path.Combine(Application.persistentDataPath, cachingDirectory + new System.Uri(url).LocalPath);
                    return path.Replace("\\","/");
                }
                else
                {
                    throw new Exception("[Caching] error: Url address was entered incorrectly " + url);;
                }
            }
            catch (System.UriFormatException e)
            {
                throw new Exception("[Caching] error: " + url + " " + e.Message);
            }
        }

        public class Exception : System.Exception
        {
            public Exception(string message) : base(message)
            { }
        }
    }
}
