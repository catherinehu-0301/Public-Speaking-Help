using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class OfficeSceneFinisherEditor
{
    private const string OfficeScenePath = "Assets/Scenes/Office.unity";
    private const string OfficeRootName = "uploads_files_2988061_Format3";
    private const string GeneratedRootName = "_OfficeGeneratedEnhancements";
    private const string SessionKeyPrefix = "VirtualStageVR.OfficeSceneFinisher.";
    private const string OfficeMaterialFolder = "Assets/Materials/OfficeAuto";

    private enum OfficeMaterialKind
    {
        TableSurface,
        ChairUpholstery,
        MetalFrame,
        Carpet,
        WallPaint,
        CeilingWhite,
        Glass,
        ScreenDark,
        DarkWood,
        BlackTrim,
        LightLens,
        FallbackNeutral
    }

    private enum RoomSide
    {
        MinX,
        MaxX,
        MinZ,
        MaxZ
    }

    private sealed class MaterialLibrary
    {
        public readonly Dictionary<OfficeMaterialKind, Material> Materials = new Dictionary<OfficeMaterialKind, Material>();

        public Material this[OfficeMaterialKind kind] => Materials[kind];

        public bool IsManagedMaterial(Material material)
        {
            return material != null && Materials.Values.Contains(material);
        }
    }

    private sealed class RendererInfo
    {
        public MeshRenderer Renderer;
        public Bounds Bounds;
        public Vector3 Size;
        public Vector3 Center;
        public float FootprintArea;
        public float WallAreaX;
        public float WallAreaZ;
        public float Thinness;
        public bool IsHorizontal;
        public bool IsVertical;
        public bool IsTall;
    }

    private sealed class RoomAnalysis
    {
        public Bounds RoomBounds;
        public float FloorY;
        public float CeilingY;
        public float Height;
        public bool LongAxisIsX;
        public float LongSize;
        public float ShortSize;
        public Vector3 Center;
        public RendererInfo FloorCandidate;
        public RendererInfo CeilingCandidate;
        public RendererInfo TableCandidate;
        public RendererInfo ScreenCandidate;
        public RoomSide WindowSide = RoomSide.MinX;
        public HashSet<MeshRenderer> FloorRenderers = new HashSet<MeshRenderer>();
        public HashSet<MeshRenderer> CeilingRenderers = new HashSet<MeshRenderer>();
        public HashSet<MeshRenderer> WallRenderers = new HashSet<MeshRenderer>();
        public HashSet<MeshRenderer> WindowRenderers = new HashSet<MeshRenderer>();
        public HashSet<MeshRenderer> TrimRenderers = new HashSet<MeshRenderer>();
        public HashSet<MeshRenderer> AccentRenderers = new HashSet<MeshRenderer>();
        public HashSet<MeshRenderer> ChairRenderers = new HashSet<MeshRenderer>();
        public HashSet<MeshRenderer> MetalRenderers = new HashSet<MeshRenderer>();
    }

    private struct WallDecision
    {
        public RoomSide Side;
        public bool ShouldCreate;
        public float CoverageRatio;
    }

    static OfficeSceneFinisherEditor()
    {
        EditorSceneManager.sceneOpened += OnSceneOpened;
        EditorApplication.delayCall += ApplyToActiveOfficeSceneOnce;
    }

    [MenuItem("Tools/Virtual Stage VR/Office/Finish Office Scene")]
    private static void FinishOfficeSceneManually()
    {
        ApplyToScene(EditorSceneManager.GetActiveScene(), true);
    }

    public static void BatchFinishOfficeScene()
    {
        Scene scene = EditorSceneManager.OpenScene(OfficeScenePath, OpenSceneMode.Single);
        ApplyToScene(scene, true);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
    }

    [MenuItem("Tools/Virtual Stage VR/Office/Reset Auto-Finish Session Flag")]
    private static void ResetSessionFlag()
    {
        SessionState.SetBool(SessionKeyPrefix + OfficeScenePath, false);
    }

    private static void OnSceneOpened(Scene scene, OpenSceneMode mode)
    {
        ApplyToScene(scene, false);
    }

    private static void ApplyToActiveOfficeSceneOnce()
    {
        ApplyToScene(EditorSceneManager.GetActiveScene(), false);
    }

    private static void ApplyToScene(Scene scene, bool force)
    {
        if (!scene.IsValid() || string.IsNullOrEmpty(scene.path) || !scene.path.EndsWith(OfficeScenePath))
        {
            return;
        }

        string sessionKey = SessionKeyPrefix + scene.path;
        if (!force && SessionState.GetBool(sessionKey, false))
        {
            return;
        }

        SessionState.SetBool(sessionKey, true);

        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        GameObject officeRoot = FindOfficeRoot();
        if (officeRoot == null)
        {
            Debug.LogWarning("Office scene finisher could not find the imported office root.");
            return;
        }

        MeshRenderer[] renderers = officeRoot.GetComponentsInChildren<MeshRenderer>(true);
        if (renderers.Length == 0)
        {
            Debug.LogWarning("Office scene finisher found the office root but no MeshRenderers under it.", officeRoot);
            return;
        }

        MaterialLibrary materialLibrary = EnsureMaterialLibrary();
        RoomAnalysis analysis = AnalyzeRoom(renderers);

        StringBuilder report = new StringBuilder(4096);
        report.AppendLine("Office Scene Finisher");
        report.AppendLine("Room bounds: " + FormatVector(analysis.RoomBounds.size));
        report.AppendLine("Floor/Ceiling: " + analysis.FloorY.ToString("0.00") + " / " + analysis.CeilingY.ToString("0.00"));
        report.AppendLine("Window side guess: " + analysis.WindowSide);

        if (analysis.FloorCandidate != null)
        {
            report.AppendLine("Floor candidate: " + analysis.FloorCandidate.Renderer.name + " @ " + FormatVector(analysis.FloorCandidate.Center));
        }

        if (analysis.CeilingCandidate != null)
        {
            report.AppendLine("Ceiling candidate: " + analysis.CeilingCandidate.Renderer.name + " @ " + FormatVector(analysis.CeilingCandidate.Center));
        }

        AssignMaterials(renderers, analysis, materialLibrary, report);
        GenerateArchitecturalShell(analysis, materialLibrary, report);
        GenerateCeilingLights(analysis, materialLibrary, report);

        EditorSceneManager.MarkSceneDirty(scene);
        AssetDatabase.SaveAssets();
        EditorSceneManager.SaveScene(scene);
        Debug.Log(report.ToString(), officeRoot);
    }

    private static GameObject FindOfficeRoot()
    {
        GameObject namedRoot = GameObject.Find(OfficeRootName);
        if (namedRoot != null)
        {
            return namedRoot;
        }

        MeshRenderer[] allRenderers = Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        GameObject bestRoot = null;
        int bestCount = 0;

        foreach (MeshRenderer renderer in allRenderers)
        {
            Transform root = renderer.transform.root;
            int count = root.GetComponentsInChildren<MeshRenderer>(true).Length;
            if (count > bestCount)
            {
                bestCount = count;
                bestRoot = root.gameObject;
            }
        }

        return bestRoot;
    }

    private static MaterialLibrary EnsureMaterialLibrary()
    {
        EnsureFolderExists(OfficeMaterialFolder);

        Texture2D woodTexture = LoadTexture("Assets/Textures/wood texture.jpeg", false);
        Texture2D darkWoodTexture = LoadTexture("Assets/Textures/Image20250828181143.jpg", false);
        Texture2D fabricBase = LoadTexture("Assets/VRTemplateAssets/Materials/Primitive/fabric_Base_color.png", false);
        Texture2D fabricNormal = LoadTexture("Assets/VRTemplateAssets/Materials/Primitive/fabric_Normal.png", true);
        Texture2D wallBase = LoadTexture("Assets/VRTemplateAssets/Materials/Environment/wall2_Base_color.png", false);
        Texture2D wallNormal = LoadTexture("Assets/VRTemplateAssets/Materials/Environment/wall2_Normal.png", true);

        MaterialLibrary library = new MaterialLibrary();
        library.Materials[OfficeMaterialKind.TableSurface] = GetOrCreateMaterial("M_Office_TableSurface");
        SetupOpaqueMaterial(library[OfficeMaterialKind.TableSurface], new Color(0.91f, 0.91f, 0.90f), 0f, 0.32f, null, null, 1f, Vector2.one);

        library.Materials[OfficeMaterialKind.ChairUpholstery] = GetOrCreateMaterial("M_Office_ChairUpholstery");
        SetupOpaqueMaterial(library[OfficeMaterialKind.ChairUpholstery], new Color(0.88f, 0.88f, 0.89f), 0f, 0.38f, fabricBase, fabricNormal, 0.18f, new Vector2(2f, 2f));

        library.Materials[OfficeMaterialKind.MetalFrame] = GetOrCreateMaterial("M_Office_MetalFrame");
        SetupOpaqueMaterial(library[OfficeMaterialKind.MetalFrame], new Color(0.80f, 0.82f, 0.84f), 1f, 0.82f, null, null, 1f, Vector2.one);

        library.Materials[OfficeMaterialKind.Carpet] = GetOrCreateMaterial("M_Office_Carpet");
        SetupOpaqueMaterial(library[OfficeMaterialKind.Carpet], new Color(0.77f, 0.75f, 0.71f), 0f, 0.05f, null, null, 1f, Vector2.one);

        library.Materials[OfficeMaterialKind.WallPaint] = GetOrCreateMaterial("M_Office_WallPaint");
        SetupOpaqueMaterial(library[OfficeMaterialKind.WallPaint], new Color(0.95f, 0.94f, 0.91f), 0f, 0.12f, wallBase, wallNormal, 0.06f, new Vector2(2f, 2f));

        library.Materials[OfficeMaterialKind.CeilingWhite] = GetOrCreateMaterial("M_Office_CeilingWhite");
        SetupOpaqueMaterial(library[OfficeMaterialKind.CeilingWhite], new Color(0.97f, 0.97f, 0.96f), 0f, 0.08f, null, null, 1f, Vector2.one);

        library.Materials[OfficeMaterialKind.Glass] = GetOrCreateMaterial("M_Office_Glass");
        SetupTransparentMaterial(library[OfficeMaterialKind.Glass], new Color(0.84f, 0.90f, 0.95f, 0.20f), 0.96f);

        library.Materials[OfficeMaterialKind.ScreenDark] = GetOrCreateMaterial("M_Office_ScreenDark");
        SetupOpaqueMaterial(library[OfficeMaterialKind.ScreenDark], new Color(0.07f, 0.07f, 0.08f), 0f, 0.86f, null, null, 1f, Vector2.one);

        library.Materials[OfficeMaterialKind.DarkWood] = GetOrCreateMaterial("M_Office_DarkWood");
        SetupOpaqueMaterial(library[OfficeMaterialKind.DarkWood], new Color(0.30f, 0.22f, 0.16f), 0f, 0.48f, darkWoodTexture != null ? darkWoodTexture : woodTexture, null, 1f, new Vector2(1.5f, 1.5f));

        library.Materials[OfficeMaterialKind.BlackTrim] = GetOrCreateMaterial("M_Office_BlackTrim");
        SetupOpaqueMaterial(library[OfficeMaterialKind.BlackTrim], new Color(0.10f, 0.10f, 0.11f), 0f, 0.45f, null, null, 1f, Vector2.one);

        library.Materials[OfficeMaterialKind.LightLens] = GetOrCreateMaterial("M_Office_LightLens");
        SetupEmissiveMaterial(library[OfficeMaterialKind.LightLens], new Color(0.98f, 0.96f, 0.92f), new Color(2.4f, 2.25f, 2.05f, 1f));

        library.Materials[OfficeMaterialKind.FallbackNeutral] = GetOrCreateMaterial("M_Office_FallbackNeutral");
        SetupOpaqueMaterial(library[OfficeMaterialKind.FallbackNeutral], new Color(0.76f, 0.78f, 0.80f), 0f, 0.18f, null, null, 1f, Vector2.one);

        AssetDatabase.SaveAssets();
        return library;
    }

    private static RoomAnalysis AnalyzeRoom(MeshRenderer[] renderers)
    {
        List<RendererInfo> info = BuildRendererInfo(renderers);
        Bounds roomBounds = info[0].Bounds;
        for (int i = 1; i < info.Count; i++)
        {
            roomBounds.Encapsulate(info[i].Bounds);
        }

        RoomAnalysis analysis = new RoomAnalysis
        {
            RoomBounds = roomBounds,
            FloorY = roomBounds.min.y,
            CeilingY = roomBounds.max.y,
            Height = roomBounds.size.y,
            LongAxisIsX = roomBounds.size.x >= roomBounds.size.z,
            LongSize = Mathf.Max(roomBounds.size.x, roomBounds.size.z),
            ShortSize = Mathf.Min(roomBounds.size.x, roomBounds.size.z),
            Center = roomBounds.center
        };

        analysis.FloorCandidate = FindFloorCandidate(info, analysis);
        analysis.CeilingCandidate = FindCeilingCandidate(info, analysis);

        if (analysis.FloorCandidate != null)
        {
            analysis.FloorY = analysis.FloorCandidate.Bounds.max.y;
        }

        if (analysis.CeilingCandidate != null)
        {
            analysis.CeilingY = analysis.CeilingCandidate.Bounds.min.y;
        }

        analysis.TableCandidate = FindTableCandidate(info, analysis);
        analysis.ScreenCandidate = FindScreenCandidate(info, analysis);
        analysis.WindowSide = DetectWindowSide(info, analysis);

        foreach (RendererInfo item in info)
        {
            MeshRenderer renderer = item.Renderer;
            if (IsFloor(item, analysis))
            {
                analysis.FloorRenderers.Add(renderer);
                continue;
            }

            if (IsCeiling(item, analysis))
            {
                analysis.CeilingRenderers.Add(renderer);
                continue;
            }

            if (item.Renderer == analysis.ScreenCandidate?.Renderer)
            {
                continue;
            }

            if (IsWindowGlass(item, analysis))
            {
                analysis.WindowRenderers.Add(renderer);
                continue;
            }

            if (IsTrim(item, analysis))
            {
                analysis.TrimRenderers.Add(renderer);
                continue;
            }

            if (IsAccentPanel(item, analysis))
            {
                analysis.AccentRenderers.Add(renderer);
                continue;
            }

            if (IsWall(item, analysis))
            {
                analysis.WallRenderers.Add(renderer);
                continue;
            }
        }

        if (analysis.TableCandidate != null)
        {
            Bounds expandedTable = analysis.TableCandidate.Bounds;
            expandedTable.Expand(new Vector3(1.6f, 1.0f, 1.6f));

            foreach (RendererInfo item in info)
            {
                if (analysis.FloorRenderers.Contains(item.Renderer) ||
                    analysis.CeilingRenderers.Contains(item.Renderer) ||
                    analysis.WindowRenderers.Contains(item.Renderer) ||
                    analysis.TrimRenderers.Contains(item.Renderer) ||
                    analysis.WallRenderers.Contains(item.Renderer) ||
                    analysis.AccentRenderers.Contains(item.Renderer) ||
                    item.Renderer == analysis.TableCandidate.Renderer ||
                    item.Renderer == analysis.ScreenCandidate?.Renderer)
                {
                    continue;
                }

                if (expandedTable.Contains(item.Center) || HorizontalDistance(item.Center, analysis.TableCandidate.Center) < 2.2f)
                {
                    if (LooksLikeMetalSupport(item, analysis))
                    {
                        analysis.MetalRenderers.Add(item.Renderer);
                    }
                    else
                    {
                        analysis.ChairRenderers.Add(item.Renderer);
                    }
                }
            }
        }

        return analysis;
    }

    private static List<RendererInfo> BuildRendererInfo(MeshRenderer[] renderers)
    {
        var info = new List<RendererInfo>(renderers.Length);
        foreach (MeshRenderer renderer in renderers)
        {
            if (renderer == null)
            {
                continue;
            }

            MeshFilter filter = renderer.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null)
            {
                continue;
            }

            Bounds bounds = renderer.bounds;
            Vector3 size = bounds.size;
            info.Add(new RendererInfo
            {
                Renderer = renderer,
                Bounds = bounds,
                Size = size,
                Center = bounds.center,
                FootprintArea = size.x * size.z,
                WallAreaX = size.y * size.z,
                WallAreaZ = size.y * size.x,
                Thinness = Mathf.Min(size.x, Mathf.Min(size.y, size.z)),
                IsHorizontal = size.y < Mathf.Min(size.x, size.z) * 0.18f,
                IsVertical = Mathf.Min(size.x, size.z) < size.y * 0.18f,
                IsTall = size.y > 1.8f
            });
        }

        return info;
    }

    private static RendererInfo FindFloorCandidate(List<RendererInfo> info, RoomAnalysis analysis)
    {
        RendererInfo namedGround = info.FirstOrDefault(item => NormalizeName(item.Renderer.name) == "ground");
        if (namedGround != null)
        {
            return namedGround;
        }

        float roomFootprint = analysis.RoomBounds.size.x * analysis.RoomBounds.size.z;
        RendererInfo best = null;
        float bestScore = float.MinValue;

        foreach (RendererInfo item in info)
        {
            if (!item.IsHorizontal || item.FootprintArea < roomFootprint * 0.18f)
            {
                continue;
            }

            float score = item.FootprintArea * 2f - (item.Center.y - analysis.RoomBounds.min.y) * 8f;
            if (score > bestScore)
            {
                bestScore = score;
                best = item;
            }
        }

        return best;
    }

    private static RendererInfo FindCeilingCandidate(List<RendererInfo> info, RoomAnalysis analysis)
    {
        RendererInfo namedCeiling = info
            .Where(item => item.IsHorizontal && NormalizeName(item.Renderer.name).Contains("컨"))
            .OrderByDescending(item => item.Center.y)
            .FirstOrDefault();
        if (namedCeiling != null)
        {
            return namedCeiling;
        }

        float roomFootprint = analysis.RoomBounds.size.x * analysis.RoomBounds.size.z;
        RendererInfo best = null;
        float bestScore = float.MinValue;

        foreach (RendererInfo item in info)
        {
            if (analysis.FloorCandidate != null && item.Renderer == analysis.FloorCandidate.Renderer)
            {
                continue;
            }

            if (!item.IsHorizontal || item.FootprintArea < roomFootprint * 0.12f)
            {
                continue;
            }

            if (item.Center.y < analysis.RoomBounds.center.y + analysis.Height * 0.1f)
            {
                continue;
            }

            float score = item.FootprintArea * 2f + (item.Center.y - analysis.RoomBounds.center.y) * 8f;
            if (score > bestScore)
            {
                bestScore = score;
                best = item;
            }
        }

        return best;
    }

    private static RendererInfo FindTableCandidate(List<RendererInfo> info, RoomAnalysis analysis)
    {
        RendererInfo namedTable = info.FirstOrDefault(item => NormalizeName(item.Renderer.name) == "2");
        if (namedTable != null)
        {
            return namedTable;
        }

        float roomFootprint = analysis.RoomBounds.size.x * analysis.RoomBounds.size.z;
        RendererInfo best = null;
        float bestScore = float.MinValue;

        foreach (RendererInfo item in info)
        {
            if (analysis.FloorCandidate != null && item.Renderer == analysis.FloorCandidate.Renderer)
            {
                continue;
            }

            if (!item.IsHorizontal)
            {
                continue;
            }

            if (item.FootprintArea > roomFootprint * 0.45f || item.FootprintArea < roomFootprint * 0.03f)
            {
                continue;
            }

            float heightAboveFloor = item.Center.y - analysis.FloorY;
            if (heightAboveFloor < 0.55f || heightAboveFloor > 1.15f)
            {
                continue;
            }

            float edgePenalty = DistanceToNearestSide(item.Center, analysis.RoomBounds) < 1.1f ? 3f : 0f;
            float score = item.FootprintArea * 3f - Mathf.Abs(item.Center.x - analysis.Center.x) - Mathf.Abs(item.Center.z - analysis.Center.z) - edgePenalty;
            if (score > bestScore)
            {
                bestScore = score;
                best = item;
            }
        }

        return best;
    }

    private static RendererInfo FindScreenCandidate(List<RendererInfo> info, RoomAnalysis analysis)
    {
        RendererInfo namedScreen = info.FirstOrDefault(item => NormalizeName(item.Renderer.name).StartsWith("box009"));
        if (namedScreen != null)
        {
            return namedScreen;
        }

        RendererInfo best = null;
        float bestScore = float.MinValue;

        foreach (RendererInfo item in info)
        {
            if (!item.IsVertical)
            {
                continue;
            }

            float width = Mathf.Max(item.Size.x, item.Size.z);
            float aspect = width / Mathf.Max(0.01f, item.Size.y);
            float heightAboveFloor = item.Center.y - analysis.FloorY;
            if (heightAboveFloor < 1f || heightAboveFloor > 2.8f)
            {
                continue;
            }

            if (width < 0.8f || width > 3.5f || item.Size.y < 0.55f || item.Size.y > 2.2f)
            {
                continue;
            }

            if (aspect < 1.2f || aspect > 2.4f)
            {
                continue;
            }

            float edgeBonus = 1.5f - DistanceToNearestSide(item.Bounds, analysis.RoomBounds);
            float score = (item.WallAreaX + item.WallAreaZ) + edgeBonus - Mathf.Abs(aspect - 1.6f);
            if (score > bestScore)
            {
                bestScore = score;
                best = item;
            }
        }

        return best;
    }

    private static RoomSide DetectWindowSide(List<RendererInfo> info, RoomAnalysis analysis)
    {
        float bestScore = float.MinValue;
        RoomSide bestSide = RoomSide.MinX;

        foreach (RoomSide side in System.Enum.GetValues(typeof(RoomSide)))
        {
            float score = 0f;
            foreach (RendererInfo item in info)
            {
                if (!item.IsVertical || !item.IsTall)
                {
                    continue;
                }

                if (DistanceToSide(item.Bounds, analysis.RoomBounds, side) > 0.45f)
                {
                    continue;
                }

                float horizontalThickness = side == RoomSide.MinX || side == RoomSide.MaxX ? item.Size.x : item.Size.z;
                if (horizontalThickness > 0.35f)
                {
                    continue;
                }

                if (Mathf.Max(item.Size.x, item.Size.z) < 0.18f)
                {
                    continue;
                }

                score += item.WallAreaX + item.WallAreaZ;
            }

            if (score > bestScore)
            {
                bestScore = score;
                bestSide = side;
            }
        }

        return bestSide;
    }

    private static void AssignMaterials(MeshRenderer[] renderers, RoomAnalysis analysis, MaterialLibrary library, StringBuilder report)
    {
        var assignments = new Dictionary<OfficeMaterialKind, int>();

        foreach (MeshRenderer renderer in renderers)
        {
            MeshFilter filter = renderer.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null)
            {
                continue;
            }

            Material[] shared = renderer.sharedMaterials;
            if (shared == null || shared.Length == 0)
            {
                shared = new Material[1];
            }

            Material[] updated = new Material[shared.Length];
            OfficeMaterialKind kind = ClassifyRenderer(renderer, analysis);

            for (int i = 0; i < shared.Length; i++)
            {
                Material current = shared[i];
                bool preserveExisting = shared.Length > 1 && current != null && !library.IsManagedMaterial(current);
                updated[i] = preserveExisting ? current : library[kind];
            }

            if (!MaterialsMatch(shared, updated))
            {
                Undo.RecordObject(renderer, "Finish Office Scene");
                renderer.sharedMaterials = updated;
                EditorUtility.SetDirty(renderer);
            }

            if (!assignments.ContainsKey(kind))
            {
                assignments[kind] = 0;
            }

            assignments[kind]++;
        }

        report.AppendLine("Material assignment counts:");
        foreach (KeyValuePair<OfficeMaterialKind, int> entry in assignments.OrderByDescending(x => x.Value))
        {
            report.AppendLine("  " + entry.Key + ": " + entry.Value);
        }

        if (analysis.TableCandidate != null)
        {
            report.AppendLine("Table candidate: " + analysis.TableCandidate.Renderer.name + " @ " + FormatVector(analysis.TableCandidate.Center));
        }

        if (analysis.ScreenCandidate != null)
        {
            report.AppendLine("Screen candidate: " + analysis.ScreenCandidate.Renderer.name + " @ " + FormatVector(analysis.ScreenCandidate.Center));
        }

        AppendRendererList(report, "Floor objects", analysis.FloorRenderers);
        AppendRendererList(report, "Ceiling objects", analysis.CeilingRenderers);
        AppendRendererList(report, "Wall objects", analysis.WallRenderers);
        AppendRendererList(report, "Window objects", analysis.WindowRenderers);
        AppendRendererList(report, "Trim objects", analysis.TrimRenderers);
        AppendRendererList(report, "Accent objects", analysis.AccentRenderers);
        AppendRendererList(report, "Chair objects", analysis.ChairRenderers);
        AppendRendererList(report, "Metal objects", analysis.MetalRenderers);
    }

    private static OfficeMaterialKind ClassifyRenderer(MeshRenderer renderer, RoomAnalysis analysis)
    {
        RendererInfo explicitProbe = BuildRendererInfo(new[] { renderer }).FirstOrDefault();
        if (explicitProbe != null && TryClassifySpecificOfficeImport(renderer.name, explicitProbe, out OfficeMaterialKind explicitKind))
        {
            return explicitKind;
        }

        if (analysis.TableCandidate != null && renderer == analysis.TableCandidate.Renderer)
        {
            return OfficeMaterialKind.TableSurface;
        }

        if (analysis.ScreenCandidate != null && renderer == analysis.ScreenCandidate.Renderer)
        {
            return OfficeMaterialKind.ScreenDark;
        }

        if (analysis.FloorRenderers.Contains(renderer))
        {
            Bounds bounds = renderer.bounds;
            bool nearRoomEdge = DistanceToNearestSide(bounds.center, analysis.RoomBounds) < 0.85f;
            return nearRoomEdge ? OfficeMaterialKind.DarkWood : OfficeMaterialKind.Carpet;
        }

        if (analysis.CeilingRenderers.Contains(renderer))
        {
            return OfficeMaterialKind.CeilingWhite;
        }

        if (analysis.WindowRenderers.Contains(renderer))
        {
            return OfficeMaterialKind.Glass;
        }

        if (analysis.TrimRenderers.Contains(renderer))
        {
            return OfficeMaterialKind.BlackTrim;
        }

        if (analysis.AccentRenderers.Contains(renderer))
        {
            return OfficeMaterialKind.DarkWood;
        }

        if (analysis.WallRenderers.Contains(renderer))
        {
            return OfficeMaterialKind.WallPaint;
        }

        if (TryClassifyByName(renderer.name, out OfficeMaterialKind nameKind))
        {
            return nameKind;
        }

        if (analysis.MetalRenderers.Contains(renderer))
        {
            return OfficeMaterialKind.MetalFrame;
        }

        if (analysis.ChairRenderers.Contains(renderer))
        {
            return OfficeMaterialKind.ChairUpholstery;
        }

        RendererInfo probe = BuildRendererInfo(new[] { renderer }).FirstOrDefault();
        if (probe != null && LooksLikeMetalSupport(probe, analysis))
        {
            return OfficeMaterialKind.MetalFrame;
        }

        return OfficeMaterialKind.FallbackNeutral;
    }

    private static void GenerateArchitecturalShell(RoomAnalysis analysis, MaterialLibrary library, StringBuilder report)
    {
        GameObject generatedRoot = GetOrCreateGeneratedRoot();
        GameObject shellRoot = GetOrCreateChildRoot(generatedRoot, "Generated_Walls");
        ClearChildren(shellRoot.transform);

        float wallHeight = analysis.Height;
        float thickness = 0.06f;
        float margin = 0.08f;
        float sideLengthX = analysis.RoomBounds.size.x;
        float sideLengthZ = analysis.RoomBounds.size.z;

        var decisions = new List<WallDecision>();
        foreach (RoomSide side in System.Enum.GetValues(typeof(RoomSide)))
        {
            float coverage = ComputeWallCoverage(side, analysis);
            float sideArea = (side == RoomSide.MinX || side == RoomSide.MaxX ? sideLengthZ : sideLengthX) * wallHeight;
            float ratio = sideArea > 0.01f ? coverage / sideArea : 1f;
            bool shouldCreate = side != analysis.WindowSide && ratio < 0.08f;
            decisions.Add(new WallDecision { Side = side, CoverageRatio = ratio, ShouldCreate = shouldCreate });
        }

        foreach (WallDecision decision in decisions)
        {
            if (!decision.ShouldCreate)
            {
                continue;
            }

            CreateWallPanel(shellRoot.transform, analysis, decision.Side, library[OfficeMaterialKind.WallPaint], thickness, margin);
        }

        report.AppendLine("Generated walls:");
        foreach (WallDecision decision in decisions)
        {
            report.AppendLine("  " + decision.Side + " coverage=" + decision.CoverageRatio.ToString("0.00") + " created=" + decision.ShouldCreate);
        }
    }

    private static void GenerateCeilingLights(RoomAnalysis analysis, MaterialLibrary library, StringBuilder report)
    {
        GameObject generatedRoot = GetOrCreateGeneratedRoot();
        GameObject lightRoot = GetOrCreateChildRoot(generatedRoot, "Generated_CeilingLights");
        ClearChildren(lightRoot.transform);

        Vector3 roomCenter = analysis.Center;
        Vector3 lightCenter = analysis.TableCandidate != null ? analysis.TableCandidate.Center : roomCenter;
        float ceilingY = analysis.CeilingY - 0.12f;
        float longSpan = Mathf.Clamp(analysis.TableCandidate != null ? (analysis.LongAxisIsX ? analysis.TableCandidate.Size.x : analysis.TableCandidate.Size.z) * 1.15f : analysis.LongSize * 0.55f, 3.2f, analysis.LongSize * 0.72f);
        float rowOffset = Mathf.Clamp((analysis.TableCandidate != null ? (analysis.LongAxisIsX ? analysis.TableCandidate.Size.z : analysis.TableCandidate.Size.x) : analysis.ShortSize * 0.22f) * 0.7f, 0.7f, 1.2f);
        int columns = analysis.LongSize > 8f ? 4 : 3;

        Vector3 longAxis = analysis.LongAxisIsX ? Vector3.right : Vector3.forward;
        Vector3 crossAxis = analysis.LongAxisIsX ? Vector3.forward : Vector3.right;
        float longStart = -longSpan * 0.5f;
        float longStep = columns == 1 ? 0f : longSpan / (columns - 1);

        int fixtureCount = 0;
        for (int row = -1; row <= 1; row += 2)
        {
            for (int column = 0; column < columns; column++)
            {
                Vector3 localOffset = longAxis * (longStart + longStep * column) + crossAxis * (rowOffset * 0.5f * row);
                Vector3 position = new Vector3(lightCenter.x, ceilingY, lightCenter.z) + localOffset;
                CreateLightFixture(lightRoot.transform, position, library);
                fixtureCount++;
            }
        }

        report.AppendLine("Generated ceiling fixtures: " + fixtureCount);
    }

    private static float ComputeWallCoverage(RoomSide side, RoomAnalysis analysis)
    {
        float coverage = 0f;

        foreach (MeshRenderer renderer in analysis.WallRenderers.Concat(analysis.AccentRenderers).Concat(analysis.WindowRenderers))
        {
            Bounds bounds = renderer.bounds;
            if (DistanceToSide(bounds, analysis.RoomBounds, side) > 0.35f)
            {
                continue;
            }

            coverage += side == RoomSide.MinX || side == RoomSide.MaxX ? bounds.size.y * bounds.size.z : bounds.size.y * bounds.size.x;
        }

        return coverage;
    }

    private static void CreateWallPanel(Transform parent, RoomAnalysis analysis, RoomSide side, Material material, float thickness, float margin)
    {
        GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = "Generated_" + side + "_Wall";
        wall.transform.SetParent(parent, false);

        Vector3 position = analysis.RoomBounds.center;
        Vector3 scale = new Vector3(analysis.RoomBounds.size.x + margin, analysis.Height + margin, thickness);

        switch (side)
        {
            case RoomSide.MinX:
                position.x = analysis.RoomBounds.min.x - thickness * 0.5f;
                scale = new Vector3(thickness, analysis.Height + margin, analysis.RoomBounds.size.z + margin);
                break;
            case RoomSide.MaxX:
                position.x = analysis.RoomBounds.max.x + thickness * 0.5f;
                scale = new Vector3(thickness, analysis.Height + margin, analysis.RoomBounds.size.z + margin);
                break;
            case RoomSide.MinZ:
                position.z = analysis.RoomBounds.min.z - thickness * 0.5f;
                break;
            case RoomSide.MaxZ:
                position.z = analysis.RoomBounds.max.z + thickness * 0.5f;
                break;
        }

        wall.transform.position = position;
        wall.transform.localScale = scale;

        MeshRenderer renderer = wall.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        Object.DestroyImmediate(wall.GetComponent<Collider>());
        GameObjectUtility.SetStaticEditorFlags(wall, StaticEditorFlags.BatchingStatic | StaticEditorFlags.ContributeGI | StaticEditorFlags.OccluderStatic | StaticEditorFlags.ReflectionProbeStatic);
    }

    private static void CreateLightFixture(Transform parent, Vector3 position, MaterialLibrary library)
    {
        GameObject fixture = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        fixture.name = "Generated_Downlight";
        fixture.transform.SetParent(parent, false);
        fixture.transform.position = position;
        fixture.transform.localScale = new Vector3(0.16f, 0.012f, 0.16f);

        MeshRenderer fixtureRenderer = fixture.GetComponent<MeshRenderer>();
        fixtureRenderer.sharedMaterial = library[OfficeMaterialKind.LightLens];
        Object.DestroyImmediate(fixture.GetComponent<Collider>());
        GameObjectUtility.SetStaticEditorFlags(fixture, StaticEditorFlags.BatchingStatic | StaticEditorFlags.ContributeGI | StaticEditorFlags.ReflectionProbeStatic);

        GameObject lightObject = new GameObject("Generated_Downlight_Light");
        lightObject.transform.SetParent(fixture.transform, false);
        lightObject.transform.localPosition = Vector3.down * 0.08f;
        lightObject.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Spot;
        light.spotAngle = 78f;
        light.innerSpotAngle = 48f;
        light.range = 7f;
        light.intensity = 2.6f;
        light.shadows = LightShadows.None;
        light.color = new Color(1f, 0.97f, 0.92f, 1f);
        light.lightmapBakeType = LightmapBakeType.Baked;
    }

    private static GameObject GetOrCreateGeneratedRoot()
    {
        GameObject existing = GameObject.Find(GeneratedRootName);
        if (existing != null)
        {
            return existing;
        }

        GameObject generatedRoot = new GameObject(GeneratedRootName);
        Undo.RegisterCreatedObjectUndo(generatedRoot, "Create office generated root");
        return generatedRoot;
    }

    private static GameObject GetOrCreateChildRoot(GameObject parent, string childName)
    {
        Transform existing = parent.transform.Find(childName);
        if (existing != null)
        {
            return existing.gameObject;
        }

        GameObject child = new GameObject(childName);
        Undo.RegisterCreatedObjectUndo(child, "Create office generated child");
        child.transform.SetParent(parent.transform, false);
        return child;
    }

    private static void ClearChildren(Transform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            Undo.DestroyObjectImmediate(parent.GetChild(i).gameObject);
        }
    }

    private static bool IsFloor(RendererInfo item, RoomAnalysis analysis)
    {
        if (NormalizeName(item.Renderer.name) == "ground")
        {
            return true;
        }

        float roomFootprint = analysis.RoomBounds.size.x * analysis.RoomBounds.size.z;
        if (analysis.FloorCandidate != null && item.Renderer == analysis.FloorCandidate.Renderer)
        {
            return true;
        }

        return item.IsHorizontal &&
               item.FootprintArea > roomFootprint * 0.03f &&
               item.Bounds.max.y <= analysis.FloorY + 0.18f;
    }

    private static bool IsCeiling(RendererInfo item, RoomAnalysis analysis)
    {
        if (NormalizeName(item.Renderer.name).Contains("컨"))
        {
            return true;
        }

        float roomFootprint = analysis.RoomBounds.size.x * analysis.RoomBounds.size.z;
        if (analysis.CeilingCandidate != null && item.Renderer == analysis.CeilingCandidate.Renderer)
        {
            return true;
        }

        return item.IsHorizontal &&
               item.FootprintArea > roomFootprint * 0.03f &&
               item.Bounds.min.y >= analysis.CeilingY - 0.22f;
    }

    private static bool IsWall(RendererInfo item, RoomAnalysis analysis)
    {
        string name = NormalizeName(item.Renderer.name);
        if (StartsWithAny(name, "box021_1", "box021_2", "001_1", "box008", "ground__8_"))
        {
            return true;
        }

        if (!item.IsVertical || !item.IsTall)
        {
            return false;
        }

        if (DistanceToNearestSide(item.Bounds, analysis.RoomBounds) > 0.85f)
        {
            return false;
        }

        return item.WallAreaX + item.WallAreaZ > 3f;
    }

    private static bool IsWindowGlass(RendererInfo item, RoomAnalysis analysis)
    {
        if (!item.IsVertical || !item.IsTall)
        {
            return false;
        }

        if (DistanceToSide(item.Bounds, analysis.RoomBounds, analysis.WindowSide) > 0.35f)
        {
            return false;
        }

        float sideThickness = analysis.WindowSide == RoomSide.MinX || analysis.WindowSide == RoomSide.MaxX ? item.Size.x : item.Size.z;
        return sideThickness < 0.28f && Mathf.Max(item.Size.x, item.Size.z) > 0.28f && item.Center.y - analysis.FloorY > 0.8f;
    }

    private static bool IsTrim(RendererInfo item, RoomAnalysis analysis)
    {
        if (item.Renderer == analysis.TableCandidate?.Renderer || item.Renderer == analysis.ScreenCandidate?.Renderer)
        {
            return false;
        }

        bool ceilingStrip = item.IsHorizontal && analysis.CeilingY - item.Center.y < 0.3f && Mathf.Max(item.Size.x, item.Size.z) > 2.2f && item.Size.y < 0.12f;
        bool windowMullion = item.IsVertical && DistanceToSide(item.Bounds, analysis.RoomBounds, analysis.WindowSide) < 0.4f && Mathf.Min(item.Size.x, item.Size.z) < 0.25f && item.Size.y > analysis.Height * 0.5f;
        return ceilingStrip || windowMullion;
    }

    private static bool IsAccentPanel(RendererInfo item, RoomAnalysis analysis)
    {
        string name = NormalizeName(item.Renderer.name);
        if (StartsWithAny(name, "ground__8_"))
        {
            return true;
        }

        if (!item.IsVertical || !item.IsTall)
        {
            return false;
        }

        if (DistanceToSide(item.Bounds, analysis.RoomBounds, analysis.WindowSide) < 0.5f)
        {
            return false;
        }

        return DistanceToNearestSide(item.Bounds, analysis.RoomBounds) < 0.85f &&
               (item.WallAreaX + item.WallAreaZ) > 6f &&
               item.Center.y - analysis.FloorY > analysis.Height * 0.35f;
    }

    private static bool TryClassifySpecificOfficeImport(string rendererName, RendererInfo item, out OfficeMaterialKind kind)
    {
        kind = OfficeMaterialKind.FallbackNeutral;
        string name = NormalizeName(rendererName);

        if (name == "ground")
        {
            kind = OfficeMaterialKind.Carpet;
            return true;
        }

        if (name.Contains("컨"))
        {
            kind = OfficeMaterialKind.CeilingWhite;
            return true;
        }

        if (name == "2")
        {
            kind = OfficeMaterialKind.TableSurface;
            return true;
        }

        if (name == "3" || StartsWithAny(name, "b2_polysurface404"))
        {
            kind = OfficeMaterialKind.MetalFrame;
            return true;
        }

        if (StartsWithAny(name, "b2_polysurface383", "b2_polysurface384"))
        {
            kind = OfficeMaterialKind.BlackTrim;
            return true;
        }

        if (StartsWithAny(name, "polysurface17", "polysurface38"))
        {
            kind = OfficeMaterialKind.ChairUpholstery;
            return true;
        }

        if (StartsWithAny(name, "polysurface217", "polysurface251"))
        {
            kind = OfficeMaterialKind.MetalFrame;
            return true;
        }

        if (StartsWithAny(name, "circle"))
        {
            float diameter = Mathf.Max(item.Size.x, item.Size.z);
            kind = diameter >= 0.19f ? OfficeMaterialKind.BlackTrim : OfficeMaterialKind.LightLens;
            return true;
        }

        if (StartsWithAny(name, "cylinder001"))
        {
            kind = OfficeMaterialKind.BlackTrim;
            return true;
        }

        if (StartsWithAny(name, "box009"))
        {
            kind = OfficeMaterialKind.ScreenDark;
            return true;
        }

        if (StartsWithAny(name, "ground__8_"))
        {
            kind = OfficeMaterialKind.DarkWood;
            return true;
        }

        if (StartsWithAny(name, "box021_2"))
        {
            kind = OfficeMaterialKind.BlackTrim;
            return true;
        }

        if (StartsWithAny(name, "box021_1", "001_1", "box008"))
        {
            kind = OfficeMaterialKind.WallPaint;
            return true;
        }

        if (name.StartsWith("box0") && item.IsTall && item.Thinness < 0.18f)
        {
            kind = OfficeMaterialKind.BlackTrim;
            return true;
        }

        return false;
    }

    private static bool TryClassifyByName(string rendererName, out OfficeMaterialKind kind)
    {
        kind = OfficeMaterialKind.FallbackNeutral;
        if (string.IsNullOrWhiteSpace(rendererName))
        {
            return false;
        }

        string name = rendererName.ToLowerInvariant();

        if (ContainsAny(name, "glass", "window", "pane"))
        {
            kind = OfficeMaterialKind.Glass;
            return true;
        }

        if (ContainsAny(name, "screen", "tv", "monitor", "display"))
        {
            kind = OfficeMaterialKind.ScreenDark;
            return true;
        }

        if (ContainsAny(name, "ceiling", "roof", "soffit"))
        {
            kind = OfficeMaterialKind.CeilingWhite;
            return true;
        }

        if (ContainsAny(name, "carpet", "rug"))
        {
            kind = OfficeMaterialKind.Carpet;
            return true;
        }

        if (ContainsAny(name, "ground", "floor"))
        {
            kind = OfficeMaterialKind.Carpet;
            return true;
        }

        if (ContainsAny(name, "woodfloor", "parquet", "wood_floor"))
        {
            kind = OfficeMaterialKind.DarkWood;
            return true;
        }

        if (ContainsAny(name, "trim", "mullion", "frame", "recess"))
        {
            kind = OfficeMaterialKind.BlackTrim;
            return true;
        }

        if (ContainsAny(name, "accent", "panel", "wood"))
        {
            kind = OfficeMaterialKind.DarkWood;
            return true;
        }

        if (ContainsAny(name, "wall", "column", "pillar", "partition"))
        {
            kind = OfficeMaterialKind.WallPaint;
            return true;
        }

        if (ContainsAny(name, "metal", "chrome", "steel", "leg", "base", "support", "arm", "caster", "wheel", "cylinder", "circle"))
        {
            kind = OfficeMaterialKind.MetalFrame;
            return true;
        }

        if (ContainsAny(name, "chair", "seat", "back", "cushion", "upholstery", "fabric", "leather"))
        {
            kind = OfficeMaterialKind.ChairUpholstery;
            return true;
        }

        if (ContainsAny(name, "table", "desk", "conference", "meeting"))
        {
            kind = OfficeMaterialKind.TableSurface;
            return true;
        }

        return false;
    }

    private static string NormalizeName(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToLowerInvariant();
    }

    private static bool StartsWithAny(string value, params string[] prefixes)
    {
        for (int i = 0; i < prefixes.Length; i++)
        {
            if (value.StartsWith(prefixes[i]))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsAny(string value, params string[] tokens)
    {
        for (int i = 0; i < tokens.Length; i++)
        {
            if (value.Contains(tokens[i]))
            {
                return true;
            }
        }

        return false;
    }

    private static bool LooksLikeMetalSupport(RendererInfo item, RoomAnalysis analysis)
    {
        bool slender = item.Thinness < 0.09f && Mathf.Max(item.Size.x, Mathf.Max(item.Size.y, item.Size.z)) > 0.35f;
        bool lowAndThin = item.Center.y - analysis.FloorY < 0.75f && item.Thinness < 0.12f;
        bool tableLeg = analysis.TableCandidate != null && HorizontalDistance(item.Center, analysis.TableCandidate.Center) < 1.6f && item.Size.y > 0.45f && item.Thinness < 0.14f;
        return slender || lowAndThin || tableLeg;
    }

    private static float DistanceToNearestSide(Vector3 point, Bounds bounds)
    {
        float dx = Mathf.Min(Mathf.Abs(point.x - bounds.min.x), Mathf.Abs(point.x - bounds.max.x));
        float dz = Mathf.Min(Mathf.Abs(point.z - bounds.min.z), Mathf.Abs(point.z - bounds.max.z));
        return Mathf.Min(dx, dz);
    }

    private static float DistanceToNearestSide(Bounds itemBounds, Bounds roomBounds)
    {
        float minX = Mathf.Abs(itemBounds.min.x - roomBounds.min.x);
        float maxX = Mathf.Abs(itemBounds.max.x - roomBounds.max.x);
        float minZ = Mathf.Abs(itemBounds.min.z - roomBounds.min.z);
        float maxZ = Mathf.Abs(itemBounds.max.z - roomBounds.max.z);
        return Mathf.Min(Mathf.Min(minX, maxX), Mathf.Min(minZ, maxZ));
    }

    private static float DistanceToSide(Bounds itemBounds, Bounds roomBounds, RoomSide side)
    {
        switch (side)
        {
            case RoomSide.MinX:
                return Mathf.Abs(itemBounds.min.x - roomBounds.min.x);
            case RoomSide.MaxX:
                return Mathf.Abs(itemBounds.max.x - roomBounds.max.x);
            case RoomSide.MinZ:
                return Mathf.Abs(itemBounds.min.z - roomBounds.min.z);
            default:
                return Mathf.Abs(itemBounds.max.z - roomBounds.max.z);
        }
    }

    private static float HorizontalDistance(Vector3 a, Vector3 b)
    {
        Vector2 a2 = new Vector2(a.x, a.z);
        Vector2 b2 = new Vector2(b.x, b.z);
        return Vector2.Distance(a2, b2);
    }

    private static string FormatVector(Vector3 vector)
    {
        return vector.x.ToString("0.00") + ", " + vector.y.ToString("0.00") + ", " + vector.z.ToString("0.00");
    }

    private static void AppendRendererList(StringBuilder report, string title, IEnumerable<MeshRenderer> renderers)
    {
        MeshRenderer[] ordered = renderers.Where(r => r != null).OrderBy(r => r.name).ToArray();
        if (ordered.Length == 0)
        {
            return;
        }

        report.AppendLine(title + ":");
        foreach (MeshRenderer renderer in ordered.Take(20))
        {
            report.AppendLine("  " + renderer.name);
        }

        if (ordered.Length > 20)
        {
            report.AppendLine("  ... " + (ordered.Length - 20) + " more");
        }
    }

    private static Material GetOrCreateMaterial(string materialName)
    {
        string path = OfficeMaterialFolder + "/" + materialName + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null)
        {
            return material;
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        material = new Material(shader) { name = materialName, enableInstancing = true };
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    private static Texture2D LoadTexture(string path, bool normalMap)
    {
        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (texture == null)
        {
            return null;
        }

        if (normalMap)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null && importer.textureType != TextureImporterType.NormalMap)
            {
                importer.textureType = TextureImporterType.NormalMap;
                importer.SaveAndReimport();
                texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            }
        }

        return texture;
    }

    private static void SetupOpaqueMaterial(Material material, Color baseColor, float metallic, float smoothness, Texture2D baseMap, Texture2D normalMap, float bumpScale, Vector2 tiling)
    {
        material.shader = ResolvePreferredShader();
        material.enableInstancing = true;

        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", baseColor);
        }

        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", baseColor);
        }

        if (material.HasProperty("_Metallic"))
        {
            material.SetFloat("_Metallic", metallic);
        }

        if (material.HasProperty("_Smoothness"))
        {
            material.SetFloat("_Smoothness", smoothness);
        }

        SetTexture(material, "_BaseMap", baseMap, tiling);
        SetTexture(material, "_MainTex", baseMap, tiling);
        SetTexture(material, "_BumpMap", normalMap, Vector2.one);
        if (material.HasProperty("_BumpScale"))
        {
            material.SetFloat("_BumpScale", normalMap != null ? bumpScale : 1f);
        }

        ConfigureSurface(material, false);
        EditorUtility.SetDirty(material);
    }

    private static void SetupTransparentMaterial(Material material, Color baseColor, float smoothness)
    {
        material.shader = ResolvePreferredShader();
        material.enableInstancing = true;

        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", baseColor);
        }

        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", baseColor);
        }

        if (material.HasProperty("_Metallic"))
        {
            material.SetFloat("_Metallic", 0f);
        }

        if (material.HasProperty("_Smoothness"))
        {
            material.SetFloat("_Smoothness", smoothness);
        }

        ConfigureSurface(material, true);
        EditorUtility.SetDirty(material);
    }

    private static void SetupEmissiveMaterial(Material material, Color baseColor, Color emissionColor)
    {
        material.shader = ResolvePreferredShader();
        material.enableInstancing = true;

        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", baseColor);
        }

        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", baseColor);
        }

        if (material.HasProperty("_Metallic"))
        {
            material.SetFloat("_Metallic", 0f);
        }

        if (material.HasProperty("_Smoothness"))
        {
            material.SetFloat("_Smoothness", 0.74f);
        }

        if (material.HasProperty("_EmissionColor"))
        {
            material.EnableKeyword("_EMISSION");
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            material.SetColor("_EmissionColor", emissionColor);
        }

        ConfigureSurface(material, false);
        EditorUtility.SetDirty(material);
    }

    private static void SetTexture(Material material, string property, Texture texture, Vector2 tiling)
    {
        if (!material.HasProperty(property))
        {
            return;
        }

        material.SetTexture(property, texture);
        material.SetTextureScale(property, tiling);
    }

    private static Shader ResolvePreferredShader()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader != null)
        {
            return shader;
        }

        shader = Shader.Find("Standard");
        return shader != null ? shader : Shader.Find("Diffuse");
    }

    private static void ConfigureSurface(Material material, bool transparent)
    {
        bool isUrp = material.shader != null && material.shader.name.Contains("Universal Render Pipeline");

        if (isUrp)
        {
            material.SetOverrideTag("RenderType", transparent ? "Transparent" : "Opaque");
            if (material.HasProperty("_Surface"))
            {
                material.SetFloat("_Surface", transparent ? 1f : 0f);
            }

            if (material.HasProperty("_Blend"))
            {
                material.SetFloat("_Blend", 0f);
            }

            if (material.HasProperty("_SrcBlend"))
            {
                material.SetFloat("_SrcBlend", transparent ? (float)BlendMode.SrcAlpha : (float)BlendMode.One);
            }

            if (material.HasProperty("_DstBlend"))
            {
                material.SetFloat("_DstBlend", transparent ? (float)BlendMode.OneMinusSrcAlpha : (float)BlendMode.Zero);
            }

            if (material.HasProperty("_ZWrite"))
            {
                material.SetFloat("_ZWrite", transparent ? 0f : 1f);
            }

            if (transparent)
            {
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.renderQueue = (int)RenderQueue.Transparent;
            }
            else
            {
                material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.renderQueue = -1;
            }
        }
        else if (material.HasProperty("_Mode"))
        {
            material.SetFloat("_Mode", transparent ? 3f : 0f);
            material.SetOverrideTag("RenderType", transparent ? "Transparent" : "Opaque");
            material.SetInt("_SrcBlend", transparent ? (int)BlendMode.SrcAlpha : (int)BlendMode.One);
            material.SetInt("_DstBlend", transparent ? (int)BlendMode.OneMinusSrcAlpha : (int)BlendMode.Zero);
            material.SetInt("_ZWrite", transparent ? 0 : 1);
            material.renderQueue = transparent ? (int)RenderQueue.Transparent : -1;
        }
    }

    private static bool MaterialsMatch(Material[] current, Material[] updated)
    {
        if (current.Length != updated.Length)
        {
            return false;
        }

        for (int i = 0; i < current.Length; i++)
        {
            if (current[i] != updated[i])
            {
                return false;
            }
        }

        return true;
    }

    private static void EnsureFolderExists(string assetPath)
    {
        string[] parts = assetPath.Split('/');
        string current = parts[0];

        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, parts[i]);
            }

            current = next;
        }
    }
}
