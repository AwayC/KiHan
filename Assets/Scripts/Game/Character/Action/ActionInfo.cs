using TrueSync;
using System;

using UnityEngine;



namespace KiHan.Logic

{



    /// <summary>

    /// 角色的一个动作的信息

    /// 在这个demo里面我偷了一个懒：就是直接让所有的类似这个结构的结构都变成可以填表的数据了

    /// 其实应该有个ActionData，通过这个ActionData转为ActionInfo来用，这也是大多团队都会漏掉做的一步

    /// ActionData就是读取Json表的数据，里面只有string int等基本类型，然后通过转换函数：ActionInfo::FromData(ActionData data)

    /// 来转换出来类似enum的结构，但是这个比较繁琐，可以在第一次重构的时候做，这个是必须要做的， 不然策划几乎没法做好

    /// 尤其是enum现在用数字做，很魔法，最好换成string来做。

    /// 但还是这句话，既然是demo……又不是不能用，对吧

    ///

    /// 至于音效和镜头的Tween，也应该是这个结构里面提供的，只是这里就不给范例了，非常简单的

    /// 比如镜头的Tween，其实是一个(float normalized)=>(Vector3 offset, float fieldOfView)

    /// </summary>

    [Serializable]

    public struct ActionInfo

    {

        /// <summary>

        /// 一个动作的id，每个动作都有一个id，必须是唯一的，同名的action会互相覆盖

        /// 也因为同名的action互相覆盖，所以才可以好好利用这个来做一些玩法，比如MHR中的替换技

        /// </summary>

        public string id;



        /// <summary>

        /// 在Animator中的一个动作的名称，用来叫Animator播放某个动作的

        /// </summary>

        public string animKey;



        /// <summary>

        /// 动作的分类，一个动作不一定要有分类，所以可以是空字符串

        /// 但是他会被用于一些动作切换的时候，比如我们可以定义它为“受伤动作”

        /// </summary>

        public string catalog;



        /// <summary>

        /// 这个动作的CancelTag，他可以Cancel掉哪些动作

        /// </summary>

        public CancelTag[] cancelTag;



        /// <summary>

        /// 这个动作可以被Cancel的信息

        /// 这里是长久存在的BeCancelledTag

        /// </summary>

        public BeCancelledTag[] beCancelledTag;



        /// <summary>

        /// 临时的被Cancel信息

        /// 这个是需要临时开启和关闭的信息

        /// </summary>

        public TempBeCancelledTag[] tempBeCancelledTag;



        /// <summary>

        /// 允许的操作，一个动作未必只有一个方式操作出来

        /// 比如街霸6的春丽百裂腿，236脚和连续按脚都能发

        /// </summary>

        public ActionCommand[] commands;



        /// <summary>

        /// 这里是保持移动方向的倍率，根据游戏不同、精度不同所需要的参数不同

        /// 大多横版游戏这个倍率会有多个值

        /// 这个值的作用是当做这个动作的时候，我继续前进或者后退按照什么倍率来

        /// 在动作游戏中，我们放有些技能，按住前进会向前更多的距离，按住后则会向前较短距离甚至不会向前

        /// 靠的就是这个参数，他的倍率x角色移动速度小于动作本身位移速度，就会导致按住后动作前进距离变短

        /// 大多动作这个值应该都是0，而移动类则是1

        /// 值得注意的是，一个动作的acceptance是阶段性的，比如起跳动作起跳的蹲伏阶段是不能移动的，但是跳起来之后却可以

        /// 如果美术把起跳到最高点做在一个动作了，也问题不大，靠这个来做

        /// 如果策划填表填错了，2段重叠了，那么就取速度慢的那段

        /// </summary>

        public MoveInputAcceptance[] inputAcceptance;



        /// <summary>

        /// 下一个动作的id

        /// 这个id是当动作自然播放完毕之后转向的那个动作，所以必须是一个严格的id

        /// </summary>

        public string autoNextActionId;



        /// <summary>

        /// 档切换到自己这个动作的时候，是否保持继续播放

        /// 这个意思是：比如移动会被移动自己cancel，这时候移动动作应该继续播放，而不是重置，所以要有这个true

