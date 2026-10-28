using System.ComponentModel.DataAnnotations;
using System.Windows.Media;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.ItemEditor.CustomVisibilityAttributes;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin;
using YukkuriMovieMaker.Plugin.Effects;
using YukkuriMovieMaker.Project;
using YukkuriMovieMaker.Project.Items;
using YukkuriMovieMaker.Resources.Localization;

namespace FontEffect;

/// <summary>
/// テキストアイテムに挿すと、フォントや文字間隔などのテキスト設定を上書きして描画し直すエフェクト。
/// 元のテキストアイテムの値は書き換えず、エフェクトを外せば元の見た目に戻る。
/// </summary>
/// <remarks>
/// 映像エフェクトは本来ラスタライズ済みの画像しか受け取れないため、
/// 親テキストアイテムの複製に上書き値を適用し、YMM4 標準のテキスト描画で描き直した画像に差し替える。
/// 前段のエフェクトの効果は失われるので、エフェクト一覧の一番上に置くことを推奨する。
/// </remarks>
[VideoEffect("フォント変更", ["テキスト"], ["フォント", "font", "文字間隔", "行間", "書体"], IsEffectItemSupported = false, IsAviUtlSupported = false)]
[PluginDetails(AuthorName = "あおもや")]
public class FontOverrideEffect : VideoEffectBase
{
    public override string Label => IsFontOverridden && !string.IsNullOrEmpty(Font) ? $"フォント変更 ({Font})" : "フォント変更";

    #region フォント

    [Display(GroupName = "フォント", Name = "フォントを変更", Description = "テキストアイテムのフォントを上書きする", Order = 100)]
    [ToggleSlider]
    public bool IsFontOverridden { get => isFontOverridden; set => Set(ref isFontOverridden, value, nameof(IsFontOverridden), nameof(Label)); }
    bool isFontOverridden = true;

    [Display(GroupName = "フォント", Name = "フォント", Description = "上書きするフォント", Order = 110)]
    [FontComboBox]
    [ShowPropertyEditorWhen(nameof(IsFontOverridden), true)]
    public string Font { get => font; set => Set(ref font, value, nameof(Font), nameof(Label)); }
    string font = Texts.DefaultFont;

    [Display(GroupName = "フォント", Name = "太字", Description = "太字を上書きする", Order = 120)]
    [EnumComboBox]
    public OverrideSwitch Bold { get => bold; set => Set(ref bold, value); }
    OverrideSwitch bold = OverrideSwitch.Keep;

    [Display(GroupName = "フォント", Name = "イタリック", Description = "イタリックを上書きする", Order = 130)]
    [EnumComboBox]
    public OverrideSwitch Italic { get => italic; set => Set(ref italic, value); }
    OverrideSwitch italic = OverrideSwitch.Keep;

    [Display(GroupName = "フォント", Name = "サイズを変更", Description = "テキストアイテムの文字サイズを上書きする", Order = 140)]
    [ToggleSlider]
    public bool IsFontSizeOverridden { get => isFontSizeOverridden; set => Set(ref isFontSizeOverridden, value); }
    bool isFontSizeOverridden;

    [Display(GroupName = "フォント", Name = "サイズ", Description = "上書きする文字サイズ", Order = 150)]
    [AnimationSlider("F1", "px", 1.0, 50.0)]
    [ShowPropertyEditorWhen(nameof(IsFontSizeOverridden), true)]
    public Animation FontSize { get; } = new Animation(34.0, 1.0, 100000.0);

    #endregion

    #region 文字間隔・行間

    [Display(GroupName = "文字間隔・行間", Name = "文字間隔を変更", Description = "テキストアイテムの文字間隔を上書きする", Order = 200)]
    [ToggleSlider]
    public bool IsLetterSpacingOverridden { get => isLetterSpacingOverridden; set => Set(ref isLetterSpacingOverridden, value); }
    bool isLetterSpacingOverridden;

