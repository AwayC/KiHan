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

    /// <summary>
    /// 受击拦截器。当实体被攻击时，优先调用当前状态的此方法。
    /// 返回 true 表示该状态拦截了此次受击判定（由状态自行处理），底层将不再执行默认的受击切状态逻辑。
    /// </summary>
    public virtual bool OnBeforeHit(CharacterEntity owner, HitData data)
    {
        return false;
    }
}

public abstract class StateMachine
{
    protected StateBase _currState; 
    protected StateBase _lastState; // 新增：记录上一个状态
    protected Dictionary<sbyte, StateBase> _states = new Dictionary<sbyte, StateBase>();
    protected CharacterEntity _owner;

    public StateBase CurrentState => _currState;
    public StateBase LastState => _lastState;
    
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
        
        if (_currState != null)
        {
            _currState.Exit(_owner);
            _lastState = _currState;
        }

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

    /// <summary>
    /// 尝试让当前状态拦截受击
    /// </summary>
    public bool TryInterceptHit(HitData hitData)
    {
        if (_currState != null)
        {
            return _currState.OnBeforeHit(_owner, hitData);
        }
        return false;
    }
}
