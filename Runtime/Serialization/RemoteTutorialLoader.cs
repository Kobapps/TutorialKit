using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace TutorialKit
{
    /// <summary>
    /// Loads tutorial graphs from remote or local text (JSON). Useful for LiveOps: publish/patch
    /// tutorials without a client rebuild.
    /// </summary>
    public static class RemoteTutorialLoader
    {
        /// <summary>Downloads and parses a tutorial graph from a URL (http/https/file).</summary>
        public static async UniTask<TutorialGraph> LoadFromUrlAsync(string url, CancellationToken ct = default)
        {
            using var request = UnityWebRequest.Get(url);
            await request.SendWebRequest().WithCancellation(ct);

#if UNITY_2020_2_OR_NEWER
            if (request.result != UnityWebRequest.Result.Success)
#else
            if (request.isNetworkError || request.isHttpError)
#endif
            {
                Debug.LogError($"[TutorialKit] Failed to load tutorial from '{url}': {request.error}");
                return null;
            }

            return TutorialJson.FromJson(request.downloadHandler.text);
        }

        /// <summary>Parses a tutorial graph from an already-loaded JSON string.</summary>
        public static TutorialGraph LoadFromJson(string json) => TutorialJson.FromJson(json);

        /// <summary>Parses a tutorial graph from a <see cref="TextAsset"/>.</summary>
        public static TutorialGraph LoadFromTextAsset(TextAsset asset) =>
            asset != null ? TutorialJson.FromJson(asset.text) : null;
    }
}
