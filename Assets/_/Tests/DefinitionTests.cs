using System.Linq;
using NUnit.Framework;
using UnityEditor;

namespace bHaptics.VRChatAvatar.Tests
{
    /// Rules every shipped definition keeps, whatever the contract JSON says: each set's schema, self/others against the receiver flags, and the merged node pairs.
    public class DefinitionTests
    {
        private const string Folder = "Packages/com.bhaptics.vrchat-avatar/Runtime/Definitions";

        [Test]
        public void EachSetUsesItsPlatformsSchema()
        {
            foreach (var definition in LoadDefinitions())
            {
                foreach (var receiver in definition.pc.SelectMany(node => node.receivers))
                {
                    Assert.That(receiver.address, Does.StartWith("bOSC/v2/"), definition.name);
                }

                foreach (var receiver in definition.quest.SelectMany(node => node.receivers))
                {
                    Assert.That(receiver.address, Does.StartWith("bOSC/v2m/"), definition.name);
                }
            }
        }

        [Test]
        public void SelfAndOthersMatchTheReceiverFlags()
        {
            foreach (var definition in LoadDefinitions())
            {
                foreach (var receiver in definition.pc.Concat(definition.quest).SelectMany(node => node.receivers))
                {
                    if (receiver.address.EndsWith("/self"))
                    {
                        Assert.That(receiver.allowSelf && !receiver.allowOthers, Is.True, receiver.address);
                        continue;
                    }

                    Assert.That(receiver.address, Does.EndWith("/others"));
                    Assert.That(receiver.allowOthers, Is.True, receiver.address);
                    // Only the Quest vest's receivers take both, writing /others alone (contract item 4).
                    if (receiver.allowSelf)
                    {
                        Assert.That(receiver.address, Does.StartWith("bOSC/v2m/Vest"), receiver.address);
                    }
                }
            }
        }

        // Node n and n + count share one motor on current products, so they share one PC node.
        [TestCase("Vest", "VestFront", 12, 4, 32)]
        [TestCase("Vest", "VestBack", 12, 4, 32)]
        [TestCase("ForearmL", "ForearmL", 0, 3, 3)]
        [TestCase("ForearmR", "ForearmR", 0, 3, 3)]
        public void MergedPairsShareANode(string definitionName, string segment, int first, int count, int nodeCount)
        {
            var definition = AssetDatabase.LoadAssetAtPath<DeviceDefinition>($"{Folder}/{definitionName}.asset");

            Assert.That(definition.pc.Length, Is.EqualTo(nodeCount));
            for (var n = first; n < first + count; n++)
            {
                var addresses = new[] { n, n + count }.SelectMany(i => new[] { $"bOSC/v2/{segment}/{i}/others", $"bOSC/v2/{segment}/{i}/self" });
                var node = definition.pc.Single(candidate => candidate.receivers.Any(receiver => receiver.address == $"bOSC/v2/{segment}/{n}/self"));
                Assert.That(node.receivers.Select(receiver => receiver.address), Is.EquivalentTo(addresses), node.name);
            }
        }

        [Test]
        public void TheCatalogListsEveryOfferedDefinitionOnce()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<DeviceCatalog>($"{Folder}/Catalog.asset");
            // Hand and Foot are discontinued and left out of Add Device for now; their definitions stay.
            var offered = LoadDefinitions().Where(d => d.kind != DeviceKind.Hand && d.kind != DeviceKind.Foot);

            Assert.That(catalog.definitions, Is.Unique);
            Assert.That(catalog.definitions, Is.EquivalentTo(offered));
        }

        private static DeviceDefinition[] LoadDefinitions()
        {
            return AssetDatabase.FindAssets("t:DeviceDefinition", new[] { Folder })
                .Select(guid => AssetDatabase.LoadAssetAtPath<DeviceDefinition>(AssetDatabase.GUIDToAssetPath(guid)))
                .ToArray();
        }
    }
}
