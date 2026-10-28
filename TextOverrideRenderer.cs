using System.Collections.Immutable;
using System.Numerics;
using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Project;
using YukkuriMovieMaker.Project.Items;

namespace FontEffect;

/// <summary>
/// 親テキストアイテムの「影」を作り、上書き設定を適用して YMM4 標準のテキスト描画で描き直す。
/// </summary>
/// <remarks>
/// 映像エフェクトには親アイテムへの参照が渡されないため、
/// 描画中のシーンのタイムラインから「同じレイヤーで現在フレームを含むテキスト／ボイスアイテム」を探して親とみなす。
/// ボイスアイテムの字幕は描画処理こそ別だが中身はテキストアイテムと同等なので、
/// 字幕の実効設定（個別設定 or キャラクター設定）を写した影のテキストアイテムで描き直す。
/// （グループ制御から掛けた場合も、このレイヤー照合で個々のテキストアイテムを特定できる）
/// 影アイテムは親から毎フレーム設定を写すので、テキストアイテム側の編集にも追従する。
/// 1つのアイテム（＝描画コンテキスト）につき1つ作られ、文字毎のプロセッサ間で共有される。
/// </remarks>
internal sealed class TextOverrideRenderer : IDisposable
{
    static readonly Lock registryLock = new();
    static readonly List<TextOverrideRenderer> registry = [];

    readonly FontOverrideEffect effect;
    readonly IGraphicsDevicesAndContext devices;
    int refCount;

    BaseItem? owner;
    TextItem? shadow;
    ISource? shadowSource;

    // 文字毎に個別オブジェクトのとき、元の文字位置を知るために上書きなしで描いておく比較用
    TextItem? reference;
    ISource? referenceSource;

    ImmutableList<TextDecoration>? decorationsCacheSource;
    ImmutableList<TextDecoration>? decorationsCache;
    (bool font, bool spacing) decorationsCacheKey;

    ID2D1CommandList? emptyImage;

    public bool IsReady { get; private set; }

    TextOverrideRenderer(FontOverrideEffect effect, IGraphicsDevicesAndContext devices)
    {
        this.effect = effect;
        this.devices = devices;
    }

    public static TextOverrideRenderer Acquire(FontOverrideEffect effect, IGraphicsDevicesAndContext devices)
    {
        lock (registryLock)
        {
            var renderer = registry.FirstOrDefault(x => ReferenceEquals(x.effect, effect) && ReferenceEquals(x.devices, devices));
            if (renderer is null)
            {
                renderer = new TextOverrideRenderer(effect, devices);
                registry.Add(renderer);
            }
            renderer.refCount++;
            return renderer;
        }
    }

    public void Release()
    {
        lock (registryLock)
        {
            if (--refCount > 0)
                return;
            registry.Remove(this);
        }
        Dispose();
    }

    public void Update(EffectDescription desc)
    {
        var scene = desc.Scenes?.OfType<Scene>().FirstOrDefault(x => x.ID == desc.SceneId);
        var target = scene is null ? null : FindTarget(scene.Timeline, desc);
        if (scene is null || target is null || !CanRender(target))
        {
            ResetShadow();
            return;
        }

        if (!ReferenceEquals(owner, target) || shadow is null || !shadow.KeyFrames.Frames.SequenceEqual(target.KeyFrames.Frames))
        {
            ResetShadow();
            owner = target;
            shadow = CreateShadow(target);
            shadowSource = shadow.CreateVideoSource(devices, scene);
        }

        CopySettings(target, shadow);
        ApplyOverrides(shadow);

        // TimelineSource が各アイテムのソースへ渡すのと同じ、アイテム基準の時間情報を作る
        var itemFrame = Math.Max(0, desc.TimelinePosition.Frame - target.Frame);
        var itemDesc = new TimelineItemSourceDescription(desc, itemFrame, Math.Max(1, target.Length), target.Layer);
        shadowSource!.Update(itemDesc);

        if (shadow.IsDevidedPerCharacter)
        {
            if (reference is null)
            {
                reference = CreateShadow(target);
                referenceSource = reference.CreateVideoSource(devices, scene);
            }
            CopySettings(target, reference);
            referenceSource!.Update(itemDesc);
        }
        else
        {
            DisposeReference();
        }

        IsReady = true;
    }

    /// <summary>
    /// 描き直した画像のうち index 番目の出力と、元の文字位置からのずれを返す。
    /// 上書きできない状態なら false（呼び出し側は入力を素通しする）。
    /// </summary>
    public bool TryGetOutput(int index, out ID2D1Image image, out Vector2 offsetDelta)
    {
        if (!IsReady || shadowSource is null)
        {
            image = null!;
            offsetDelta = default;
            return false;
        }

        var outputs = shadowSource.Outputs;
        if (index < 0 || outputs.Count <= index)
        {
            // フォントの違いで出力数が減った（合字など）文字は何も描かない
            image = GetEmptyImage();
            offsetDelta = default;
            return true;
        }

        var shadowOutput = outputs[index];
        image = shadowOutput.Output;

        // 入力の描画位置には元の文字位置（DrawingOffset）が含まれているので、差分だけずらす。
        // 一括描画のときは DrawingOffset は常に 0。
        var originalOffset = referenceSource is { } refSource && index < refSource.Outputs.Count
            ? refSource.Outputs[index].DrawingOffset
            : Vector2.Zero;
        offsetDelta = shadowOutput.DrawingOffset - originalOffset;
        return true;
    }

