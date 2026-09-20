using System.Collections.Generic;
using FlagRush.Domain.Ids;
using FlagRush.Domain.Profile;

namespace FlagRush.Domain.Tests.Fakes
{
    public sealed class TestInventory : IInventorySnapshot
    {
        public TestInventory(int slotCount, params SlotEntry[] entries)
        {
            SlotCount = slotCount;
            Entries = entries;
        }

        public int SlotCount { get; }
        public IReadOnlyList<SlotEntry> Entries { get; }
    }

    public sealed class TestProfile : IPlayerProfile
    {
        public TestProfile(AuthId authId, string displayName, int xp = 0, int score = 0, long money = 0, IInventorySnapshot inventory = null)
        {
            AuthId = authId;
            DisplayName = displayName;
            Xp = xp;
            Score = score;
            Money = money;
            Inventory = inventory ?? new TestInventory(0);
        }

        public AuthId AuthId { get; }
        public string DisplayName { get; }
        public int Xp { get; }
        public int Score { get; }
        public long Money { get; }
        public IInventorySnapshot Inventory { get; }
    }

    public sealed class InMemoryProfileRepository : IProfileRepository
    {
        readonly InMemoryStore<AuthId, IPlayerProfile> _store = new InMemoryStore<AuthId, IPlayerProfile>();

        public int Count => _store.Count;
        public bool TryLoad(AuthId key, out IPlayerProfile record) => _store.TryLoad(key, out record);
        public void Save(AuthId key, IPlayerProfile record) => _store.Save(key, record);
        public bool Delete(AuthId key) => _store.Delete(key);
    }

    sealed class SessionBinding : ISessionBinding
    {
        public AuthId AuthId { get; set; }
        public PlayerId PlayerId { get; set; }
        public EntityId Avatar { get; set; }
        public SessionState State { get; set; }
        public Tick LastSeen { get; set; }
    }

    public sealed class InMemorySessionRegistry : ISessionRegistry
    {
        readonly Dictionary<AuthId, SessionBinding> _byAuth = new Dictionary<AuthId, SessionBinding>();
        readonly Dictionary<PlayerId, SessionBinding> _byPlayer = new Dictionary<PlayerId, SessionBinding>();

        public IReadOnlyCollection<ISessionBinding> All => _byAuth.Values;

        public bool TryGetByAuth(AuthId authId, out ISessionBinding binding)
        {
            var found = _byAuth.TryGetValue(authId, out var b);
            binding = b;
            return found;
        }

        public bool TryGetByPlayer(PlayerId playerId, out ISessionBinding binding)
        {
            var found = _byPlayer.TryGetValue(playerId, out var b);
            binding = b;
            return found;
        }

        public ISessionBinding Connect(AuthId authId, PlayerId playerId, Tick tick)
        {
            if (!_byAuth.TryGetValue(authId, out var binding))
            {
                binding = new SessionBinding { AuthId = authId };
                _byAuth.Add(authId, binding);
            }
            else
            {
                _byPlayer.Remove(binding.PlayerId);
            }

            binding.PlayerId = playerId;
            binding.State = SessionState.Connected;
            binding.LastSeen = tick;
            _byPlayer[playerId] = binding;
            return binding;
        }

        public bool Disconnect(PlayerId playerId, Tick tick)
        {
            if (!_byPlayer.TryGetValue(playerId, out var binding)) return false;
            binding.State = SessionState.Disconnected;
            binding.LastSeen = tick;
            return true;
        }

        public bool AssignAvatar(PlayerId playerId, EntityId avatar)
        {
            if (!_byPlayer.TryGetValue(playerId, out var binding)) return false;
            binding.Avatar = avatar;
            return true;
        }

        public bool Forget(AuthId authId)
        {
            if (!_byAuth.TryGetValue(authId, out var binding)) return false;
            _byPlayer.Remove(binding.PlayerId);
            return _byAuth.Remove(authId);
        }
    }
}
