using UnityEditor;

/// <summary>
/// Resources/Backgrounds, Resources/Characters 의 이미지를 항상 Single 스프라이트로 임포트한다.
/// 이 프로젝트의 기본 설정은 이미지를 자동으로 여러 조각(Multiple)으로 나눠서,
/// 그대로 두면 Resources.Load&lt;Sprite&gt;가 이미지 전체가 아닌 첫 조각만 돌려준다.
/// </summary>
public class StageSpriteImporter : AssetPostprocessor
{
    private static readonly string[] Folders =
    {
        "Assets/Resources/Backgrounds/",
        "Assets/Resources/Characters/",
    };

    void OnPreprocessTexture()
    {
        bool target = false;
        foreach (var folder in Folders)
        {
            if (assetPath.StartsWith(folder)) { target = true; break; }
        }
        if (!target) return;

        var importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
    }
}
