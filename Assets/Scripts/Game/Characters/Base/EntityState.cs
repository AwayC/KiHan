using KiHan.Logic;

/// <summary>
/// 状态抽象基类
/// </summary>
public abstract class EntityState
{
    public abstract sbyte StateType { get; }
    public abstract void Enter(CharacterEntity owner);
    public abstract void Update(CharacterEntity owner, InputFrame input);
    public abstract void Exit(CharacterEntity owner);
}
