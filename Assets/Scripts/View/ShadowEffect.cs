using UnityEngine;

/// <summary>
/// 脚下光环动画脚本
/// 负责 2.5D 透视压缩以及内外圈差异化旋转
/// </summary>
public class ShadowEffect : MonoBehaviour
{
    [Header("旋转速度 (度/秒)")]
    public float Speed1 = 180f; // 外圈速度
    public float Speed2 = -90f; // 内圈速度 (通常转速相差一倍且反向视觉效果较好)

    private Transform _part1;
    private Transform _part2;

    private void Start()
    {
        // 1. 实现 2.5D 视角下的 Y 轴透视压缩 (0.4 倍)
        // 注意：父物体压缩后，子物体绕 Z 轴旋转会产生椭圆转动的视觉感，完美贴合地面
        transform.localScale = new Vector3(1f, 0.35f, 1f);

        // 2. 获取预制体子物体 (根据 shadow_1 结构)
        if (transform.childCount > 0)
        {
            _part1 = transform.GetChild(0);
        }
        
        if (transform.childCount > 1)
        {
            _part2 = transform.GetChild(1);
        }
    }

    private void Update()
    {
        // 3. 驱动旋转动画
        if (_part1 != null)
        {
            _part1.Rotate(0, 0, Speed1 * Time.deltaTime);
        }
        
        if (_part2 != null)
        {
            _part2.Rotate(0, 0, Speed2 * Time.deltaTime);
        }
    }
}
