using System;
using KiHan.Config;
using KiHan.Logic;
using KiHan.Math;
using TMPro;
using TrueSync;
using UnityEngine;
using UnityEngine.UI;

namespace View.UI
{
    /// <summary>
    /// 顶部角色状态 (一名玩家)：头像、名字、血条、查克拉、换人按钮 (只在训练场显示)
    /// 绑定一次角色后只读其状态；只在数值变化时修改 UI
    /// </summary>
    public partial class FighterStatusView : KiHan.UI.UIView
    {
        private CharacterObj _character;
        private Action<CharacterObj> _onChangeCharacter;

        private void Awake()
        {
            _changeCharBtn.onClick.AddListener(OnChangeCharClicked);
        }

        /// <param name="onChangeCharacter">点击换人按钮的回调 (参数为当前角色)；为 null 时隐藏换人按钮 (联机)</param>
        public void Bind(CharacterObj character, Action<CharacterObj> onChangeCharacter = null)
        {
            _character = character;
            _onChangeCharacter = onChangeCharacter;
            if (character == null)
            {
                gameObject.SetActive(false);
                return;
            }
            gameObject.SetActive(true);
            _changeCharBtn.gameObject.SetActive(onChangeCharacter != null);

            CharacterManifest manifest = CharacterRegistry.Find(character.CharacterId);
            if (manifest != null)
            {
                if (manifest.headshot != null) _headshot.sprite = manifest.headshot;
                _name.text = manifest.displayName;
            }
            else
            {
                Debug.LogError($"[FighterStatusView] 角色注册表中没有已启用的角色 {character.CharacterId}");
            }

            _hpBar.SetImmediate(HpRatio(character));
            SkillController skills = character.Skills;
            if (skills != null) _chakra.Set(skills.ChakraMax, skills.Chakra);
        }

        public void Refresh()
        {
            if (_character == null) return;
            _hpBar.SetRatio(HpRatio(_character));
            SkillController skills = _character.Skills;
            if (skills != null) _chakra.Set(skills.ChakraMax, skills.Chakra);
        }

        private void OnChangeCharClicked()
        {
            if (_character != null) _onChangeCharacter?.Invoke(_character);
        }

        private static float HpRatio(CharacterObj character)
        {
            return character.maxHp > FP.Zero ? (character.Hp / character.maxHp).AsFloat() : 0f;
        }
    }
}