    [Display(GroupName = "文字間隔・行間", Name = "文字間隔", Description = "上書きする文字間隔", Order = 210)]
    [AnimationSlider("F1", "px", -100.0, 100.0)]
    [ShowPropertyEditorWhen(nameof(IsLetterSpacingOverridden), true)]
    public Animation LetterSpacing { get; } = new Animation(0.0, -100000.0, 100000.0);

    [Display(GroupName = "文字間隔・行間", Name = "行間を変更", Description = "テキストアイテムの行間を上書きする", Order = 220)]
    [ToggleSlider]
    public bool IsLineHeightOverridden { get => isLineHeightOverridden; set => Set(ref isLineHeightOverridden, value); }
    bool isLineHeightOverridden;

    [Display(GroupName = "文字間隔・行間", Name = "行間", Description = "上書きする行間", Order = 230)]
    [AnimationSlider("F1", "％", 0.0, 200.0)]
    [ShowPropertyEditorWhen(nameof(IsLineHeightOverridden), true)]
    public Animation LineHeight { get; } = new Animation(100.0, 0.0, 100000.0);

    #endregion

    #region 色・装飾

    [Display(GroupName = "色・装飾", Name = "文字色を変更", Description = "テキストアイテムの文字色を上書きする", Order = 300)]
    [ToggleSlider]
    public bool IsFontColorOverridden { get => isFontColorOverridden; set => Set(ref isFontColorOverridden, value); }
    bool isFontColorOverridden;

    [Display(GroupName = "色・装飾", Name = "文字色", Description = "上書きする文字色", Order = 310)]
    [ColorPicker]
    [ShowPropertyEditorWhen(nameof(IsFontColorOverridden), true)]
    public Color FontColor { get => fontColor; set => Set(ref fontColor, value); }
    Color fontColor = Colors.White;

    [Display(GroupName = "色・装飾", Name = "装飾を変更", Description = "テキストアイテムの装飾（縁取り・影など）を上書きする", Order = 320)]
    [ToggleSlider]
    public bool IsStyleOverridden { get => isStyleOverridden; set => Set(ref isStyleOverridden, value); }
    bool isStyleOverridden;

    [Display(GroupName = "色・装飾", Name = "装飾", Description = "上書きする装飾の種類", Order = 330)]
    [EnumComboBox]
    [ShowPropertyEditorWhen(nameof(IsStyleOverridden), true)]
    public Style Style { get => style; set => Set(ref style, value); }
    Style style = Style.Normal;

    [Display(GroupName = "色・装飾", Name = "装飾色", Description = "上書きする装飾色", Order = 340)]
    [ColorPicker]
    [ShowPropertyEditorWhen(nameof(IsStyleOverridden), true)]
    public Color StyleColor { get => styleColor; set => Set(ref styleColor, value); }
    Color styleColor = Colors.Black;

    #endregion

    #region 詳細

    [Display(GroupName = "詳細", Name = "部分指定も上書き", Description = "テキスト内で部分的に指定されたフォント・文字間隔も、このエフェクトの値で上書きする", Order = 900)]
    [ToggleSlider]
    public bool IsPartialDecorationOverridden { get => isPartialDecorationOverridden; set => Set(ref isPartialDecorationOverridden, value); }
    bool isPartialDecorationOverridden = true;

    #endregion

    public override IEnumerable<string> CreateExoVideoFilters(int keyFrameIndex, ExoOutputDescription exoOutputDescription) => [];

    public override IVideoEffectProcessor CreateVideoEffect(IGraphicsDevicesAndContext devices) => new FontOverrideProcessor(this, devices);

    public override IEnumerable<TimelineResource> GetResources()
    {
        foreach (var resource in base.GetResources())
            yield return resource;
        if (IsFontOverridden && !string.IsNullOrEmpty(Font))
            yield return TimelineResource.FromFont(Font);
    }

    protected override IEnumerable<IAnimatable> GetAnimatables() => [FontSize, LetterSpacing, LineHeight];
}
