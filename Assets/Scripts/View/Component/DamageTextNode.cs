using System;
using UnityEngine;
using View.UI;

namespace View.Component
{
    /// <summary>
    /// 伤害跳字节点 (由 DamageTextPool 复用)
    /// 预建固定数量的数字精灵与一张底图，显示时只替换精灵与位置 (0 GC)
    /// - 普通：沿受击方向抛物线飞出 (上抛 + 重力)，后半段淡出
    /// - 暴击：数字从小放大到原大小，此时底图直接出现；数字再快速"放大 → 收缩"两次 (底图不缩放)；
    ///   停留片刻后数字与底图一起向上飘并淡出
    /// 播完归还对象池
    /// </summary>
    public class DamageTextNode : MonoBehaviour
    {
        /// <summary>
        /// 最多显示的数字位数 (更大的伤害截断为全 9)
        /// </summary>
        public const int MaxDigits = 7;

        private const float Duration = 0.5f;
        private const float Gravity = 12f;
        private const int SortingOrder = 30000; // 覆盖在人物之上
        private const int CritBackdropSortingOrder = SortingOrder + 1; // 暴击的底压在普通跳字之上
        private const int CritSortingOrder = SortingOrder + 2; // 暴击数字压在底之上

        // 暴击：弹出 → 两次脉冲 → 停留 → 上飘淡出 (秒 / 世界单位)
        private const float CritPopTime = 0.08f;
        private const float CritStartScale = 0.3f;
        private const int CritPulseCount = 2;
        private const float CritPulseTime = 0.09f;
        private const float CritPulseScale = 1.3f;
        private const float CritHoldTime = 0.25f;
        private const float CritRiseTime = 0.45f;
        private const float CritRiseDistance = 0.8f;
        private const float CritStartLift = 0.3f;

        /// <summary>
        /// 数字比底图宽度的这个比例还宽时，底图放大到能盖住数字
        /// </summary>
        private const float BackdropCoverRatio = 0.8f;

        private readonly SpriteRenderer[] _digits = new SpriteRenderer[MaxDigits];
        private readonly int[] _digitValues = new int[MaxDigits];
        private Transform _digitRoot;
        private SpriteRenderer _backdrop;
        private int _digitCount;
        private Vector3 _velocity;
        private Vector3 _origin;
        private float _timer;
        private bool _critical;
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

            var backdropGo = new GameObject("Backdrop");
            backdropGo.transform.SetParent(go.transform, false);
            node._backdrop = backdropGo.AddComponent<SpriteRenderer>();
            node._backdrop.sortingOrder = CritBackdropSortingOrder;
            node._backdrop.enabled = false;

            node._digitRoot = new GameObject("Digits").transform;
            node._digitRoot.SetParent(go.transform, false);
            for (int i = 0; i < MaxDigits; i++)
            {
                var digitGo = new GameObject("digit");
                digitGo.transform.SetParent(node._digitRoot, false);
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
        /// <param name="font">数字字体 (含暴击底图)</param>
        /// <param name="worldPos">初始世界坐标 (已由 BattleViewSpace 映射)</param>
        /// <param name="hitDirectionX">受击方向 (飞出方向的水平分量，-1 或 1)</param>
        /// <param name="critical">暴击：弹出、脉冲、停留后上飘 (字体本身由调用方选更大的暴击字体)</param>
        public void Show(int damage, DigitFont font, Vector3 worldPos, int hitDirectionX, bool critical)
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
                sr.sortingOrder = critical ? CritSortingOrder : SortingOrder;
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

            _critical = critical;
            _timer = 0f;
            _digitRoot.localScale = Vector3.one;
            _backdrop.enabled = false;
            transform.localScale = Vector3.one;
            if (critical)
            {
                PrepareBackdrop(font, x);
                _origin = worldPos + Vector3.up * CritStartLift;
                transform.SetPositionAndRotation(_origin, Quaternion.identity);
                _digitRoot.localScale = new Vector3(CritStartScale, CritStartScale, 1f);
            }
            else
            {
                _backdrop.sprite = null;
                // 抛物线在屏幕平面 (世界 XY) 内运动：沿受击方向的随机水平速度 + 随机初始上抛速度
                _velocity = new Vector3(UnityEngine.Random.Range(1.0f, 2.5f) * hitDirectionX, UnityEngine.Random.Range(3.5f, 5.0f), 0f);
                transform.SetPositionAndRotation(worldPos, Quaternion.identity);
            }
            gameObject.SetActive(true);
        }

