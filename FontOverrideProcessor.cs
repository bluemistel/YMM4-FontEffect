using System.Numerics;
using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;

namespace FontEffect;

/// <summary>
/// フォント変更エフェクトのプロセッサ。
/// 入力画像（元のフォントで描かれたテキスト）を、上書き設定で描き直した画像に差し替える。
/// 「文字毎に個別オブジェクト」のときは文字数ぶんプロセッサが作られるため、
/// 描き直しは <see cref="TextOverrideRenderer"/> で共有し、各プロセッサは自分の文字の画像だけを受け取る。
/// </summary>
internal sealed class FontOverrideProcessor(FontOverrideEffect item, IGraphicsDevicesAndContext devices) : IVideoEffectProcessor
{
    readonly TextOverrideRenderer renderer = TextOverrideRenderer.Acquire(item, devices);

    ID2D1Image? input;
    ID2D1Image? output;
    bool disposed;

    public ID2D1Image Output => output ?? input!;

    public void SetInput(ID2D1Image? input) => this.input = input;

    public void ClearInput()
    {
        input = null;
        output = null;
    }

    public DrawDescription Update(EffectDescription effectDescription)
    {
        var drawDesc = effectDescription.DrawDescription;

        // 出力は先頭から順に更新されるので、先頭のプロセッサが1フレームに1回だけ描き直す
        if (effectDescription.InputIndex == 0 || !renderer.IsReady)
            renderer.Update(effectDescription);

        if (!renderer.TryGetOutput(effectDescription.InputIndex, out var image, out var offsetDelta))
        {
            // テキストアイテム以外に挿された等、上書きできないときは素通しする
            output = null;
            return drawDesc;
        }

        output = image;
        return offsetDelta == Vector2.Zero
            ? drawDesc
            : drawDesc with { Draw = drawDesc.Draw + new Vector3(offsetDelta, 0f) };
    }

    public void Dispose()
    {
        if (disposed)
            return;
        disposed = true;
        input = null;
        output = null;
        renderer.Release();
    }
}