    BaseItem? FindTarget(Timeline timeline, EffectDescription desc)
    {
        var frame = desc.TimelinePosition.Frame;
        BaseItem? sameLayer = null;
        foreach (var item in timeline.Items)
        {
            if (item is not (TextItem or VoiceItem) || item.Layer != desc.Layer || frame < item.Frame || item.Frame + item.Length <= frame)
                continue;
            // ボイスアイテムの VideoEffects は字幕のエフェクト（個別設定 or キャラクター設定）を返す
            if (((IVideoItem)item).VideoEffects.Any(x => ReferenceEquals(x, effect)))
                return (BaseItem)item;
            sameLayer ??= (BaseItem)item;
        }
        return sameLayer;
    }

    /// <summary>
    /// 字幕を表示しないボイスアイテムは元々何も描かないので、上書きもしない。
    /// </summary>
    static bool CanRender(BaseItem item) => item switch
    {
        TextItem => true,
        VoiceItem voice => voice.Character is { } character && voice.JimakuVisibility switch
        {
            JimakuVisibility.Hidden => false,
            JimakuVisibility.UseCharacterSetting => character.IsJimakuVisible,
            _ => true,
        },
        _ => false,
    };

    static TextItem CreateShadow(BaseItem item) => item switch
    {
        TextItem text => (TextItem)text.GetClone(),
        // 位置・キーフレーム等も引き継ぐ YMM4 の「テキストアイテムに変換」相当のコンストラクタ
        VoiceItem voice => new TextItem(voice),
        _ => throw new NotSupportedException(item.GetType().Name),
    };

    /// <summary>
    /// テキスト描画が参照する設定を親から影へ写す。値が同じなら何もしない。
    /// </summary>
    static void CopySettings(BaseItem from, TextItem to)
    {
        to.Length = from.Length;
        switch (from)
        {
            case TextItem text:
                to.Text = text.Text;
                to.Decorations = text.Decorations;
                TextSettings.From(text).ApplyTo(to);
                break;
            case VoiceItem voice:
                to.Text = voice.Serif ?? string.Empty;
                to.Decorations = voice.Decorations;
                (voice.JimakuVisibility == JimakuVisibility.Custom ? TextSettings.From(voice) : TextSettings.From(voice.Character)).ApplyTo(to);
                break;
        }
    }

    void ApplyOverrides(TextItem target)
    {
        var isFontOverridden = effect.IsFontOverridden && !string.IsNullOrEmpty(effect.Font);
        if (isFontOverridden)
            target.Font = effect.Font;
        if (effect.Bold != OverrideSwitch.Keep)
            target.Bold = effect.Bold == OverrideSwitch.On;
        if (effect.Italic != OverrideSwitch.Keep)
            target.Italic = effect.Italic == OverrideSwitch.On;
        if (effect.IsFontSizeOverridden)
            TextSettings.CopyAnimation(effect.FontSize, target.FontSize);
        if (effect.IsLetterSpacingOverridden)
            TextSettings.CopyAnimation(effect.LetterSpacing, target.LetterSpacing2);
        if (effect.IsLineHeightOverridden)
            TextSettings.CopyAnimation(effect.LineHeight, target.LineHeight2);
        if (effect.IsFontColorOverridden)
            target.FontColor = effect.FontColor;
        if (effect.IsStyleOverridden)
        {
            target.Style = effect.Style;
            target.StyleColor = effect.StyleColor;
        }

        if (effect.IsPartialDecorationOverridden && (isFontOverridden || effect.IsLetterSpacingOverridden))
            target.Decorations = OverrideDecorations(target.Decorations, isFontOverridden, effect.IsLetterSpacingOverridden);
    }

    /// <summary>
    /// 部分指定（リッチテキスト）のフォント・文字間隔を外し、アイテム全体の値に従わせる。
    /// 毎フレーム作り直すと無駄なので、元のリストと条件が同じ間は前回の結果を使い回す。
    /// </summary>
    ImmutableList<TextDecoration> OverrideDecorations(ImmutableList<TextDecoration> source, bool font, bool spacing)
    {
        if (ReferenceEquals(source, decorationsCacheSource) && decorationsCacheKey == (font, spacing) && decorationsCache is not null)
            return decorationsCache;

        decorationsCacheSource = source;
        decorationsCacheKey = (font, spacing);
        decorationsCache = source.ConvertAll(x => x is null ? x! : x with
        {
            Font = font ? null : x.Font,
            CharacterSpacing = spacing ? null : x.CharacterSpacing,
        });
        return decorationsCache;
    }

    ID2D1Image GetEmptyImage()
    {
        if (emptyImage is null)
        {
            var dc = devices.DeviceContext;
            emptyImage = dc.CreateCommandList();
            dc.Target = emptyImage;
            dc.BeginDraw();
            dc.EndDraw();
            dc.Target = null;
            emptyImage.Close();
        }
        return emptyImage;
    }

    void DisposeReference()
    {
        referenceSource?.Dispose();
        referenceSource = null;
        reference = null;
    }

    void ResetShadow()
    {
        IsReady = false;
        shadowSource?.Dispose();
        shadowSource = null;
        shadow = null;
        owner = null;
        DisposeReference();
        decorationsCacheSource = null;
        decorationsCache = null;
    }

    public void Dispose()
    {
        ResetShadow();
        emptyImage?.Dispose();
        emptyImage = null;
    }
}
