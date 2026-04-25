using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using KiHan.Logic;

public abstract class StateBase
{
    public abstract sbyte StateType { get; }
    public abstract void Enter(CharacterEntity owner);
    public abstract void Update(CharacterEntity owner);
    public abstract void Exit(CharacterEntity owner);

    /// <summary>
    /// 状态内动态生成攻击数据
    /// </summary>
    public virtual HitData GetHitData(CharacterEntity owner)
    {
        return null; // 默认状态（如待机、跑动）没有攻击数据
    }
}

public abstract class StateMachine
{
    protected StateBase _currState; 
    protected Dictionary<sbyte, StateBase> _states = new Dictionary<sbyte, StateBase>();
    protected CharacterEntity _owner;
    
    public StateMachine(CharacterEntity owner)
    {
        _owner = owner; 
    }

    public void RegisterState(StateBase state)
    {
        _states[state.StateType] = state; 
    }

    public void ChangeState(sbyte stateIdx)
    {
        if (!_states.ContainsKey(stateIdx)) return;
        _currState?.Exit(_owner);
        _currState = _states[stateIdx];
        _currState.Enter(_owner);
    }

    public virtual void Update()
    {
        _currState?.Update(_owner);
    }

    public HitData GetHitData()
    {
        return _currState?.GetHitData(_owner);
    }
}
