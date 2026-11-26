using System;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace bHaptics.VRChatAvatar.Tests
{
    public class GeneratedAssetsTests
    {
        private static readonly DateTime Time = new DateTime(2026, 11, 26, 10, 15, 30);

        [TestCase("Avatar_FX", "Avatar_FX@bHaptics_20261126-101530")]
        [TestCase("Avatar_FX@bHaptics_20261110-120446", "Avatar_FX@bHaptics_20261126-101530")]
        [TestCase("Avatar_FX.bHapticsOSC", "Avatar_FX@bHaptics_20261126-101530")]
        public void GetPath_NamesTheCopyAfterTheSource(string source, string copy)
        {
            var path = GeneratedAssets.GetPath(source, "controller", Time);

            // A same-second copy already in the folder would add a number; these names are free.
            Assert.That(path, Is.EqualTo($"{GeneratedAssets.Folder}/{copy}.controller"));
        }

        [Test]
        public void Copy_OfAnUnsavedObject_Throws()
        {
            var clip = new AnimationClip { name = "Unsaved" };
            try
            {
                Assert.Throws<InvalidOperationException>(() => GeneratedAssets.Copy(clip, "Unsaved", Time));
            }
            finally
            {
                Object.DestroyImmediate(clip);
            }
        }
    }
}
