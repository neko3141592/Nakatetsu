using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Nakatetsu.Track.GuideLine;

namespace Nakatetsu.Track.Tests
{
    public sealed class GuideLineAssetTests
    {
        [Test]
        public void IncludedStraightAssetLoadsAndEvaluates()
        {
            var asset = AssetDatabase.LoadAssetAtPath<GuideLineAsset>(
                "Assets/Nakatetsu/Track/Geometry/Data/Straight100m.asset");
            Assert.That(asset, Is.Not.Null);
            Assert.That(asset.TryEvaluate(100f, out var sample), Is.True);
            Assert.That(Vector3.Distance(sample.Position, new Vector3(0, 0, 100)), Is.LessThan(0.0001f));
        }
    }
}
