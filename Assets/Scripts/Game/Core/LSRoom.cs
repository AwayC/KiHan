using System.Collections.Generic;
using UnityEngine;

namespace KiHan.Logic
{
    public class LSRoom
    {
        public uint RoomId { get; private set; }
        public uint CurrentFrame { get; private set; }

        public SortedList<uint, RoomFrame> RoomFrames = new SortedList<uint, RoomFrame>();
        public Dictionary<byte, CharacterEntity> Players = new Dictionary<byte, CharacterEntity>();
        public List<LogicEntity> AllEntities = new List<LogicEntity>();

        // 延迟添加和删除列表，防止在 Tick 循环中修改集合报错
        private List<LogicEntity> _pendingAdd = new List<LogicEntity>();
        private List<LogicEntity> _pendingRemove = new List<LogicEntity>();
        private int _nextEntityId = 1;

        public LSRoom(uint roomId)
        {
            this.RoomId = roomId;
            this.CurrentFrame = 0;
        }

        public void AddPlayer(CharacterEntity player)
        {
            if (player.EntityId <= 0)
            {
                player.EntityId = _nextEntityId++;
            }
            Players[(byte)player.owner] = player;
            AllEntities.Add(player);
        }

        public CharacterEntity GetPlayer(byte gId)
        {
            if (Players.TryGetValue(gId, out var player))
            {
                return player;
            }
            return null;
        }

        public void AddEntity(LogicEntity entity)
        {
            if (entity.EntityId <= 0)
            {
                entity.EntityId = _nextEntityId++;
            }
            _pendingAdd.Add(entity);
        }

        public void RemoveEntity(LogicEntity entity)
        {
            _pendingRemove.Add(entity);
        }

        public void Tick(RoomFrame frame)
        {
            CurrentFrame = frame.FrameId;
            ApplyInputs(frame.InputFrames);

            // 1. 处理延迟添加
            if (_pendingAdd.Count > 0)
            {
                AllEntities.AddRange(_pendingAdd);
                _pendingAdd.Clear();
            }

            // 2. 逻辑 Tick (更新物理与逻辑帧)
            for (int i = 0; i < AllEntities.Count; i++)
            {
                AllEntities[i].Tick();
            }

            // 3. 处理延迟删除
            if (_pendingRemove.Count > 0)
            {
                foreach(var entity in _pendingRemove)
                {
                    AllEntities.Remove(entity);
                }
                _pendingRemove.Clear();
            }

            // 4. 碰撞检测
            if (Players.TryGetValue(1, out var p1)) DoCollisionCheck(p1);
            if (Players.TryGetValue(2, out var p2)) DoCollisionCheck(p2);
        }

        private void ApplyInputs(Dictionary<byte, InputFrame> inputFrames)
        {
            foreach (var kv in inputFrames)
            {
                if (Players.TryGetValue(kv.Key, out var player))
                {
                    player.UpdateInput(kv.Value);
                }
            }
        }

        private void DoCollisionCheck(CharacterEntity target)
        {
            foreach (var attacker in AllEntities)
            {
                if (attacker.owner == target.owner) continue;
                
                // 判定：攻击者是否有攻击盒，且目标是否有受击盒，且未被此动作命中过
                if (attacker.CheckHit(target))
                {
                    if (attacker.CanHit(target))
                    {
                        HitData hitData = attacker.GetHitData();
                        target.ApplyHit(hitData);
                        attacker.RegisterHit(target); // 标记命中，防止同一段动作重复打击

                        // 触发相机打击感反馈
                        CameraControllor.Instance.ImpactEffect(hitData.IsHeavyHit);
                    }
                }
            }
        }
    }
}