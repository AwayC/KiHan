using System;

namespace KiHan.Logic.AI
{
    public enum BTStatus
    {
        Success,
        Failure,
        Running,
    }

    /// <summary>
    /// 行为树一帧的黑板：输入为角色与目标，叶子节点把决策写入按键与摇杆分量 (每帧复用，0 GC)
    /// </summary>
    public sealed class BotContext
    {
        public CharacterObj Self { get; private set; }
        public CharacterObj Target { get; private set; }
        public uint FrameId { get; private set; }

        /// <summary>
        /// 本帧按住的按键
        /// </summary>
        public ButtonMask Buttons;

        /// <summary>
        /// 摇杆分量：横向 -1 / 0 / 1 (正为屏幕右)，纵深 -1 / 0 / 1 (正为远离镜头)
        /// </summary>
        public int MoveX;
        public int MoveY;

        public void Begin(CharacterObj self, CharacterObj target, uint frameId)
        {
            Self = self;
            Target = target;
            FrameId = frameId;
            Buttons = ButtonMask.None;
            MoveX = 0;
            MoveY = 0;
        }
    }

    /// <summary>
    /// 行为树节点：每帧从根重新求值 (无运行中记忆，状态全部来自逻辑世界与帧号，结果确定)
    /// </summary>
    public abstract class BTNode
    {
        public abstract BTStatus Tick(BotContext ctx);
    }

    /// <summary>
    /// 选择：依次执行子节点，返回第一个非 Failure 的结果
    /// </summary>
    public sealed class BTSelector : BTNode
    {
        private readonly BTNode[] _children;

        public BTSelector(params BTNode[] children)
        {
            _children = children;
        }

        public override BTStatus Tick(BotContext ctx)
        {
            for (int i = 0; i < _children.Length; i++)
            {
                BTStatus status = _children[i].Tick(ctx);
                if (status != BTStatus.Failure) return status;
            }
            return BTStatus.Failure;
        }
    }

    /// <summary>
    /// 顺序：依次执行子节点，返回第一个非 Success 的结果
    /// </summary>
    public sealed class BTSequence : BTNode
    {
        private readonly BTNode[] _children;

        public BTSequence(params BTNode[] children)
        {
            _children = children;
        }

        public override BTStatus Tick(BotContext ctx)
        {
            for (int i = 0; i < _children.Length; i++)
            {
                BTStatus status = _children[i].Tick(ctx);
                if (status != BTStatus.Success) return status;
            }
            return BTStatus.Success;
        }
    }

    /// <summary>
    /// 条件：成立返回 Success，否则 Failure
    /// </summary>
    public sealed class BTCondition : BTNode
    {
        private readonly Func<BotContext, bool> _predicate;

        public BTCondition(Func<BotContext, bool> predicate)
        {
            _predicate = predicate;
        }

        public override BTStatus Tick(BotContext ctx) => _predicate(ctx) ? BTStatus.Success : BTStatus.Failure;
    }

    /// <summary>
    /// 行为：写入按键 / 摇杆并返回状态
    /// </summary>
    public sealed class BTAction : BTNode
    {
        private readonly Func<BotContext, BTStatus> _action;

        public BTAction(Func<BotContext, BTStatus> action)
        {
            _action = action;
        }

        public override BTStatus Tick(BotContext ctx) => _action(ctx);
    }

    /// <summary>
    /// 以行为树决策的人机大脑
    /// </summary>
    public sealed class BehaviorTreeBrain : IBotBrain
    {
        private readonly BTNode _root;
        private readonly BotContext _ctx = new BotContext();

        public BehaviorTreeBrain(BTNode root)
        {
            _root = root;
        }

        public void Reset()
        {
        }

        public void Think(CharacterObj self, CharacterObj target, uint frameId, InputFrame output)
        {
            _ctx.Begin(self, target, frameId);
            _root.Tick(_ctx);
            output.Buttons = _ctx.Buttons;
            output.JoyStickDir = JoystickDir.FromAxes(_ctx.MoveX, _ctx.MoveY);
        }
    }
}
