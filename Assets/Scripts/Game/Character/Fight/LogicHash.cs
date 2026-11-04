namespace KiHan.Logic
{
    /// <summary>
    /// 战斗中的确定性"随机"：由逻辑帧号与攻防双方实体 ID 哈希得到，所有客户端同一局面结果相同 (不使用 UnityEngine.Random)
    /// </summary>
    public static class LogicHash
    {
        public static uint Mix(uint frameId, int targetId, int attackerId)
        {
            unchecked
            {
                uint h = frameId * 2654435761u ^ (uint)targetId * 2246822519u ^ (uint)attackerId * 3266489917u;
                h ^= h >> 15;
                h *= 2246822519u;
                h ^= h >> 13;
                h *= 3266489917u;
                h ^= h >> 16;
                return h;
            }
        }
    }
}
