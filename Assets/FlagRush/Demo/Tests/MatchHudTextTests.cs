using NUnit.Framework;

namespace FlagRush.Demo.Tests
{
    public class MatchHudTextTests
    {
        [Test]
        public void ClockFormats()
        {
            Assert.That(MatchHudText.Clock(18000), Is.EqualTo("5:00"));
            Assert.That(MatchHudText.Clock(59), Is.EqualTo("0:01"), "partial seconds round up");
            Assert.That(MatchHudText.Clock(0), Is.EqualTo("0:00"));
        }

        [Test]
        public void BannerNamesWinnerOrDraw()
        {
            Assert.That(MatchHudText.Banner(3, 1), Is.EqualTo("Red wins 3–1"));
            Assert.That(MatchHudText.Banner(1, 3), Is.EqualTo("Blue wins 1–3"));
            Assert.That(MatchHudText.Banner(2, 2), Is.EqualTo("Draw 2–2"));
        }

        [Test]
        public void FlagLines()
        {
            var home = new FlagGhost { Team = 0, State = FlagState.Home };
            var carried = new FlagGhost { Team = 0, State = FlagState.Carried, CarrierGhostId = 7 };
            var dropped = new FlagGhost { Team = 0, State = FlagState.Dropped, DroppedTick = 100 };
            Assert.That(MatchHudText.FlagLine(home, 7, 500), Is.EqualTo("home"));
            Assert.That(MatchHudText.FlagLine(carried, 7, 500), Is.EqualTo("carried by you"));
            Assert.That(MatchHudText.FlagLine(carried, 8, 500), Is.EqualTo("carried"));
            Assert.That(MatchHudText.FlagLine(dropped, 7, 280), Is.EqualTo("dropped · 7 s"));
            Assert.That(MatchHudText.FlagLine(dropped, 7, 100), Is.EqualTo("dropped · 10 s"));
            Assert.That(MatchHudText.FlagLine(dropped, 7, 900), Is.EqualTo("dropped · 0 s"), "past auto-return clamps at 0");
        }

        [Test]
        public void HitFlashWindowIsWrapSafe()
        {
            Assert.That(MatchHudText.IsHitFlash(100, 0), Is.False, "0 = never hit");
            Assert.That(MatchHudText.IsHitFlash(100, 100), Is.True);
            Assert.That(MatchHudText.IsHitFlash(109, 100), Is.True);
            Assert.That(MatchHudText.IsHitFlash(110, 100), Is.False);
            Assert.That(MatchHudText.IsHitFlash(99, 100), Is.False, "a hit in the future is not a flash");
            Assert.That(MatchHudText.IsHitFlash(3, uint.MaxValue - 2), Is.True, "wraps");
        }
    }
}
