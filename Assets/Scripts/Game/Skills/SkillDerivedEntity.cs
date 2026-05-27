using KiHan.Logic;
using UnityEngine;

namespace KiHan.Logic
{
    /// <summary>
    /// 技能派生实体（如飞行道具、召唤物、攻击特效实体等）
    /// </summary>
    public class SkillDerivedEntity : LogicEntity
    {
        public int LifeTime; // 存活逻辑帧数
        public CharacterEntity Creator; // 释放者

        public SkillDerivedEntity(CharacterEntity creator, int lifeTime)
        {
            this.Creator = creator;
            this.owner = creator.owner; // 继承属主，避免痛击队友
            this.LifeTime = lifeTime;
            this.IsFacingLeft = creator.IsFacingLeft;
            this.pos = creator.pos;
            this.height = creator.height;
        }

        public virtual void Update()
        {

        }

        public override void Tick()
        {
            // 自动销毁机制
            LifeTime--;
            if (LifeTime <= 0)
            {
                DestroySelf();
                return; // 销毁后不再执行后续逻辑
            }

            UpdateTickCounter();
            Update();
            ProcessPhysics();

        }

        public virtual void DestroySelf()
        {
            // 从场景管理器中安全移除
            SceneManager.Instance.RemoveEntity(this);
        }

        public override void LoadRes(string basePath)
        {
            // 派生实体的逻辑帧数据加载，可以由子类重写或者读取特定的配置
        }
    }
}
