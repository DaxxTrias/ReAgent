using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using ExileCore2.PoEMemory.Components;
using ExileCore2.PoEMemory.MemoryObjects;
using Newtonsoft.Json;

namespace ReAgent.State;

[Api]
public class BuffDictionary
{
    private readonly SkillDictionary _playerSkills;
    private readonly Dictionary<string, Buff> _source;
    private readonly List<Buff> _listSource;
    private readonly Lazy<List<StatusEffect>> _allBuffs;

    public BuffDictionary(List<Buff> source, SkillDictionary playerSkills)
    {
        _playerSkills = playerSkills;
        _listSource = source.Where(x => x.Name != null).ToList();
        _source = _listSource.DistinctBy(x => x.Name).ToDictionary(x => x.Name);
        _allBuffs = new Lazy<List<StatusEffect>>(() => _listSource.Select(CreateStatusEffect).ToList(), LazyThreadSafetyMode.None);
    }

    [Api]
    public StatusEffect this[string id]
    {
        get
        {
            if (_source.TryGetValue(id, out var value))
            {
                return CreateStatusEffect(value);
            }

            return new StatusEffect("", "", false, 0, 0, 0, 0, new Lazy<SkillInfo>(() => SkillInfo.Empty("")));
        }
    }

    private StatusEffect CreateStatusEffect(Buff value)
    {
        return new StatusEffect(value.Name, value.DisplayName, true, value.Timer, value.MaxTime, value.BuffCharges, value.FlaskSlot, new Lazy<SkillInfo>(() =>
            Entity.Player.Equals(value.SourceEntity)
                ? _playerSkills?.ByNumericId(value.SourceSkillId, value.SourceSkillId2) ?? SkillInfo.Empty("")
                : SkillInfo.Empty("")));
    }

    /// <summary>Checks if there is a buff with name <paramref name="id"/></summary>
    [Api]
    public bool Has(string id)
    {
        return _source.ContainsKey(id);
    }

    /// <summary>
    /// Reads a <see cref="ushort"/> at <c>buff.Address + offset</c>.
    /// Used for fields not yet mapped in GameOffsets (e.g. Chronomancer phase at 0x14C).
    /// </summary>
    [Api]
    public ushort? ReadUInt16At(string buffId, int offset)
    {
        if (!_source.TryGetValue(buffId, out var buff) || buff.Address == 0)
            return null;

        try
        {
            return buff.M.Read<ushort>(buff.Address + offset);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Chronomancer Sands pendulum position from <c>chronomancer_cast_speed</c> at <paramref name="buffFieldOffset"/> (default 0x14C).
    /// Game stores strength as 0..6000 (hundredths of a percent: 5800 = 58%). Returns 0 = max cast speed (left), 1 = max AoE (right).
    /// </summary>
    [Api]
    public float? ChronomancerSandsPendulumT(int buffFieldOffset = 0x14C)
    {
        var raw = ReadUInt16At("chronomancer_cast_speed", buffFieldOffset);
        if (raw == null)
            return null;

        var castPercent = raw.Value / 100f;
        return Math.Clamp((60f - castPercent) / 59f, 0f, 1f);
    }

    public List<StatusEffect> AllBuffs => _allBuffs.Value;
}