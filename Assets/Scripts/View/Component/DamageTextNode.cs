using System;
using UnityEngine;
using View.UI;

namespace View.Component
{
    /// <summary>
    /// 伤害跳字节点 (由 DamageTextPool 复用)
    /// 预建固定数量的数字精灵，显示时只替换精灵与位置 (0 GC)
    /// 动画：先沿飞出方向减速飞出一小段 (稍慢)，再加速继续飞并淡出消失 (稍快)，播完归还对象池
    /// </summary>
    public class DamageTextNode : MonoBehaviour
    {
        /// <summary>
        /// 最多显示的数字位数 (更大的伤害截断为全 9)
        /// </summary>
        public const int MaxDigits = 7;

        private const float FlyOutSeconds = 0.25f;
        private const float FlyOutDistance = 0.45f;
        private const float FlyAwaySeconds = 0.3f;
        private const float FlyAwayDistance = 1.2f;
        private const int SortingOrder = 30000; // 覆盖在人物之上

        private readonly SpriteRenderer[] _digits = new SpriteRenderer[MaxDigits];
        private readonly int[] _digitValues = new int[MaxDigits];
        private int _digitCount;
        private Vector3 _origin;
        private Vector3 _direction;
        private float _timer;
        private Action<DamageTextNode> _onFinished;

        /// <summary>
        /// 构建节点 (加载阶段调用)
        /// </summary>
        public static DamageTextNode Create(Transform parent, Action<DamageTextNode> onFinished)
        {
            var go = new GameObject("DamageText");
            go.transform.SetParent(parent, false);
            var node = go.AddComponent<DamageTextNode>();
            node._onFinished = onFinished;

            for (int i = 0; i < MaxDigits; i++)
            {
                var digitGo = new GameObject("digit");
                digitGo.transform.SetParent(go.transform, false);
                SpriteRenderer sr = digitGo.AddComponent<SpriteRenderer>();
                sr.sortingOrder = SortingOrder;
                node._digits[i] = sr;
            }

            go.SetActive(false);
            return node;
        }

        /// <summary>
        /// 显示跳字
        /// </summary>
        /// <param name="damage">伤害数值</param>
        /// <param name="font">数字字体</param>
        /// <param name="worldPos">初始世界坐标 (已由 BattleViewSpace 映射)</param>
        /// <param name="hitDirectionX">受击方向 (飞出方向的水平分量，-1 或 1)</param>
        public void Show(int damage, DigitFont font, Vector3 worldPos, int hitDirectionX)
        {
            FillDigits(damage);

            // 精灵按自身 PixelsPerUnit 换算世界尺寸，再乘字体缩放；从左到右排列，整体以文字中心为原点
            float scale = font.scale;
            float x = 0f;
            for (int i = 0; i < MaxDigits; i++)
            {
                SpriteRenderer sr = _digits[i];
                Sprite sprite = i < _digitCount ? font.Get(_digitValues[i]) : null;
                sr.enabled = sprite != null;
                if (sprite == null) continue;

                float unitsPerPixel = scale / sprite.pixelsPerUnit;
                if (i > 0) x += font.spacing * unitsPerPixel;
                sr.sprite = sprite;
                sr.color = Color.white;
                sr.transform.localScale = new Vector3(scale, scale, 1f);
                // 精灵左边缘对齐 x (精灵以 pivot 为原点)
                sr.transform.localPosition = new Vector3(x + sprite.pivot.x * unitsPerPixel, 0f, 0f);
                x += sprite.rect.width * unitsPerPixel;
            }

            float halfWidth = x * 0.5f;
            for (int i = 0; i < _digitCount; i++)
            {
                _digits[i].transform.localPosition -= new Vector3(halfWidth, 0f, 0f);
            }

            // 在屏幕平面 (世界 XY) 内斜向上飞：沿受击方向偏转，带少许随机
            _direction = new Vector3(hitDirectionX * UnityEngine.Random.Range(0.35f, 0.7f), 1f, 0f).normalized;
            _origin = worldPos;
            _timer = 0f;
            transform.SetPositionAndRotation(worldPos, Quaternion.identity);
            gameObject.SetActive(true);
        }

        private void FillDigits(int damage)
        {
            int value = Mathf.Clamp(Mathf.Abs(damage), 0, 9999999);
            _digitCount = 1;
            for (int v = value / 10; v > 0; v /= 10) _digitCount++;

            for (int i = _digitCount - 1; i >= 0; i--)
            {
                _digitValues[i] = value % 10;
                value /= 10;
            }
        }

        private void Update()
        {
            _timer += Time.deltaTime;

            float distance;
            float alpha = 1f;
            if (_timer < FlyOutSeconds)
            {
                // 飞出：减速 (ease-out)
                float t = _timer / FlyOutSeconds;
                distance = FlyOutDistance * (1f - (1f - t) * (1f - t));
            }
            else
            {
                // 继续飞走：加速 (ease-in) 并淡出
                float t = Mathf.Clamp01((_timer - FlyOutSeconds) / FlyAwaySeconds);
                distance = FlyOutDistance + FlyAwayDistance * t * t;
                alpha = 1f - t;
            }

            transform.position = _origin + _direction * distance;
            if (alpha < 1f)
            {
                var c = new Color(1f, 1f, 1f, alpha);
                for (int i = 0; i < _digitCount; i++)
                {
                    _digits[i].color = c;
                }
            }

            if (_timer >= FlyOutSeconds + FlyAwaySeconds)
            {
                gameObject.SetActive(false);
                _onFinished?.Invoke(this);
            }
        }
    }
}
