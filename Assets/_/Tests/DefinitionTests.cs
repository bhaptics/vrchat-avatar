using System.Linq;
using NUnit.Framework;
using UnityEditor;

namespace bHaptics.VRChatAvatar.Tests
{
    /// Rules every shipped definition keeps, whatever the contract JSON says: each set's schema, and self/others against the receiver flags.
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

        [Test]
        public void TheCatalogListsEveryDefinitionOnce()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<DeviceCatalog>($"{Folder}/Catalog.asset");
            var definitions = LoadDefinitions();

            Assert.That(catalog.definitions, Is.Unique);
            Assert.That(catalog.definitions, Is.EquivalentTo(definitions));
        }

        private static DeviceDefinition[] LoadDefinitions()
        {
            return AssetDatabase.FindAssets("t:DeviceDefinition", new[] { Folder })
                .Select(guid => AssetDatabase.LoadAssetAtPath<DeviceDefinition>(AssetDatabase.GUIDToAssetPath(guid)))
                .ToArray();
        }
    }
}
