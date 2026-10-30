using DOL.Database;
using DOL.Events;
using DOL.GS;
using DOL.GS.GameEvents;
using NUnit.Framework;

namespace DOL.UnitTests.Gameserver
{
    [TestFixture]
    public class UT_RiftStartupLocations
    {
        [Test]
        [TestCase(eRealm.Albion)]
        [TestCase(eRealm.Midgard)]
        [TestCase(eRealm.Hibernia)]
        public void CharacterCreation_AllRealms_SpawnsOnCotswoldBridge(eRealm realm)
        {
            var character = new DOLCharacters
            {
                Name = "Test" + realm,
                Realm = (byte)realm,
                Class = 1,
                Race = 1,
                AccountName = "TestAccount"
            };

            var eventArgs = new CharacterEventArgs(character, null);
            StartupLocations.CharacterCreation(null, null, eventArgs);

            Assert.Multiple(() =>
            {
                Assert.That(character.Region, Is.EqualTo(RiftConstants.COTSWOLD_REGION));
                Assert.That(character.Xpos, Is.EqualTo(RiftConstants.COTSWOLD_BRIDGE_X));
                Assert.That(character.Ypos, Is.EqualTo(RiftConstants.COTSWOLD_BRIDGE_Y));
                Assert.That(character.Zpos, Is.EqualTo(RiftConstants.COTSWOLD_BRIDGE_Z));
                Assert.That(character.Direction, Is.EqualTo(RiftConstants.COTSWOLD_BRIDGE_HEADING));

                // Bind stone coordinates
                Assert.That(character.BindRegion, Is.EqualTo(RiftConstants.COTSWOLD_REGION));
                Assert.That(character.BindXpos, Is.EqualTo(RiftConstants.COTSWOLD_BRIDGE_X));
                Assert.That(character.BindYpos, Is.EqualTo(RiftConstants.COTSWOLD_BRIDGE_Y));
                Assert.That(character.BindZpos, Is.EqualTo(RiftConstants.COTSWOLD_BRIDGE_Z));
                Assert.That(character.BindHeading, Is.EqualTo(RiftConstants.COTSWOLD_BRIDGE_HEADING));
            });
        }
    }
}
