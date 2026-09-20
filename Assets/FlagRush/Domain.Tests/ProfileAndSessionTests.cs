using System.Linq;
using FlagRush.Domain.Ids;
using FlagRush.Domain.Profile;
using FlagRush.Domain.Tests.Fakes;
using NUnit.Framework;

namespace FlagRush.Domain.Tests
{
    public class ProfileAndSessionTests
    {
        [Test]
        public void ProfileRoundTripsThroughThePersistencePort()
        {
            var repo = new InMemoryProfileRepository();
            var auth = AuthId.NewRandom();
            var inventory = new TestInventory(8, new SlotEntry(0, new ItemKind(1), 3), new SlotEntry(2, new ItemKind(7), 1));
            repo.Save(auth, new TestProfile(auth, "Taylor", xp: 120, score: 4, money: 250, inventory: inventory));

            Assert.That(repo.TryLoad(auth, out var loaded), Is.True);
            Assert.That(loaded.AuthId, Is.EqualTo(auth));
            Assert.That(loaded.Xp, Is.EqualTo(120));
            Assert.That(loaded.Money, Is.EqualTo(250));
            Assert.That(loaded.Inventory.SlotCount, Is.EqualTo(8));
            Assert.That(loaded.Inventory.Entries.Select(e => e.Quantity), Is.EqualTo(new[] { 3, 1 }));

            Assert.That(repo.Delete(auth), Is.True);
            Assert.That(repo.TryLoad(auth, out _), Is.False);
        }

        [Test]
        public void ReconnectingWithTheSameAuthIdResumesTheSession()
        {
            var sessions = new InMemorySessionRegistry();
            var auth = AuthId.NewRandom();
            var avatar = new EntityId(42);

            var first = sessions.Connect(auth, new PlayerId(1), new Tick(10));
            Assert.That(sessions.AssignAvatar(new PlayerId(1), avatar), Is.True);
            Assert.That(sessions.Disconnect(new PlayerId(1), new Tick(20)), Is.True);
            Assert.That(first.State, Is.EqualTo(SessionState.Disconnected));

            var resumed = sessions.Connect(auth, new PlayerId(2), new Tick(30));

            Assert.That(resumed.AuthId, Is.EqualTo(auth));
            Assert.That(resumed.PlayerId, Is.EqualTo(new PlayerId(2)));
            Assert.That(resumed.Avatar, Is.EqualTo(avatar), "the avatar survives the disconnect");
            Assert.That(resumed.State, Is.EqualTo(SessionState.Connected));
            Assert.That(resumed.LastSeen, Is.EqualTo(new Tick(30)));
            Assert.That(sessions.TryGetByPlayer(new PlayerId(1), out _), Is.False, "the old PlayerId is gone");
            Assert.That(sessions.TryGetByPlayer(new PlayerId(2), out var byPlayer) && byPlayer.AuthId == auth, Is.True);
            Assert.That(sessions.All.Count, Is.EqualTo(1));
        }

        [Test]
        public void ForgettingASessionDoesNotTouchTheProfile()
        {
            var sessions = new InMemorySessionRegistry();
            var repo = new InMemoryProfileRepository();
            var auth = AuthId.NewRandom();
            repo.Save(auth, new TestProfile(auth, "Taylor", xp: 5));
            sessions.Connect(auth, new PlayerId(1), Tick.Zero);

            Assert.That(sessions.Forget(auth), Is.True);
            Assert.That(sessions.TryGetByAuth(auth, out _), Is.False);
            Assert.That(repo.TryLoad(auth, out var profile) && profile.Xp == 5, Is.True);
        }

        [Test]
        public void SlotEntriesRejectNegativeSlotsAndQuantities()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new SlotEntry(-1, new ItemKind(1), 1));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new SlotEntry(0, new ItemKind(1), -1));
        }
    }
}
