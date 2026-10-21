// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.21.0, GPL-3.0-or-later
// 9da28eb58adbe065be1e4baef779a448acd38e0f
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.Rendering;

namespace ExtraPotions.Wildblood.Adopted.Aspects
{
    /// <summary>
    /// The look of an echo: the boss's own body, white and nearly clear (the user: "white, still detailed, with like 8%
    /// opacity"). See-through faces alone stack up wherever the body overlaps itself, so every body renderer draws twice,
    /// after everything else in the frame: first depth only (Unity's Hidden/Internal-Colored, blend Zero/One), then, as
    /// a child renderer on the same mesh and bones, the body's own texture lightened on the game's Sprites/Default shader
    /// (unlit, alpha-blended, depth tested) where that depth is the nearest, so only one layer shows. The same technique
    /// as OpenKeep's construction ghosts, for skinned bodies. The film children join the body's level-of-detail group
    /// beside the renderer they copy, so only the shown level draws. Particles, trails and lights on the body go dark: the
    /// echo is the body alone. No shadows. <see cref="Tune"/> changes the look live (DevBridge eval). When a shader is
    /// missing the echo keeps the boss's look.
    /// </summary>
    internal static class EchoLook
    {
        /// <summary>The name of every film child, so a second pass over a body never films a film.</summary>
        private const string FilmName = "ecr echo film";

        /// <summary>After every transparent thing of the frame (water, smoke), so the depth pass hides none of them.</summary>
        private const int DepthQueue = 3990;

        /// <summary>The film's colour: the texture lightened toward white, at 8 % opacity.</summary>
        private static Color _film = new Color(2.2f, 2.2f, 2.2f, 0.08f);

        private static readonly Dictionary<Material, Material> Films = new Dictionary<Material, Material>();
        private static readonly List<Renderer> Found = new List<Renderer>();
        private static Material? _depth;
        private static Shader? _filmShader;
        private static bool _textured = true;
        private static bool _tried;

        /// <summary>Every renderer of the body not filmed yet gets its depth pass and film; the rest go dark.</summary>
        public static void Apply(Character echo)
        {
            if (!Ready())
            {
                return;
            }
            Dictionary<Renderer, Renderer> films = new Dictionary<Renderer, Renderer>();
            echo.GetComponentsInChildren(true, Found);
            foreach (Renderer renderer in Found)
            {
                Treat(renderer, films);
            }
            Found.Clear();
            JoinLevels(echo, films);
            foreach (Light light in echo.GetComponentsInChildren<Light>(true))
            {
                light.enabled = false;
            }
        }

        /// <summary>
        /// Live tuning (DevBridge: <c>ExtraPotions.Wildblood.Adopted.Aspects.EchoLook.Tune(0.08, 2.2, true)</c>): opacity, how much
        /// the texture is lightened (1 = as is), and whether it shows; every echo's film changes at once.
        /// </summary>
        public static string Tune(double alpha, double brightness, bool texture)
        {
            float b = (float)brightness;
            _film = new Color(b, b, b, (float)alpha);
            _textured = texture;
            foreach (KeyValuePair<Material, Material> pair in Films)
            {
                Dress(pair.Value, pair.Key);
            }
            return string.Format(CultureInfo.InvariantCulture, "echo: alpha {0}, brightness {1}, texture {2}", alpha,
                brightness, texture);
        }

        private static void Treat(Renderer renderer, Dictionary<Renderer, Renderer> films)
        {
            if (renderer.gameObject.name == FilmName || Filmed(renderer))
            {
                return;
            }
            Renderer? film = renderer switch
            {
                SkinnedMeshRenderer skinned => Split(skinned),
                MeshRenderer mesh => Split(mesh),
                _ => null,
            };
            if (film != null)
            {
                films[renderer] = film;
            }
            else if (!(renderer is SkinnedMeshRenderer) && !(renderer is MeshRenderer))
            {
                renderer.enabled = false; // particles, trails, lines: the echo is the body alone
            }
        }

        /// <summary>Filmed once already: it has its film child (a level tint may since have copied its depth material).</summary>
        private static bool Filmed(Renderer renderer) => renderer.transform.Find(FilmName) != null;

