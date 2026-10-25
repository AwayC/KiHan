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
    /// 顶部角色状态 (一名玩家)：头像、名字、血条、查克拉
    /// 绑定一次角色后只读其状态；只在数值变化时修改 UI
    /// </summary>
    public class FighterStatusView : MonoBehaviour
    {
        [SerializeField] private Image _headshot;
        [SerializeField] private TMP_Text _name;
        [SerializeField] private HpBarView _hpBar;
        [SerializeField] private BeanListView _chakra;

        private CharacterObj _character;

        public void Bind(CharacterObj character)
        {
            _character = character;
            if (character == null)
            {
                gameObject.SetActive(false);
                return;
            }
            gameObject.SetActive(true);

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

        private static float HpRatio(CharacterObj character)
        {
            return character.maxHp > FP.Zero ? (character.Hp / character.maxHp).AsFloat() : 0f;
        }
    }
}
