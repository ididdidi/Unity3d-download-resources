using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Video;

namespace mofrison.Network {
    public class Example : MonoBehaviour
    {
        private string cachingDirectory = "data";
        private string skyboxMatName = "SkyboxMaterial";
        private string canvasName = "MovieCanvas";

        [SerializeField] private string textureUrl = "http://192.168.1.11:8000/test/kris-guico-rsB-he-ye7w-unsplash.jpg";
        [SerializeField] private string materialsURL = "http://192.168.1.11:8000/test/moviecanvas";
        [SerializeField] private string prefabURL = "http://192.168.1.11:8000/test/moviecanvas";
        [SerializeField] private string movieUrl = "http://app.iqpax.com/oculus/go/Yurkov.mp4";

        private List<AssetBundle> loadedBundles = new List<AssetBundle>();

        private void Awake()
        {
            ResourceCache.ConfiguringCaching(cachingDirectory);
        }

            // Start is called before the first frame update
        private async void Start()
        {
            RenderSettings.skybox = await DownloadFromBundle<Material>(materialsURL, skyboxMatName);
            RenderSettings.skybox.mainTexture = await DownloadTexture(textureUrl);

            var prefab = await DownloadFromBundle<GameObject>(prefabURL, canvasName);
            var videoPlayer = Instantiate(prefab).GetComponent<VideoPlayer>();
            videoPlayer.url = DownloadVideo(movieUrl);
            if (videoPlayer.gameObject.activeInHierarchy) { videoPlayer.Play(); }
            else { Debug.LogWarning("videoPlayer.is not ActiveAndEnabled"); }
        }

        private async Task<T> DownloadFromBundle<T>(string url, string name) where T : Object
        {
            var bundle = await Network.RequestBundle(url, new System.Threading.CancellationTokenSource());
            loadedBundles.Add(bundle);
            return bundle.LoadAsset<T>(name);
        }

        private async Task<Texture> DownloadTexture(string url)
        {
            var texture = await Network.RequestTexture(url, new System.Threading.CancellationTokenSource());
            texture.wrapMode = TextureWrapMode.Clamp;
            return texture;
        }

        private string DownloadVideo(string url)
        {
            return Network.RequestVideoStream(url, new System.Threading.CancellationTokenSource());
        }

        private void OnDestroy()
        {
            foreach(var bundle in loadedBundles)
            {
                bundle.Unload(true);
            }
        }
    }
}