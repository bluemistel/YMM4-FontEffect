using System.ComponentModel.DataAnnotations;

namespace FontEffect;

/// <summary>
/// 太字・斜体のような ON/OFF 設定を「元のまま／強制ON／強制OFF」で上書きするための選択肢。
/// </summary>
public enum OverrideSwitch
{
    [Display(Name = "変更しない", Description = "テキストアイテム側の設定をそのまま使う")]
    Keep,

    [Display(Name = "オン", Description = "テキストアイテム側の設定に関わらずオンにする")]
    On,

    [Display(Name = "オフ", Description = "テキストアイテム側の設定に関わらずオフにする")]
    Off,
}
