using System;
using System.Collections.Generic;
using FlagRush.Domain.Ids;
using FlagRush.Domain.Ports;
using FlagRush.Domain.Simulation;

namespace FlagRush.Domain.Tests.Fakes
{
    public sealed class SeededRandom : IRandomSource
    {
        readonly Random _random;

        public SeededRandom(uint seed)
        {
            Seed = seed;
            _random = new Random((int)seed);
        }

        public uint Seed { get; }
        public int NextInt(int minInclusive, int maxExclusive) => _random.Next(minInclusive, maxExclusive);
        public float NextFloat() => (float)_random.NextDouble();
    }

    public sealed class ListLog : ILogSink
    {
        public readonly List<(LogLevel Level, string Message)> Entries = new List<(LogLevel, string)>();
        public void Log(LogLevel level, string message) => Entries.Add((level, message));
    }

    public sealed class FixedClock : IClock
    {
        public FixedClock(int tickRate) { TickRate = tickRate; }
        public Tick Now { get; set; }
        public int TickRate { get; }
    }

    public sealed class InMemoryStore<TKey, TRecord> : IPersistenceStore<TKey, TRecord>
    {
        readonly Dictionary<TKey, TRecord> _records = new Dictionary<TKey, TRecord>();

        public int Count => _records.Count;
        public bool TryLoad(TKey key, out TRecord record) => _records.TryGetValue(key, out record);
        public void Save(TKey key, TRecord record) => _records[key] = record;
        public bool Delete(TKey key) => _records.Remove(key);
    }

    /// <summary>Both ends of a channel in one object: inputs go in with Send, the authority drains them with Collect.</summary>
    public sealed class LoopbackChannel : IReplicationChannel
    {
        readonly Queue<IInput> _inbound = new Queue<IInput>();
        public readonly List<(Tick Tick, IReadOnlyList<IGameEvent> Events)> Published = new List<(Tick, IReadOnlyList<IGameEvent>)>();

        public void Send(IInput input) => _inbound.Enqueue(input);

        public IReadOnlyList<IInput> Collect(Tick tick)
        {
            var batch = new List<IInput>();
            while (_inbound.Count > 0 && _inbound.Peek().Tick <= tick) batch.Add(_inbound.Dequeue());
            return batch;
        }

        public void Publish(Tick tick, IReadOnlyList<IGameEvent> events) => Published.Add((tick, events));
    }
}