        /// <summary>
        /// 随机取一张底图，以数字中心为中心；数字太宽时放大底图 (先不显示，弹出到原大小时才出现)
        /// </summary>
        private void PrepareBackdrop(DigitFont font, float textWidth)
        {
            Sprite[] backdrops = font.backdrops;
            Sprite sprite = backdrops != null && backdrops.Length > 0 ? backdrops[UnityEngine.Random.Range(0, backdrops.Length)] : null;
            _backdrop.sprite = sprite;
            if (sprite == null) return;

            float nativeWidth = sprite.rect.width / sprite.pixelsPerUnit;
            float s = Mathf.Max(font.backdropScale, textWidth / (nativeWidth * BackdropCoverRatio));
            Transform t = _backdrop.transform;
            t.localScale = new Vector3(s, s, 1f);
            // 精灵以 pivot 为原点：把精灵中心对齐到数字中心
            Vector2 centerOffset = (sprite.rect.size * 0.5f - sprite.pivot) / sprite.pixelsPerUnit * s;
            t.localPosition = new Vector3(-centerOffset.x, -centerOffset.y, 0f);
            _backdrop.color = Color.white;
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
            float dt = Time.deltaTime;
            _timer += dt;
            bool finished = _critical ? UpdateCritical() : UpdateNormal(dt);
            if (!finished) return;

            gameObject.SetActive(false);
            _onFinished?.Invoke(this);
        }

        private bool UpdateNormal(float dt)
        {
            transform.position += _velocity * dt;
            _velocity.y -= Gravity * dt;

            // 后半段淡出
            float progress = _timer / Duration;
            if (progress > 0.5f) SetAlpha(Mathf.Clamp01(1f - (progress - 0.5f) * 2f));
            return _timer >= Duration;
        }

        private bool UpdateCritical()
        {
            float t = _timer;

            // 1. 数字从小放大到原大小 (缓出)
            if (t < CritPopTime)
            {
                float p = t / CritPopTime;
                SetDigitScale(Mathf.Lerp(CritStartScale, 1f, 1f - (1f - p) * (1f - p)));
                return false;
            }

            // 2. 到原大小时底图直接出现；数字快速"放大 → 收缩"两次，底图不缩放
            if (!_backdrop.enabled && _backdrop.sprite != null) _backdrop.enabled = true;
            t -= CritPopTime;
            float pulses = CritPulseCount * CritPulseTime;
            if (t < pulses)
            {
                float p = (t % CritPulseTime) / CritPulseTime;
                SetDigitScale(1f + (CritPulseScale - 1f) * Mathf.Sin(p * Mathf.PI));
                return false;
            }

            SetDigitScale(1f);
            t -= pulses + CritHoldTime;
            if (t <= 0f) return false;

            // 3. 数字与底图一起上飘淡出 (缓出)
            float rise = Mathf.Clamp01(t / CritRiseTime);
            float eased = 1f - (1f - rise) * (1f - rise);
            transform.position = _origin + Vector3.up * (CritRiseDistance * eased);
            SetAlpha(1f - rise);
            return rise >= 1f;
        }

        private void SetDigitScale(float s)
        {
            _digitRoot.localScale = new Vector3(s, s, 1f);
        }

        private void SetAlpha(float alpha)
        {
            var c = new Color(1f, 1f, 1f, alpha);
            for (int i = 0; i < _digitCount; i++)
            {
                _digits[i].color = c;
            }
            if (_backdrop.enabled) _backdrop.color = c;
        }
    }
}
