using UnityEngine;
using System.Collections;
using KiHan.View.Render;

namespace View.Component
{
    /// <summary>
    /// 伤害跳字特效节点
    /// 负责加载数字切图并实现抛物线和淡出效果
    /// </summary>
    public class DamageTextNode : MonoBehaviour
    {
        private SpriteRenderer[] _digitRenderers;
        private float _duration = 0.5f;

        /// <summary>
        /// 初始化跳字
        /// </summary>
        /// <param name="damage">伤害数值</param>
        /// <param name="isPlayerHit">是否是玩家自己造成的伤害（控制颜色，玩家白字，敌人红字）</param>
        /// <param name="worldPos">初始世界坐标 (已由 BattleViewSpace 映射)</param>
        /// <param name="hitDirectionX">受击方向（控制抛物线方向，-1或1）</param>
        public void Init(int damage, bool isPlayerHit, Vector3 worldPos, int hitDirectionX)
        {
            string damageStr = Mathf.Abs(damage).ToString();
            _digitRenderers = new SpriteRenderer[damageStr.Length];

            string fontPath = isPlayerHit ? "UI/Number/White_Font" : "UI/Number/Red_Font";

            float currentX = 0f;
            for (int i = 0; i < damageStr.Length; i++)
            {
                string charStr = damageStr[i].ToString();
                
                // 直接按照路径加载单个数字的 Sprite Asset
                Sprite digitSprite = Resources.Load<Sprite>($"{fontPath}/{charStr}");

                if (digitSprite != null)
                {
                    GameObject digitGo = new GameObject($"digit_{i}");
                    digitGo.transform.SetParent(this.transform);
                    digitGo.transform.localPosition = new Vector3(currentX, 0, 0);

                    SpriteRenderer sr = digitGo.AddComponent<SpriteRenderer>();
                    sr.sprite = digitSprite;
                    // 确保层级极高，覆盖在人物之上
                    sr.sortingOrder = 30000; 
                    _digitRenderers[i] = sr;

                    // 累加 x 坐标，用于排列下一个数字。使用 sprite 的宽度转换单位，加上字间距
                    currentX += (digitSprite.rect.width * 0.01f) + 0.02f; 
                }
                else
                {
                    Debug.LogError($"[DamageText] 未能加载数字图片: {fontPath}/{charStr}");
                }
            }

            // 整体居中偏移：把父节点中心移到文字中心
            float totalWidth = currentX - 0.02f;
            for (int i = 0; i < transform.childCount; i++)
            {
                var child = transform.GetChild(i);
                child.localPosition -= new Vector3(totalWidth / 2f, 0, 0);
            }

            // 抛物线在屏幕平面 (世界 XY) 内运动
            transform.SetPositionAndRotation(worldPos, Quaternion.identity);

            // 开始抛物线动画
            StartCoroutine(AnimateText(worldPos, hitDirectionX));
        }

        private IEnumerator AnimateText(Vector3 startPos, int hitDirection)
        {
            float timer = 0f;

            // 随机抛物线参数
            // X轴：沿着受击方向的随机水平速度
            float velocityX = Random.Range(1.0f, 2.5f) * hitDirection; 
            // Y轴：随机初始向上跳跃速度
            float velocityY = Random.Range(3.5f, 5.0f); 
            // 重力加速度
            float gravity = 12f;

            Vector3 currentPos = startPos;

            while (timer < _duration)
            {
                timer += Time.deltaTime;
                float progress = timer / _duration;

                // 物理抛物线计算 (屏幕右方向 = 世界 X，屏幕上方向 = 世界 Y)
                currentPos += new Vector3(velocityX, velocityY, 0f) * Time.deltaTime;
                velocityY -= gravity * Time.deltaTime;
                transform.position = currentPos;

                // 透明度淡出 (后半段开始淡出)
                if (progress > 0.5f)
                {
                    float alpha = 1f - ((progress - 0.5f) * 2f);
                    foreach (var sr in _digitRenderers)
                    {
                        if (sr != null)
                        {
                            Color c = sr.color;
                            c.a = alpha;
                            sr.color = c;
                        }
                    }
                }

                yield return null;
            }

            // 播放完毕，彻底销毁
            Destroy(gameObject);
        }
    }
}