        /// 原本在帧为单位的时候，cancel关系都是nextFrame所以可以通过frame之间的连接关系来，现在要以动作为单位，就靠这个凑出这个效果了

        /// </summary>

        public bool keepPlayingAnim;



        /// <summary>

        /// 是否当没有收到命令的时候，就自动走向autoNext了

        /// 这并不是一个好的做法，正确的做法，应该是在动作过程中设置某些帧

        /// 在这些帧去判断是否还有对应的command，如果没有了就终止了

        /// UE的Montage里面可以用多个NotifyState分布在动画过程

        /// Unity的话得自己做个编辑工具，所以我这个demo就先偷懒了——如果true，就是每一帧都做这个检查

        /// 其实从逻辑结构来说，我可以写成一个float[]，每个float代表一个percentage检查一下

        /// 但是这个填表………不是地球人可以轻易做到的（烦得很）………所以就先偷懒了，但是意思是一样的

        /// </summary>

        public bool autoTerminate;



        /// <summary>

        /// 是否反向，参照下面的facing，可以参考上面的设置检测点，现在只在动作开始的时候判定

        /// </summary>

        public bool inverse;



        /// <summary>

        /// 转向类型

        /// “input", "Inherited", 默认跟随方向输入

        /// </summary>

        public string facing;



        /// <summary>

        /// 在这个动作期间存在的攻击信息

        /// </summary>

        public AttackInfo[] attacks;



        /// <summary>

        /// 每一段攻击的信息

        /// </summary>

        public AttackBoxTurnOnInfo[] attackPhase;



        /// <summary>

        /// 受击框开关信息，

        /// 若有2个阶段重叠，那么战斗中的信息取哪个？

        /// 首先会看受击框属于哪个信息的，如果受击框同时被多个重叠的阶段开启

        /// 那就真的听天由命了（这就是策划配表问题了，只能通过编辑器ui解决）

        /// </summary>

        public BeHitBoxTurnOnInfo[] defensePhase;



        /// <summary>

        /// RootMotion 分段列表 (水平缓动位移 + 竖直速度/加速度)，为空则原地不动

        /// </summary>

        public RootMotionSegment[] rootMotion;



        /// <summary>

        /// 优先级

        /// 一个动作的基础优先级，优先级越高的动作越可能被选中

        /// </summary>

        public int priority;



        /// <summary>

        /// 滞空中落地时的动作变化 (如 jump_fall -> land、air_attack -> land)

        /// changeType 为 Keep 时落地不改变动作；击飞落地固定进入 hurt_land，不读取此字段

        /// </summary>

        public ActionChangeInfo onLand;



        /// <summary>

        /// 是否为空中动作 (隐性姿态)：配置了 onLand 且没有设置竖直速度的分段 (自身不起跳)，在地面上时视同落地

        /// 动作的进入条件不再单独配置姿态，由 cancelTag / beCancelledTag 的取消关系决定 (如空中攻击只挂在跳跃动作的取消窗口上)

        /// </summary>

        public bool IsAirAction => onLand.changeType != ActionChangeType.Keep && !HasVelocityZSegment;



        public bool HasVelocityZSegment
        {
            get
            {
                if (rootMotion == null) return false;
                for (int i = 0; i < rootMotion.Length; i++)
                {
                    if (rootMotion[i].hasVelocityZ) return true;
                }
                return false;
            }
        }



        /// <summary>

        /// 霸体开启区间，多个区间重叠时取最高等级

        /// </summary>

        public ArmorPhaseInfo[] armorPhase;

    }



    /// <summary>

    /// 读取json用的

    /// </summary>

    [Serializable]

    public struct ActionInfoContainer

    {

        public ActionInfo[] data;

    }



    /// <summary>

    /// 阶段性输入百分比

    /// </summary>

    [Serializable]

    public struct MoveInputAcceptance

    {

        /// <summary>

        /// 生效的动画帧区间 (30fps 素材帧，x: 起始帧, y: 结束帧)

        /// </summary>

        public Vector2Int range;

        /// <summary>

        /// 允许百分比

        /// </summary>

        public FP rate;

        /// <summary>

        /// 区间内是否允许随摇杆转向：摇杆偏左则朝左、偏右则朝右 (正上/正下不改变朝向)，受控锁定时无效

        /// </summary>

        public bool turn;

    }

}