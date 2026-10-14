using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace KiHan.DebugTools
{
    [CreateAssetMenu(fileName = "LocalDebugConfig", menuName = "KiHan/Create Debug Config")]

    public class DebugConfig : ScriptableObject
    {
        static public string DefaultPath = "LocalDebugConfig";
        [Header("--- 训练场快速调试 ---")]
        public bool isBattleDebug = false;

        [Tooltip("单机对局双方无限血：受击照常扣血与显示伤害，但生命值不低于 1")]
        public bool infiniteHp = false;

        [Header("--- 选人测试 ---")]
        public int debugPlayer1CharId = 90001; // 1P 默认鸣人
        public int debugPlayer2CharId = 90001; // 2P 默认鸣人
    }
}
