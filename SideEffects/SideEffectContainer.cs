﻿using System.Diagnostics;
using ReAgent.State;

namespace ReAgent.SideEffects;

public record SideEffectContainer(ISideEffect SideEffect, RuleGroup Group, Rule Rule)
{
    private static readonly Stopwatch StopwatchInstance = new Stopwatch();

    public void SetPending()
    {
        Rule.PendingEffectCount++;
    }

    public void SetExecuted(RuleState state)
    {
        using (state.InternalState.SetCurrentGroup(Group))
        {
            if (--Rule.PendingEffectCount == 0)
            {
                state.InternalState.CurrentGroupState.ConditionActivations[Rule] = StopwatchInstance;
                StopwatchInstance.Restart();
            }
        }
    }

    public SideEffectApplicationResult Apply(RuleState state)
    {
        using (state.InternalState.SetCurrentGroup(Group))
        using (state.InternalState.CurrentGroupState.SetCurrentRule(Rule))
        {
            return SideEffect.Apply(state);
        }
    }
}