using System.Reflection;
using YukkuriMovieMaker.Plugin;

namespace FontEffect;

/// <summary>
/// フォント変更エフェクトプラグインのエントリポイント。
/// </summary>
[PluginDetails(AuthorName = "あおもや")]
public class FontEffectPlugin : IPlugin
{
    public string Name => "フォント変更エフェクト";

    /// <summary>
    /// 使用素材一覧の「プラグイン」行は <see cref="IPlugin.Details"/> から作者情報を読むが、
    /// 既定実装は null を返すため、クラスに付けた属性を明示的に返す。
    /// </summary>
    public PluginDetailsAttribute? Details => GetType().GetCustomAttribute<PluginDetailsAttribute>();
}
