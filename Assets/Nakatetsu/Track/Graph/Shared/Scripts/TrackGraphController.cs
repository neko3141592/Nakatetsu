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
                Debug.LogError($"Track graph lookup initialization failed: {error}", this);
        }

        /// <summary>Rebuilds lookup dictionaries only; does not compile or generate track geometry.</summary>
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
