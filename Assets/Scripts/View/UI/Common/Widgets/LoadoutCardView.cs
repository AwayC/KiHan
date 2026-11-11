using KiHan.Config;

namespace KiHan.UI
{
    /// <summary>
    /// 出战角色卡 (对战 Loading 的 3v3 卡片 CharInfoCard3v3 与 1v1 卡片)：立绘、角色名、佩戴的秘卷 (图标 + 名字，没有佩戴时隐藏)
    /// </summary>
    public partial class LoadoutCardView : UIView
    {
        public void Bind(PlayerLoadout loadout)
        {
            CharacterManifest manifest = loadout != null ? CharacterRegistry.Find(loadout.characterId) : null;
            _headshot.sprite = manifest != null ? manifest.Portrait : null;
            _headshot.enabled = _headshot.sprite != null;
            _charName.text = manifest != null ? manifest.displayName : string.Empty;

            SecretScrollEntry scroll = loadout != null ? SecretScrollRegistry.Find(loadout.secret) : null;
            bool hasScroll = scroll != null;
            if (_secretCard.gameObject.activeSelf != hasScroll) _secretCard.gameObject.SetActive(hasScroll);
            if (_secretName.gameObject.activeSelf != hasScroll) _secretName.gameObject.SetActive(hasScroll);
            if (!hasScroll) return;
            _secretCard.Bind(scroll);
            _secretName.text = scroll.name;
        }
    }
}
