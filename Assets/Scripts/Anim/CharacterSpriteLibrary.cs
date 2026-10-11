using System.Collections.Generic;
using UnityEngine;
using KiHan.Logic;

namespace KiHan.Logic
{
    /// <summary>
    /// 帧锚点 (整数像素，相对脚底中心；x 面朝方向为正, y 高度)
    /// 表现层用作特效挂点；逻辑层按名称读取 (如抓取锚点 "grab")，因此必须为整数保证确定性
    /// </summary>
    [System.Serializable]
    public class EffectAnchorInfo
    {
        public string Name = "Default";
        public Vector2Int Position;
    }

    [System.Serializable]
    public class SpriteFrameData
    {
        public string Name;
        public Sprite Sprite;
        public Vector2 Offset;
        
        public List<EffectAnchorInfo> EffectAnchors = new List<EffectAnchorInfo>();

        public List<LogicBox> HurtBoxes = new List<LogicBox>();
        public List<LogicBox> HitBoxes = new List<LogicBox>();
        public List<EffectLayerInfo> ExtraLayers = new List<EffectLayerInfo>();
    }
}

[CreateAssetMenu(fileName = "NewSpriteLibrary", menuName = "Naruto/Sprite Library")]
public class CharacterSpriteLibrary : ScriptableObject
{
    [Header("判定盒模板 (整数像素单位)")]
    public LogicBox DefaultHurtBox = new LogicBox(new Vector2Int(0, 60), new Vector2Int(60, 140), 80);
    public LogicBox DefaultHitBox = new LogicBox(new Vector2Int(40, 60), new Vector2Int(60, 60), 80);

    [Header("厚度预设 (Side Presets，整数像素)")]
    public List<int> SidePresets = new List<int> { 50, 80, 100, 150 };

    public List<SpriteFrameData> AllFrames = new List<SpriteFrameData>();

    public SpriteFrameData GetFrame(int index)
    {
        if (index >= 0 && index < AllFrames.Count) return AllFrames[index];
        return null;
    }
}