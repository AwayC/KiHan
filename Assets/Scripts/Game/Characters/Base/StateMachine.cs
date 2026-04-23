using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using KiHan.Logic;

/// <summary>
/// 状态抽象基类
/// </summary>
public abstract class StateBase
{
    private StateMachine _subSM;  // 子状态机

    public abstract sbyte StateType { get; }
    public abstract void Enter(CharacterEntity owner);
    public abstract void Update(CharacterEntity owner);
    public abstract void Exit(CharacterEntity owner);
}

public abstract class StateMachine
{
    private StateBase _currState; // 当前状态
    private Dictionary<sbyte, StateBase> _states = new Dictionary<sbyte, StateBase>();
    private CharacterEntity _owner;
    
    public StateMachine(CharacterEntity owner)
    {
        _owner = owner; 
    }

    public void RegisterState(StateBase state)
    {
        _states[state.StateType] = state; 
    }

    public void ChangeState(sbyte state)
    {
        _currState.Exit(_owner);
        _currState = _states[state];
        _currState.Enter(_owner);
    }

    public virtual void Update()
    {
        _currState.Update(_owner);
    }
}