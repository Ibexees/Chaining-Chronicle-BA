using System;
using System.Collections.Generic;
using System.Linq;
using CC.Data;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace CC.Editor
{
    /// <summary>
    /// Builds the design-data assets (animator controllers, enemy and biome definitions, GameContent)
    /// from the baseline art in Assets/Export. Re-runnable: existing assets are updated in place,
    /// so references to them stay valid. Menu: Chaining Chronicle/Build Content.
    /// </summary>
    public static class ContentBuilder
    {
        private const string ContentRoot = "Assets/Content";
        private const string Export = "Assets/Export";

        /// <summary>How a baseline clip is turned into the clip that is used in a controller.</summary>
        private enum ClipKind
        {
            /// <summary>Same frames and timing as the baseline clip.</summary>
            Baseline,
            /// <summary>Walk loop: frames spaced evenly over exactly one second, so the controller can set the speed.</summary>
            WalkCycle,
            /// <summary>One standing pose: the first frame of the baseline clip.</summary>
            Still
        }

        private readonly struct StateSpec
        {
            public readonly string Name, SourcePath, ClipName;
            public readonly ClipKind Kind;

            public StateSpec(string name, string sourcePath, ClipKind kind = ClipKind.Baseline)
            {
                Name = name;
                SourcePath = sourcePath;
                Kind = kind;
                ClipName = kind == ClipKind.Still ? name : System.IO.Path.GetFileNameWithoutExtension(sourcePath);
            }
        }

        // Speed multiplier parameter of the walk states. Set at runtime so one walk cycle spans a fixed number of steps.
        public const string WalkSpeedParameter = "walkSpeed";

        // Frame rate of the clips built here. Unity extends a sprite clip by one frame past its last key.
        private const float BuiltClipFrameRate = 60f;

        private sealed class EnemyEntry
        {
            public string Id, DisplayName, Clip;
            public int FirstStage;
        }

        private static readonly EnemyEntry[] Enemies =
        {
            new EnemyEntry { Id = "mouse",        DisplayName = "Mouse",         Clip = "Enemies/mouse/mouse.anim",                FirstStage = 1 },
            new EnemyEntry { Id = "hooddude",     DisplayName = "Hood Dude",     Clip = "Enemies/HoodDude/HoodDude.anim",          FirstStage = 2 },
            new EnemyEntry { Id = "evileye",      DisplayName = "Evil Eye",      Clip = "Enemies/evilEye/evilEye.anim",            FirstStage = 3 },
            new EnemyEntry { Id = "walrus",       DisplayName = "Walrus",        Clip = "Enemies/walrus/walrus.anim",              FirstStage = 4 },
            new EnemyEntry { Id = "livingarmour", DisplayName = "Living Armour", Clip = "Enemies/LivingArmour/LivingAmour.anim",   FirstStage = 5 },
            new EnemyEntry { Id = "magmaegg",     DisplayName = "Magma Egg",     Clip = "Enemies/magmaegg/magmaegg.anim",          FirstStage = 7 },
        };

        // Indices into Tiles-Sheet.png (sliced 32x32). Floor: plain tile first, decoration after.
        private const string TileSheet = Export + "/Tiles/Tiles-Sheet.png";

        [MenuItem("Chaining Chronicle/Build Content")]
        public static void Build()
        {
            SpritePivotNormalizer.NormalizeAll();

            EnsureFolder(ContentRoot);
            EnsureFolder(ContentRoot + "/Animators");
            EnsureFolder(ContentRoot + "/Animations");
            _spritesById = null;
            EnsureFolder(ContentRoot + "/Enemies");
            EnsureFolder(ContentRoot + "/Biomes");

            // The baseline only has an idle animation facing down. For the other facings the neutral frame of
            // the matching walk clip is used as a standing pose, so the stored facing is always visible.
            const string Hero = Export + "/MainCharacter/";
            var playerController = EnsureController(ContentRoot + "/Animators/Player.controller",
                new StateSpec("idleDown", Hero + "idle.anim"),
                new StateSpec("idleUp", Hero + "walkUp.anim", ClipKind.Still),
                new StateSpec("idleLeft", Hero + "walkLeft.anim", ClipKind.Still),
                new StateSpec("idleRight", Hero + "walkRight.anim", ClipKind.Still),
                new StateSpec("walkDown", Hero + "walkDown.anim", ClipKind.WalkCycle),
                new StateSpec("walkUp", Hero + "walkUp.anim", ClipKind.WalkCycle),
                new StateSpec("walkLeft", Hero + "walkLeft.anim", ClipKind.WalkCycle),
                new StateSpec("walkRight", Hero + "walkRight.anim", ClipKind.WalkCycle));
            var pickupController = EnsureController(ContentRoot + "/Animators/Pickup.controller",
                new StateSpec("bounce", Export + "/PowerUp/BounciingExclemationMark.anim"));

            var enemyDefs = new Dictionary<string, EnemyDef>();
            foreach (var entry in Enemies)
            {
                var controller = EnsureController($"{ContentRoot}/Animators/{entry.Id}.controller", new StateSpec("idle", $"{Export}/{entry.Clip}"));
                var def = LoadOrCreate<EnemyDef>($"{ContentRoot}/Enemies/{entry.Id}.asset");
                def.id = entry.Id;
                def.displayName = entry.DisplayName;
                def.animator = controller;
                def.firstStage = entry.FirstStage;
                EditorUtility.SetDirty(def);
                enemyDefs[entry.Id] = def;
            }

            var stairs = AssetDatabase.LoadAssetAtPath<Sprite>(Export + "/Tiles/StairsDown.png");
            var snowFloor = AssetDatabase.LoadAssetAtPath<Sprite>(Export + "/Tiles/SnowTile.png");
            if (stairs == null || snowFloor == null) throw new InvalidOperationException("Stairs or snow tile sprite missing");

            var meadow = Biome("Meadow", "meadow", Sheet(6, 2, 11, 12, 1, 7), Sheet(9), stairs,
                enemyDefs, "mouse", "hooddude", "evileye");
            var snow = Biome("Snow", "snow", new[] { snowFloor }, Sheet(10), stairs,
                enemyDefs, "mouse", "walrus", "livingarmour");
            var dungeon = Biome("Dungeon", "dungeon", Sheet(13), Sheet(5), stairs,
                enemyDefs, "evileye", "livingarmour", "magmaegg");

            var content = LoadOrCreate<GameContent>(ContentRoot + "/GameContent.asset");
            content.biomes = new[] { meadow, snow, dungeon };
            content.stagesPerBiome = 3;
            content.playerAnimator = playerController;
            content.pickupAnimator = pickupController;
            EditorUtility.SetDirty(content);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Chaining Chronicle content built: " + ContentRoot);
        }

        private static BiomeDef Biome(string assetName, string id, Sprite[] floors, Sprite[] walls, Sprite stairs,
            Dictionary<string, EnemyDef> enemies, params string[] enemyIds)
        {
            var biome = LoadOrCreate<BiomeDef>($"{ContentRoot}/Biomes/{assetName}.asset");
            biome.id = id;
            biome.floorSprites = floors;
            biome.wallSprites = walls;
            biome.stairsSprite = stairs;
            biome.enemies = enemyIds.Select(e => enemies[e]).ToArray();
            EditorUtility.SetDirty(biome);
            return biome;
        }

        private static Sprite[] Sheet(params int[] indices)
        {
            var sprites = AssetDatabase.LoadAllAssetsAtPath(TileSheet).OfType<Sprite>().ToDictionary(s => s.name);
            return indices.Select(i =>
            {
                string name = $"Tiles-Sheet_{i}";
                if (!sprites.TryGetValue(name, out var sprite)) throw new InvalidOperationException($"Sprite {name} not found in {TileSheet}");
                return sprite;
            }).ToArray();
        }

        /// <summary>
        /// Creates the controller, or brings an existing one in line: one state per spec, states that are
        /// no longer listed are removed, the first spec is the default state.
        /// </summary>
        private static AnimatorController EnsureController(string path, params StateSpec[] specs)
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path)
                             ?? AnimatorController.CreateAnimatorControllerAtPath(path);
            var machine = controller.layers[0].stateMachine;

            bool hasWalk = specs.Any(s => s.Kind == ClipKind.WalkCycle);
            if (hasWalk && controller.parameters.All(p => p.name != WalkSpeedParameter))
            {
                controller.AddParameter(new AnimatorControllerParameter
                {
                    name = WalkSpeedParameter,
                    type = AnimatorControllerParameterType.Float,
                    defaultFloat = 1f
                });
            }

            foreach (var spec in specs)
            {
                var state = machine.states.Select(s => s.state).FirstOrDefault(s => s.name == spec.Name)
                            ?? machine.AddState(spec.Name);
                state.motion = RebuildClip(spec.SourcePath, spec.Kind, spec.ClipName);
                bool scaled = spec.Kind == ClipKind.WalkCycle;
                state.speedParameter = scaled ? WalkSpeedParameter : string.Empty;
                state.speedParameterActive = scaled;
            }

            foreach (var obsolete in machine.states.Where(s => specs.All(x => x.Name != s.state.name)).ToArray())
                machine.RemoveState(obsolete.state);

            machine.defaultState = machine.states.Select(s => s.state).First(s => s.name == specs[0].Name);
            EditorUtility.SetDirty(controller);
            return controller;
        }

        // The baseline clips in Assets/Export reference sprites by a texture GUID that does not match the
        // imported textures of this project (only the GUID differs, the sprite ids inside the texture are
        // the same). They would animate to "no sprite". The baseline files stay untouched: a copy with the
        // same timing is written to Assets/Content/Animations, bound to the sprites of the current import.
        private static AnimationClip RebuildClip(string sourcePath, ClipKind kind, string clipName)
        {
            string text = System.IO.File.ReadAllText(sourcePath);
            var keys = System.Text.RegularExpressions.Regex.Matches(text,
                @"- time: (?<t>[-0-9.eE]+)\s+value: \{fileID: (?<id>-?\d+), guid: [0-9a-f]+, type: 3\}");
            if (keys.Count == 0) throw new InvalidOperationException($"No sprite keyframes found in {sourcePath}");

            var sprites = SpritesByLocalId();
            var frames = new ObjectReferenceKeyframe[keys.Count];
            for (int i = 0; i < keys.Count; i++)
            {
                long id = long.Parse(keys[i].Groups["id"].Value);
                if (!sprites.TryGetValue(id, out var sprite))
                    throw new InvalidOperationException($"Sprite id {id} from {sourcePath} not found in any imported texture");
                frames[i] = new ObjectReferenceKeyframe
                {
                    time = float.Parse(keys[i].Groups["t"].Value, System.Globalization.CultureInfo.InvariantCulture),
                    value = sprite
                };
            }

            var rate = System.Text.RegularExpressions.Regex.Match(text, @"m_SampleRate: ([0-9.]+)");
            var loop = System.Text.RegularExpressions.Regex.Match(text, @"m_LoopTime: (\d)");

            frames = Retime(frames, kind);

            string clipPath = $"{ContentRoot}/Animations/{clipName}_{System.IO.Directory.GetParent(sourcePath).Name}.anim";
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            if (clip == null)
            {
                clip = new AnimationClip();
                AssetDatabase.CreateAsset(clip, clipPath);
            }

            clip.frameRate = kind != ClipKind.Baseline ? BuiltClipFrameRate
                : rate.Success ? float.Parse(rate.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture)
                : 12f;
            AnimationUtility.SetObjectReferenceCurve(clip, EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite"), frames);
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = kind != ClipKind.Baseline || !loop.Success || loop.Groups[1].Value == "1";
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            EditorUtility.SetDirty(clip);
            return clip;
        }

        /// <summary>
        /// Walk cycle: the baseline clips have different lengths (0.6 to 1.4 s) and some end with a repeat of the
        /// first frame to close the loop. Drop that repeat and space the frames evenly over one second, so the
        /// speed of every walk state can be set the same way. Still: the first frame only, held for one second.
        /// The closing key sits one frame before the second mark because Unity adds that frame to the clip, so the
        /// clip is exactly one second long.
        /// </summary>
        private static ObjectReferenceKeyframe[] Retime(ObjectReferenceKeyframe[] source, ClipKind kind)
        {
            if (kind == ClipKind.Baseline) return source;

            var first = source[0].value;
            float span = 1f - 1f / BuiltClipFrameRate;
            if (kind == ClipKind.Still)
            {
                return new[]
                {
                    new ObjectReferenceKeyframe { time = 0f, value = first },
                    new ObjectReferenceKeyframe { time = span, value = first }
                };
            }

            int count = source.Length;
            if (count > 1 && source[count - 1].value == source[0].value) count--;

            var result = new ObjectReferenceKeyframe[count + 1];
            for (int i = 0; i < count; i++)
                result[i] = new ObjectReferenceKeyframe { time = span * i / count, value = source[i].value };
            // The closing key gives the last frame its duration; it shows the first frame again as the loop restarts.
            result[count] = new ObjectReferenceKeyframe { time = span, value = first };
            return result;
        }

        private static Dictionary<long, Sprite> _spritesById;

        private static Dictionary<long, Sprite> SpritesByLocalId()
        {
            if (_spritesById != null) return _spritesById;

            var map = new Dictionary<long, Sprite>();
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Export }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                foreach (var sprite in AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>())
                    if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(sprite, out _, out long localId))
                        map[localId] = sprite;
            }
            return _spritesById = map;
        }

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;

            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }
    }
}
