using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace CC.Editor
{
    /// <summary>
    /// Gives every sprite in Assets/Export the same pivot, the sprite centre. The tile sheet and the snow
    /// tile were imported with a bottom-left pivot, so a tile was drawn half a cell off its grid cell
    /// while the entities, which use a centred pivot, were not. Only the import settings change, the
    /// image files stay untouched. Idempotent. Menu: Chaining Chronicle/Normalize Sprite Pivots.
    /// </summary>
    public static class SpritePivotNormalizer
    {
        private const string Root = "Assets/Export";
        private static readonly Vector2 Center = new Vector2(0.5f, 0.5f);

        [MenuItem("Chaining Chronicle/Normalize Sprite Pivots")]
        public static void Run()
        {
            int changed = NormalizeAll();
            Debug.Log($"Chaining Chronicle sprite pivots: {changed} texture(s) set to centre pivot in {Root}.");
        }

        /// <summary>Returns the number of textures whose import settings had to be changed.</summary>
        public static int NormalizeAll()
        {
            var factories = new SpriteDataProviderFactories();
            factories.Init();

            int changedTextures = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Root }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!(AssetImporter.GetAtPath(path) is TextureImporter importer)) continue;
                if (importer.textureType != TextureImporterType.Sprite) continue;

                var provider = factories.GetSpriteEditorDataProviderFromObject(importer);
                if (provider == null) continue;
                provider.InitSpriteEditorDataProvider();

                var rects = provider.GetSpriteRects();
                bool dirty = false;
                foreach (var rect in rects)
                {
                    if (rect.alignment == SpriteAlignment.Center && rect.pivot == Center) continue;
                    rect.alignment = SpriteAlignment.Center;
                    rect.pivot = Center;
                    dirty = true;
                }

                if (!dirty) continue;
                provider.SetSpriteRects(rects);
                provider.Apply();
                importer.SaveAndReimport();
                changedTextures++;
            }
            return changedTextures;
        }
    }
}
