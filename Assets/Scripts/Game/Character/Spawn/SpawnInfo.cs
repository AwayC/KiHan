using System;
using TrueSync;
using KiHan.Math;
using UnityEngine;

namespace KiHan.Logic
{
    /// <summary>
    /// 生成物类型 (两类都复用 CharacterObj + ActionController，只在生命周期与可受击上区分)
    /// </summary>
    public enum SpawnType
    {
        /// <summary>
        /// 简单实体 (投射物 / 特效型攻击体)：没有受击，不由大脑驱动；动作链走完或寿命到期即回收
        /// </summary>
        Simple = 0,

        /// <summary>
        /// 角色实体 (分身 / 召唤物)：可被命中、有独立生命值，由大脑注入输入；寿命到期先播消失动作再回收，生命值归零立即回收
        /// </summary>
        Actor = 1,
    }

    /// <summary>
    /// 生成物配置 (运行时)
    /// </summary>
    public class SpawnInfo
    {
        public string id;
        public SpawnType type;

        /// <summary>
        /// 生成后进入的动作 (动作库与主人角色相同)
        /// </summary>
        public string initialActionId;

        /// <summary>
        /// 寿命到期时播放的消失动作，为空时到期直接回收
        /// </summary>
        public string despawnActionId;

        /// <summary>
        /// 寿命 (逻辑帧)，<= 0 表示不限
        /// </summary>
        public int lifeTicks;

        /// <summary>
        /// Actor 的生命值，0 表示沿用角色默认值
        /// </summary>
        public FP hp;

        public CloneBrainType brain;
        public CloneScriptStep[] script;

        /// <summary>
        /// 开局预建的实例数量 (同屏可能同时存在的上限)
        /// </summary>
        public int prewarm;

        public bool Hittable => type == SpawnType.Actor;
    }

    /// <summary>
    /// spawns.json 顶层容器 DTO
    /// </summary>
    [Serializable]
    public class SpawnDataContainer
    {
        public SpawnData[] data;
    }

    /// <summary>
    /// 生成物配置 DTO (String-First)
    /// </summary>
    [Serializable]
    public class SpawnData
    {
        public string id;

        [Tooltip("Simple / Actor")]
        public string type;

        public string initialActionId;
        public string despawnActionId;

        [Tooltip("寿命 (字符串秒数)，留空表示不限")]
        public string lifeTime;

        [Tooltip("Actor 生命值 (字符串)，留空沿用角色默认值")]
        public string hp;

        [Tooltip("None / Script")]
        public string brain;

        public CloneScriptStepData[] script;

        public int prewarm;

        public SpawnInfo ToRuntime()
        {
            var info = new SpawnInfo
            {
                id = id ?? string.Empty,
                type = ActionData.ParseEnum(type, SpawnType.Simple),
                initialActionId = initialActionId ?? string.Empty,
                despawnActionId = despawnActionId ?? string.Empty,
                lifeTicks = GameConfig.SecondsToTicks(ActionData.ParseFP(lifeTime)),
                hp = ActionData.ParseFP(hp),
                brain = ActionData.ParseEnum(brain, CloneBrainType.None),
                script = new CloneScriptStep[script != null ? script.Length : 0],
                prewarm = prewarm > 0 ? prewarm : 1
            };
            for (int i = 0; i < info.script.Length; i++)
            {
                info.script[i] = script[i].ToRuntime();
            }
            return info;
        }
    }

    /// <summary>
    /// 生成请求 (动作事件触发时记录，BattleWorld 本帧末尾按请求顺序执行)
    /// </summary>
    public struct SpawnRequest
    {
        public CharacterObj Source;
        public string SpawnId;
        public FPVector3 Position;
        public bool FacingLeft;
        public int SourceSkill;
    }

    /// <summary>
    /// 生成物实例提供者 (表现层工程中由 CharacterFactory 用开局预建的实例池实现)
    /// 只负责取出 / 归还已装配好动画与动作库的实例，并填写 CharacterObj.Spawn；编号、阵营、位置、动作由 BattleWorld 统一设置
    /// </summary>
    public interface ISpawnProvider
    {
        /// <summary>
        /// 取出生成物实例，未知 spawnId 返回 null
        /// </summary>
        CharacterObj Acquire(CharacterObj source, string spawnId);

        /// <summary>
        /// 归还已移出战场的实例
        /// </summary>
        void Release(CharacterObj entity);
    }
}