        private static Renderer Split(SkinnedMeshRenderer renderer)
        {
            SkinnedMeshRenderer drawn = NewFilm(renderer).AddComponent<SkinnedMeshRenderer>();
            drawn.sharedMesh = renderer.sharedMesh;
            drawn.bones = renderer.bones;
            drawn.rootBone = renderer.rootBone;
            drawn.localBounds = renderer.localBounds;
            drawn.quality = renderer.quality;
            drawn.updateWhenOffscreen = renderer.updateWhenOffscreen;
            Dress(renderer, drawn);
            return drawn;
        }

        private static Renderer? Split(MeshRenderer renderer)
        {
            MeshFilter filter = renderer.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null)
            {
                renderer.enabled = false;
                return null;
            }
            GameObject face = NewFilm(renderer);
            face.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
            MeshRenderer drawn = face.AddComponent<MeshRenderer>();
            Dress(renderer, drawn);
            return drawn;
        }

        private static GameObject NewFilm(Renderer renderer)
        {
            GameObject face = new GameObject(FilmName) { layer = renderer.gameObject.layer };
            face.transform.SetParent(renderer.transform, false);
            return face;
        }

        /// <summary>The body renderer writes only depth; its film draws the lightened texture where that depth is nearest.</summary>
        private static void Dress(Renderer body, Renderer film)
        {
            Material[] originals = body.sharedMaterials;
            Material[] depths = new Material[originals.Length];
            Material[] faces = new Material[originals.Length];
            for (int k = 0; k < originals.Length; k++)
            {
                depths[k] = _depth!;
                faces[k] = FilmOf(originals[k]);
            }
            body.sharedMaterials = depths;
            film.sharedMaterials = faces;
            Quiet(body);
            Quiet(film);
        }

        private static Material FilmOf(Material? original)
        {
            if (original != null && Films.TryGetValue(original, out Material film) && film != null)
            {
                return film;
            }
            film = new Material(_filmShader) { name = "ecr echo film", renderQueue = DepthQueue + 1 };
            Dress(film, original);
            if (original != null)
            {
                Films[original] = film;
            }
            return film;
        }

        private static void Dress(Material film, Material? original)
        {
            film.color = _film;
            film.mainTexture = _textured && original != null && original.HasProperty("_MainTex") ? original.mainTexture : null;
        }

        private static void Quiet(Renderer renderer)
        {
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        /// <summary>Each film joins its body renderer's level of detail, so the levels not shown stay unseen.</summary>
        private static void JoinLevels(Character echo, Dictionary<Renderer, Renderer> films)
        {
            LODGroup group = echo.GetComponentInChildren<LODGroup>(true);
            if (group == null || films.Count == 0)
            {
                return;
            }
            LOD[] levels = group.GetLODs();
            for (int i = 0; i < levels.Length; i++)
            {
                List<Renderer> renderers = new List<Renderer>(levels[i].renderers);
                foreach (Renderer renderer in levels[i].renderers)
                {
                    if (renderer != null && films.TryGetValue(renderer, out Renderer film))
                    {
                        renderers.Add(film);
                    }
                }
                levels[i].renderers = renderers.ToArray();
            }
            group.SetLODs(levels);
        }

        /// <summary>The depth material and the film shader, found at first use; false when either shader is missing.</summary>
        private static bool Ready()
        {
            if (!_tried)
            {
                _tried = true;
                Shader depthShader = Shader.Find("Hidden/Internal-Colored");
                _filmShader = Shader.Find("Sprites/Default");
                if (depthShader != null && _filmShader != null)
                {
                    _depth = MakeDepth(depthShader);
                }
            }
            return _depth != null;
        }

        /// <summary>Depth only: no colour (blend Zero/One), depth written and tested, both sides.</summary>
        private static Material MakeDepth(Shader shader)
        {
            Material material = new Material(shader) { name = "ecr echo depth", renderQueue = DepthQueue };
            material.SetInt("_SrcBlend", (int)BlendMode.Zero);
            material.SetInt("_DstBlend", (int)BlendMode.One);
            material.SetInt("_ZWrite", 1);
            material.SetInt("_ZTest", (int)CompareFunction.LessEqual);
            material.SetInt("_Cull", (int)CullMode.Off);
            return material;
        }
    }
}
