using System.Windows.Media;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Project;
using YukkuriMovieMaker.Project.Items;

namespace FontEffect;

/// <summary>
/// テキスト描画が参照する書式設定ひとまとまり。
/// テキストアイテム・ボイスアイテム（字幕を個別設定）・キャラクター（字幕の既定設定）は
/// 同名のプロパティを別々の型で持つため、ここに集めてから影のテキストアイテムへ写す。
/// </summary>
internal readonly record struct TextSettings(
    string Font,
    bool Bold,
    bool Italic,
    bool Underline,
    bool Strikethrough,
    Animation FontSize,
    Animation LineHeight2,
    Animation LetterSpacing2,
    WordWrap WordWrap,
    Animation MaxWidth,
    BasePoint BasePoint,
    Color FontColor,
    Style Style,
    Color StyleColor,
    bool IsTrimEndSpace,
    bool IsDevidedPerCharacter,
    double DisplayInterval,
    TypewriterAnimationDirection DisplayDirection,
    double HideInterval,
    TypewriterAnimationDirection HideDirection)
{
    public static TextSettings From(TextItem x) => new(
        x.Font, x.Bold, x.Italic, x.Underline, x.Strikethrough,
        x.FontSize, x.LineHeight2, x.LetterSpacing2, x.WordWrap, x.MaxWidth, x.BasePoint,
        x.FontColor, x.Style, x.StyleColor, x.IsTrimEndSpace, x.IsDevidedPerCharacter,
        x.DisplayInterval, x.DisplayDirection, x.HideInterval, x.HideDirection);

    public static TextSettings From(VoiceItem x) => new(
        x.Font, x.Bold, x.Italic, x.Underline, x.Strikethrough,
        x.FontSize, x.LineHeight2, x.LetterSpacing2, x.WordWrap, x.MaxWidth, x.BasePoint,
        x.FontColor, x.Style, x.StyleColor, x.IsTrimEndSpace, x.IsDevidedPerCharacter,
        x.DisplayInterval, x.DisplayDirection, x.HideInterval, x.HideDirection);

    public static TextSettings From(Character x) => new(
        x.Font, x.Bold, x.Italic, x.Underline, x.Strikethrough,
        x.FontSize, x.LineHeight2, x.LetterSpacing2, x.WordWrap, x.MaxWidth, x.BasePoint,
        x.FontColor, x.Style, x.StyleColor, x.IsTrimEndSpace, x.IsDevidedPerCharacter,
        x.DisplayInterval, x.DisplayDirection, x.HideInterval, x.HideDirection);

    /// <summary>
    /// 設定を影のテキストアイテムへ写す。値が同じなら何もしない（描画キャッシュが保たれる）。
    /// </summary>
    public void ApplyTo(TextItem to)
    {
        to.Font = Font;
        to.Bold = Bold;
        to.Italic = Italic;
        to.Underline = Underline;
        to.Strikethrough = Strikethrough;
        CopyAnimation(FontSize, to.FontSize);
        CopyAnimation(LineHeight2, to.LineHeight2);
        CopyAnimation(LetterSpacing2, to.LetterSpacing2);
        to.WordWrap = WordWrap;
        CopyAnimation(MaxWidth, to.MaxWidth);
        to.BasePoint = BasePoint;
        to.FontColor = FontColor;
        to.Style = Style;
        to.StyleColor = StyleColor;
        to.IsTrimEndSpace = IsTrimEndSpace;
        to.IsDevidedPerCharacter = IsDevidedPerCharacter;
        to.DisplayInterval = DisplayInterval;
        to.DisplayDirection = DisplayDirection;
        to.HideInterval = HideInterval;
        to.HideDirection = HideDirection;
    }

    public static void CopyAnimation(Animation from, Animation to)
    {
        if (!to.DeepEquals(from))
            to.CopyFrom(from);
    }
}
