using UnityEngine;

namespace Nakatetsu.Track.Graph
{
    public sealed class TrackGraphController : MonoBehaviour
    {
        [SerializeField] private TrackGraphAsset graphAsset;

        public TrackGraphContext Context { get; } = new();
        public bool IsInitialized => Context.IsInitialized;

        private void Awake()
        {
            if (!TryInitialize(out string error))
            {
                Debug.LogError($"Track graph lookup initialization failed: {error}", this);
            }
        }

        // ID検索を作り直す。線形のコンパイルや距離表の生成は行わない。
        public bool TryInitialize(out string error)
        {
            if (graphAsset == null)
            {
                Context.TryBuildLookups(null, out _);
                error = "TrackGraphAsset is not assigned.";
                return false;
            }

            return Context.TryBuildLookups(graphAsset.Definition, out error);
        }
    }
}
