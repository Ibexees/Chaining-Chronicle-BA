using System.Collections.Generic;
using CC.Core.Grid;
using CC.Core.Stage;
using CC.Data;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace CC.Game.Stage
{
    /// <summary>
    /// Draws a StageLayout into Unity Tilemaps. Contains no rules. Walls are only drawn next to
    /// walkable cells, the solid rock in between stays empty.
    /// </summary>
    public sealed class StageView : MonoBehaviour
    {
        private Tilemap _ground;
        private Tilemap _overlay;
        private readonly Dictionary<Sprite, Tile> _tiles = new Dictionary<Sprite, Tile>();

        /// <summary>Centre of a cell in world space. One cell is one world unit.</summary>
        public static Vector3 CellToWorld(GridPos pos) => new Vector3(pos.X + 0.5f, pos.Y + 0.5f, 0f);

        public void Build(StageLayout layout, BiomeDef biome)
        {
            EnsureTilemaps();
            _ground.ClearAllTiles();
            _overlay.ClearAllTiles();

            var grid = layout.Grid;
            for (int x = 0; x < grid.Width; x++)
            {
                for (int y = 0; y < grid.Height; y++)
                {
                    var pos = new GridPos(x, y);
                    var cell = new Vector3Int(x, y, 0);
                    switch (grid.Get(pos))
                    {
                        case TileType.Floor:
                            _ground.SetTile(cell, TileFor(PickFloor(biome, layout.Seed, x, y)));
                            break;
                        case TileType.StairsDown:
                            _ground.SetTile(cell, TileFor(biome.floorSprites[0]));
                            _overlay.SetTile(cell, TileFor(biome.stairsSprite));
                            break;
                        default:
                            if (TouchesWalkable(grid, pos))
                                _ground.SetTile(cell, TileFor(Pick(biome.wallSprites, Hash(layout.Seed, x, y))));
                            break;
                    }
                }
            }
        }

        public void Clear()
        {
            if (_ground != null) _ground.ClearAllTiles();
            if (_overlay != null) _overlay.ClearAllTiles();
        }

        private static Sprite PickFloor(BiomeDef biome, int seed, int x, int y)
        {
            var sprites = biome.floorSprites;
            int hash = Hash(seed, x, y);
            // Plain tile most of the time, decoration on roughly every sixth cell.
            if (sprites.Length > 1 && hash % 6 == 0) return sprites[1 + hash / 6 % (sprites.Length - 1)];
            return sprites[0];
        }

        private static Sprite Pick(Sprite[] sprites, int hash) => sprites[hash % sprites.Length];

        private static int Hash(int seed, int x, int y) =>
            unchecked(x * 73856093 ^ y * 19349663 ^ seed * 83492791) & 0x7fffffff;

        private static bool TouchesWalkable(TileGrid grid, GridPos pos)
        {
            for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                    if (grid.IsWalkable(new GridPos(pos.X + dx, pos.Y + dy))) return true;
            return false;
        }

        /// <summary>Tile for a sprite, scaled so the sprite fills exactly one cell whatever its pixels per unit.</summary>
        private Tile TileFor(Sprite sprite)
        {
            if (_tiles.TryGetValue(sprite, out var tile)) return tile;

            tile = ScriptableObject.CreateInstance<Tile>();
            tile.sprite = sprite;
            tile.colliderType = Tile.ColliderType.None;
            float scale = sprite.pixelsPerUnit / sprite.rect.width;
            tile.transform = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            _tiles[sprite] = tile;
            return tile;
        }

        private void EnsureTilemaps()
        {
            if (_ground != null) return;

            var gridObject = new GameObject("Grid", typeof(UnityEngine.Grid));
            gridObject.transform.SetParent(transform, false);
            _ground = CreateTilemap(gridObject.transform, "Ground", 0);
            _overlay = CreateTilemap(gridObject.transform, "Overlay", 1);
        }

        private static Tilemap CreateTilemap(Transform parent, string name, int sortingOrder)
        {
            var go = new GameObject(name, typeof(Tilemap), typeof(TilemapRenderer));
            go.transform.SetParent(parent, false);
            go.GetComponent<TilemapRenderer>().sortingOrder = sortingOrder;
            return go.GetComponent<Tilemap>();
        }

        private void OnDestroy()
        {
            foreach (var tile in _tiles.Values)
                if (tile != null) Destroy(tile);
        }
    }
}
