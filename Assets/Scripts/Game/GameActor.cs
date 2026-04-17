using UnityEngine;
using KiHan.Logic;

/// <summary>
/// 角色容器，组合 LogicEntity 和 PlayerView
/// </summary>
public class GameActor : MonoBehaviour
{
    public LogicEntity Logic { get; private set; }
    public PlayerView View { get; private set; }

    public void Init(LogicEntity logic, GameObject viewPrefab)
    {
        this.Logic = logic;
        
        // 创建表现层
        GameObject viewGo = Instantiate(viewPrefab, this.transform);
        viewGo.transform.localPosition = Vector3.zero;
        
        this.View = viewGo.GetComponent<PlayerView>();
        if (this.View == null) this.View = viewGo.AddComponent<PlayerView>();
        
        // 绑定逻辑实体到表现层
        this.View.BindEntity = logic;
    }

    /// <summary>
    /// 逻辑驱动入口
    /// </summary>
    public void LogicTick(InputFrame input)
    {
        Logic?.Tick(input);
    }

    #region 回滚支持 Snapshot
    
    public struct ActorState
    {
        public Vector2 Pos;
        public float Height;
        public bool IsFacingLeft;
        public string AnimName;
        public int FrameIndex;
        public int TickCounter;
        public sbyte StateType;
    }

    public ActorState SaveState()
    {
        sbyte sType = -1;
        if (Logic is CharacterEntity character)
        {
            sType = character.CurrentState?.StateType ?? -1;
        }

        return new ActorState
        {
            Pos = Logic.LogicPos,
            Height = Logic.LogicHeight,
            IsFacingLeft = Logic.IsFacingLeft,
            AnimName = Logic.CurrentAnim?.Name,
            FrameIndex = Logic.CurrentFrameIndex,
            TickCounter = Logic.GetTickCounter(),
            StateType = sType
        };
    }

    public void LoadState(ActorState state)
    {
        Logic.LogicPos = state.Pos;
        Logic.LogicHeight = state.Height;
        Logic.IsFacingLeft = state.IsFacingLeft;
        Logic.CurrentFrameIndex = state.FrameIndex;
        // 注意：这里需要根据 AnimName 重新找到动画资源并恢复 _tickCounter
        // 以及根据 StateType 恢复状态机
        if (Logic is CharacterEntity character && state.StateType != -1)
        {
            character.ChangeState(state.StateType);
        }
    }
    
    #endregion
}
