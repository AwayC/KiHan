using System;
using System.Collections.Generic;
using KiHan.Asset;
using UnityEngine;
using View.UI;

namespace View.Component
{
    /// <summary>
    /// 伤害跳字对象池：加载阶段预载数字字体并预建节点，局内只做复用 (不 Load、不新建)
    /// 池耗尽时复用最早显示的节点，保证局内不产生新实例
    /// </summary>
    public class DamageTextPool
    {
        /// <summary>
        /// 本机玩家造成的伤害 (白字)
        /// </summary>
        private const string DealtFontPath = "UI/Number/DamageDealt";

        /// <summary>
        /// 本机玩家受到的伤害 (红字)
        /// </summary>
        private const string TakenFontPath = "UI/Number/DamageTaken";

        private readonly DigitFont _dealtFont;
        private readonly DigitFont _takenFont;
        private readonly Stack<DamageTextNode> _free;
        private readonly List<DamageTextNode> _active;
        private readonly Transform _root;

        public DamageTextPool(int capacity)
        {
            _dealtFont = LoadFont(DealtFontPath);
            _takenFont = LoadFont(TakenFontPath);
            _free = new Stack<DamageTextNode>(capacity);
            _active = new List<DamageTextNode>(capacity);
            _root = new GameObject("[DamageTextPool]").transform;

            Action<DamageTextNode> release = Release;
            for (int i = 0; i < capacity; i++)
            {
                _free.Push(DamageTextNode.Create(_root, release));
            }
        }

        /// <param name="byLocalPlayer">本机玩家造成的伤害用造成伤害字体，否则用受到伤害字体</param>
        public void Show(int damage, bool byLocalPlayer, Vector3 worldPos, int hitDirectionX)
        {
            DamageTextNode node;
            if (_free.Count > 0)
            {
                node = _free.Pop();
            }
            else
            {
                // 池耗尽：复用最早显示的节点 (不扩容)
                node = _active[0];
                _active.RemoveAt(0);
            }

            _active.Add(node);
            node.Show(damage, byLocalPlayer ? _dealtFont : _takenFont, worldPos, hitDirectionX);
        }

        public void Dispose()
        {
            if (_root != null) UnityEngine.Object.Destroy(_root.gameObject);
            _free.Clear();
            _active.Clear();
        }

        private void Release(DamageTextNode node)
        {
            _active.Remove(node);
            _free.Push(node);
        }

        private static DigitFont LoadFont(string path)
        {
            DigitFont font = AssetManager.Instance.Load<DigitFont>(path);
            if (font == null) Debug.LogError($"[DamageTextPool] 未能加载数字字体: Resources/{path}");
            return font;
        }
    }
}
