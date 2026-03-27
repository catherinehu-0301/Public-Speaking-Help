using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

public static class OfficeSceneMaterialTools
{
    private const string MaterialFolder = "Assets/Materials/OfficeAuto";

    private enum OfficeMaterialKind
    {
        FallbackNeutral,
        TableSurface,
        ChairUpholstery,
        MetalFrame,
        Carpet,
        WallPaint,
        CeilingWhite,
        Glass,
        ScreenDark,
        DarkWood,
        BlackTrim
    }

    [MenuItem("Tools/Virtual Stage VR/Office/Analyze Selected Office Import")]
    private static void AnalyzeSelectedOfficeImport()
    {
        GameObject root = GetSelectedSceneRoot();
        if (root == null)
        {
            return;
        }

        MeshRenderer[] renderers = root.GetComponentsInChildren<MeshRenderer>(true);
        MeshFilter[] filters = root.GetComponentsInChildren<MeshFilter>(true);

        var materialUsage = new Dictionary<string, int>();
        var lines = new List<string>();
        Bounds combinedBounds = default;
        bool hasBounds = false;
        int totalMaterialSlots = 0;
        int multiSlotRendererCount = 0;
        int noMeshCount = 0;

        foreach (MeshRenderer renderer in renderers.OrderBy(r => GetTransformPath(root.transform, r.transform)))
        {
            MeshFilter filter = renderer.GetComponent<MeshFilter>();
            Mesh mesh = filter != null ? filter.sharedMesh : null;
            int subMeshCount = mesh != null ? mesh.subMeshCount : 0;
            int slotCount = renderer.sharedMaterials != null ? renderer.sharedMaterials.Length : 0;
            totalMaterialSlots += Mathf.Max(1, slotCount);

            if (mesh == null)
            {
                noMeshCount++;
            }

            if (Mathf.Max(subMeshCount, slotCount) > 1)
            {
                multiSlotRendererCount++;
            }

            if (renderer.sharedMaterials != null)
            {
                foreach (Material material in renderer.sharedMaterials)
                {
                    string name = material != null ? material.name : "(null)";
                    if (!materialUsage.ContainsKey(name))
                    {
                        materialUsage[name] = 0;
                    }

                    materialUsage[name]++;
                }
            }

            if (!hasBounds)
            {
                combinedBounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                combinedBounds.Encapsulate(renderer.bounds);
            }

            string path = GetTransformPath(root.transform, renderer.transform);
            string meshName = mesh != null ? mesh.name : "(missing mesh)";
            lines.Add(path + " | mesh=" + meshName + " | slots=" + slotCount + " | subMeshes=" + subMeshCount);
        }

        bool combinedMesh = renderers.Length == 1;
        bool multipleChildObjects = renderers.Length > 1;
        bool hasMultipleSubmeshes = multiSlotRendererCount > 0;

        string importShape;
        if (renderers.Length == 0)
        {
            importShape = "No MeshRenderer objects found under the selected root.";
        }
        else if (combinedMesh && !hasMultipleSubmeshes)
        {
            importShape = "One combined mesh with a single material slot.";
        }
        else if (combinedMesh)
        {
            importShape = "One combined mesh with multiple submeshes/material slots.";
        }
        else if (multipleChildObjects && !hasMultipleSubmeshes)
        {
            importShape = "Multiple child objects, mostly one material slot each.";
        }
        else
        {
            importShape = "Multiple child objects and at least some multi-slot renderers.";
        }

        StringBuilder report = new StringBuilder(4096);
        report.AppendLine("Office import analysis for \"" + root.name + "\"");
        report.AppendLine("Import shape: " + importShape);
        report.AppendLine("Mesh renderers: " + renderers.Length);
        report.AppendLine("Mesh filters: " + filters.Length);
        report.AppendLine("Total material slots: " + totalMaterialSlots);
        report.AppendLine("Renderers with multiple slots/submeshes: " + multiSlotRendererCount);
        report.AppendLine("Renderers missing meshes: " + noMeshCount);
        report.AppendLine("Total transforms under root: " + root.GetComponentsInChildren<Transform>(true).Length);

        if (hasBounds)
        {
            Vector3 size = combinedBounds.size;
            report.AppendLine("Approx world-space bounds (meters if your scene scale is correct): " +
                              size.x.ToString("0.##") + " x " +
                              size.y.ToString("0.##") + " x " +
                              size.z.ToString("0.##"));
        }

        report.AppendLine("Existing material names:");
        foreach (KeyValuePair<string, int> entry in materialUsage.OrderByDescending(x => x.Value).ThenBy(x => x.Key).Take(20))
        {
            report.AppendLine("  " + entry.Key + " x" + entry.Value);
        }

        report.AppendLine("Renderer breakdown:");
        foreach (string line in lines.Take(120))
        {
            report.AppendLine("  " + line);
        }

        if (lines.Count > 120)
        {
            report.AppendLine("  ... " + (lines.Count - 120) + " more renderers omitted from the log.");
        }

        Debug.Log(report.ToString(), root);
        EditorGUIUtility.PingObject(root);
    }

    [MenuItem("Tools/Virtual Stage VR/Office/Auto Assign Office Materials")]
    private static void AutoAssignOfficeMaterials()
    {
        GameObject root = GetSelectedSceneRoot();
        if (root == null)
        {
            return;
        }

        EnsureFolderExists(MaterialFolder);
        Dictionary<OfficeMaterialKind, Material> library = BuildMaterialLibrary();
        MeshRenderer[] renderers = root.GetComponentsInChildren<MeshRenderer>(true);

        int updatedRendererCount = 0;
        int fallbackAssignments = 0;
        var fallbackPaths = new List<string>();

        foreach (MeshRenderer renderer in renderers)
        {
            MeshFilter filter = renderer.GetComponent<MeshFilter>();
            Mesh mesh = filter != null ? filter.sharedMesh : null;
            Material[] currentMaterials = renderer.sharedMaterials ?? new Material[0];
            int slotCount = Mathf.Max(1, currentMaterials.Length);
            var reassignedMaterials = new Material[slotCount];

            for (int slotIndex = 0; slotIndex < slotCount; slotIndex++)
            {
                Material currentMaterial = slotIndex < currentMaterials.Length ? currentMaterials[slotIndex] : null;
                OfficeMaterialKind kind = GuessMaterialKind(root.transform, renderer, mesh, currentMaterial, slotIndex);
                reassignedMaterials[slotIndex] = library[kind];

                if (kind == OfficeMaterialKind.FallbackNeutral)
                {
                    fallbackAssignments++;
                    fallbackPaths.Add(GetTransformPath(root.transform, renderer.transform));
                }
            }

            if (!MaterialsMatch(currentMaterials, reassignedMaterials))
            {
                Undo.RecordObject(renderer, "Assign Office Materials");
                renderer.sharedMaterials = reassignedMaterials;
                EditorUtility.SetDirty(renderer);
                updatedRendererCount++;
            }
        }

        if (root.scene.IsValid())
        {
            EditorSceneManager.MarkSceneDirty(root.scene);
        }

        AssetDatabase.SaveAssets();
        EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<Object>(MaterialFolder));

        StringBuilder summary = new StringBuilder(2048);
        summary.AppendLine("Assigned office materials under \"" + root.name + "\".");
        summary.AppendLine("Renderers updated: " + updatedRendererCount + " / " + renderers.Length);
        summary.AppendLine("Fallback material assignments: " + fallbackAssignments);
        summary.AppendLine("Material assets folder: " + MaterialFolder);

        if (fallbackPaths.Count > 0)
        {
            summary.AppendLine("Review these fallback objects and remap them manually if needed:");
            foreach (string path in fallbackPaths.Distinct().OrderBy(x => x).Take(40))
            {
                summary.AppendLine("  " + path);
            }

            if (fallbackPaths.Count > 40)
            {
                summary.AppendLine("  ... " + (fallbackPaths.Count - 40) + " more fallback assignments omitted.");
            }
        }

        Debug.Log(summary.ToString(), root);
    }

    [MenuItem("Tools/Virtual Stage VR/Office/Analyze Selected Office Import", true)]
    [MenuItem("Tools/Virtual Stage VR/Office/Auto Assign Office Materials", true)]
    private static bool ValidateSelection()
    {
        return Selection.activeGameObject != null;
    }

    private static GameObject GetSelectedSceneRoot()
    {
        GameObject root = Selection.activeGameObject;
        if (root == null)
        {
            Debug.LogWarning("Select the office root object in the Hierarchy first.");
            return null;
        }

        if (EditorUtility.IsPersistent(root))
        {
            Debug.LogWarning("Select the placed office instance in the Hierarchy, not the imported OBJ asset in the Project window.");
            return null;
        }

        return root;
    }

    private static Dictionary<OfficeMaterialKind, Material> BuildMaterialLibrary()
    {
        var library = new Dictionary<OfficeMaterialKind, Material>();

        library[OfficeMaterialKind.FallbackNeutral] = GetOrCreateMaterial(
            "M_Office_FallbackNeutral",
            new MaterialRecipe(new Color(0.75f, 0.76f, 0.78f), 0f, 0.2f, SurfaceKind.Opaque));

        library[OfficeMaterialKind.TableSurface] = GetOrCreateMaterial(
            "M_Office_TableSurface",
            new MaterialRecipe(new Color(0.91f, 0.91f, 0.90f), 0f, 0.38f, SurfaceKind.Opaque));

        library[OfficeMaterialKind.ChairUpholstery] = GetOrCreateMaterial(
            "M_Office_ChairUpholstery",
            new MaterialRecipe(new Color(0.67f, 0.67f, 0.68f), 0f, 0.28f, SurfaceKind.Opaque));

        library[OfficeMaterialKind.MetalFrame] = GetOrCreateMaterial(
            "M_Office_MetalFrame",
            new MaterialRecipe(new Color(0.78f, 0.80f, 0.82f), 1f, 0.78f, SurfaceKind.Opaque));

        library[OfficeMaterialKind.Carpet] = GetOrCreateMaterial(
            "M_Office_Carpet",
            new MaterialRecipe(new Color(0.73f, 0.71f, 0.67f), 0f, 0.06f, SurfaceKind.Opaque));

        library[OfficeMaterialKind.WallPaint] = GetOrCreateMaterial(
            "M_Office_WallPaint",
            new MaterialRecipe(new Color(0.94f, 0.93f, 0.90f), 0f, 0.12f, SurfaceKind.Opaque));

        library[OfficeMaterialKind.CeilingWhite] = GetOrCreateMaterial(
            "M_Office_CeilingWhite",
            new MaterialRecipe(new Color(0.97f, 0.97f, 0.96f), 0f, 0.08f, SurfaceKind.Opaque));

        library[OfficeMaterialKind.Glass] = GetOrCreateMaterial(
            "M_Office_Glass",
            new MaterialRecipe(new Color(0.84f, 0.91f, 0.96f, 0.22f), 0f, 0.96f, SurfaceKind.Transparent));

        library[OfficeMaterialKind.ScreenDark] = GetOrCreateMaterial(
            "M_Office_ScreenDark",
            new MaterialRecipe(new Color(0.07f, 0.07f, 0.08f), 0f, 0.84f, SurfaceKind.Opaque));

        library[OfficeMaterialKind.DarkWood] = GetOrCreateMaterial(
            "M_Office_DarkWood",
            new MaterialRecipe(new Color(0.24f, 0.17f, 0.12f), 0f, 0.42f, SurfaceKind.Opaque));

        library[OfficeMaterialKind.BlackTrim] = GetOrCreateMaterial(
            "M_Office_BlackTrim",
            new MaterialRecipe(new Color(0.10f, 0.10f, 0.11f), 0f, 0.45f, SurfaceKind.Opaque));

        return library;
    }

    private static Material GetOrCreateMaterial(string materialName, MaterialRecipe recipe)
    {
        string path = MaterialFolder + "/" + materialName + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null)
        {
            return material;
        }

        Shader shader = ResolvePreferredShader();
        material = new Material(shader)
        {
            name = materialName,
            enableInstancing = true
        };

        ApplyRecipe(material, recipe);
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    private static Shader ResolvePreferredShader()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader != null)
        {
            return shader;
        }

        shader = Shader.Find("Standard");
        if (shader != null)
        {
            return shader;
        }

        return Shader.Find("Diffuse");
    }

    private static void ApplyRecipe(Material material, MaterialRecipe recipe)
    {
        bool isUrp = material.shader != null && material.shader.name.Contains("Universal Render Pipeline");

        if (isUrp)
        {
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", recipe.BaseColor);
            }

            if (material.HasProperty("_Metallic"))
            {
                material.SetFloat("_Metallic", recipe.Metallic);
            }

            if (material.HasProperty("_Smoothness"))
            {
                material.SetFloat("_Smoothness", recipe.Smoothness);
            }

            ConfigureUrpSurface(material, recipe.Surface);
        }
        else
        {
            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", recipe.BaseColor);
            }

            if (material.HasProperty("_Metallic"))
            {
                material.SetFloat("_Metallic", recipe.Metallic);
            }

            if (material.HasProperty("_Glossiness"))
            {
                material.SetFloat("_Glossiness", recipe.Smoothness);
            }

            ConfigureStandardSurface(material, recipe.Surface);
        }
    }

    private static void ConfigureUrpSurface(Material material, SurfaceKind surfaceKind)
    {
        if (surfaceKind == SurfaceKind.Transparent)
        {
            material.SetOverrideTag("RenderType", "Transparent");
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.DisableKeyword("_ALPHATEST_ON");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Transparent;
        }
        else
        {
            material.SetOverrideTag("RenderType", "Opaque");
            material.SetFloat("_Surface", 0f);
            material.SetFloat("_SrcBlend", (float)BlendMode.One);
            material.SetFloat("_DstBlend", (float)BlendMode.Zero);
            material.SetFloat("_ZWrite", 1f);
            material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = -1;
        }
    }

    private static void ConfigureStandardSurface(Material material, SurfaceKind surfaceKind)
    {
        if (surfaceKind == SurfaceKind.Transparent)
        {
            material.SetFloat("_Mode", 3f);
            material.SetOverrideTag("RenderType", "Transparent");
            material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            material.SetInt("_ZWrite", 0);
            material.DisableKeyword("_ALPHATEST_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.EnableKeyword("_ALPHABLEND_ON");
            material.renderQueue = (int)RenderQueue.Transparent;
        }
        else
        {
            material.SetFloat("_Mode", 0f);
            material.SetOverrideTag("RenderType", "Opaque");
            material.SetInt("_SrcBlend", (int)BlendMode.One);
            material.SetInt("_DstBlend", (int)BlendMode.Zero);
            material.SetInt("_ZWrite", 1);
            material.DisableKeyword("_ALPHATEST_ON");
            material.DisableKeyword("_ALPHABLEND_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.renderQueue = -1;
        }
    }

    private static OfficeMaterialKind GuessMaterialKind(Transform root, MeshRenderer renderer, Mesh mesh, Material slotMaterial, int slotIndex)
    {
        string path = GetTransformPath(root, renderer.transform).ToLowerInvariant();
        string materialName = slotMaterial != null ? slotMaterial.name.ToLowerInvariant() : string.Empty;
        string meshName = mesh != null ? mesh.name.ToLowerInvariant() : string.Empty;
        string slotLabel = "slot" + slotIndex;
        string combinedText = path + " " + materialName + " " + meshName + " " + slotLabel;

        if (ContainsAll(combinedText, "window", "frame") ||
            ContainsAny(combinedText, "mullion", "window_frame", "windowframe", "bezel", "ceiling_strip", "recess", "track", "channel"))
        {
            return OfficeMaterialKind.BlackTrim;
        }

        if (ContainsAny(combinedText, "screen", "tv", "monitor", "display", "projector"))
        {
            return OfficeMaterialKind.ScreenDark;
        }

        if (ContainsAny(combinedText, "glass", "pane", "glazing") || ContainsWord(combinedText, "window"))
        {
            return OfficeMaterialKind.Glass;
        }

        if (ContainsWord(combinedText, "chair") && ContainsAny(combinedText, "frame", "leg", "base", "arm", "wheel", "caster", "chrome", "metal", "support"))
        {
            return OfficeMaterialKind.MetalFrame;
        }

        if ((ContainsWord(combinedText, "chair") || ContainsWord(combinedText, "seat")) &&
            ContainsAny(combinedText, "seat", "back", "cushion", "pad", "upholstery", "fabric", "leather"))
        {
            return OfficeMaterialKind.ChairUpholstery;
        }

        if (ContainsAny(combinedText, "carpet", "rug", "mat"))
        {
            return OfficeMaterialKind.Carpet;
        }

        if (ContainsWord(combinedText, "floor") || ContainsWord(combinedText, "ground"))
        {
            if (ContainsAny(combinedText, "wood", "hardwood", "parquet", "plank", "timber", "veneer"))
            {
                return OfficeMaterialKind.DarkWood;
            }

            return OfficeMaterialKind.Carpet;
        }

        if (ContainsAny(combinedText, "ceiling", "roof", "soffit"))
        {
            return OfficeMaterialKind.CeilingWhite;
        }

        if (ContainsAny(combinedText, "wall", "column", "pillar", "partition"))
        {
            if (ContainsAny(combinedText, "wood", "panel", "accent", "veneer", "laminate"))
            {
                return OfficeMaterialKind.DarkWood;
            }

            return OfficeMaterialKind.WallPaint;
        }

        if (ContainsAny(combinedText, "table", "desk", "conference", "meeting", "counter", "worktop") &&
            !ContainsAny(combinedText, "leg", "frame", "base", "support"))
        {
            return OfficeMaterialKind.TableSurface;
        }

        if (ContainsAny(combinedText, "cabinet", "shelf", "panel", "veneer", "laminate", "wood", "timber", "parquet", "baseboard", "skirting"))
        {
            return OfficeMaterialKind.DarkWood;
        }

        if (ContainsAny(combinedText, "metal", "chrome", "steel", "aluminum", "aluminium", "leg", "base", "support", "pole", "handle"))
        {
            return OfficeMaterialKind.MetalFrame;
        }

        if (ContainsAny(combinedText, "trim", "border", "channel", "track", "black"))
        {
            return OfficeMaterialKind.BlackTrim;
        }

        if (ContainsWord(combinedText, "chair") || ContainsWord(combinedText, "seat"))
        {
            return OfficeMaterialKind.ChairUpholstery;
        }

        if (ContainsAny(combinedText, "light", "lamp", "spot", "downlight", "fixture"))
        {
            return OfficeMaterialKind.CeilingWhite;
        }

        return OfficeMaterialKind.FallbackNeutral;
    }

    private static bool MaterialsMatch(Material[] current, Material[] reassigned)
    {
        if (current == null || current.Length != reassigned.Length)
        {
            return false;
        }

        for (int i = 0; i < reassigned.Length; i++)
        {
            if (current[i] != reassigned[i])
            {
                return false;
            }
        }

        return true;
    }

    private static bool ContainsAny(string text, params string[] keywords)
    {
        foreach (string keyword in keywords)
        {
            if (text.Contains(keyword))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsAll(string text, params string[] keywords)
    {
        foreach (string keyword in keywords)
        {
            if (!text.Contains(keyword))
            {
                return false;
            }
        }

        return true;
    }

    private static bool ContainsWord(string text, string word)
    {
        return text.Contains(word);
    }

    private static string GetTransformPath(Transform root, Transform target)
    {
        if (target == root)
        {
            return root.name;
        }

        var parts = new Stack<string>();
        Transform current = target;
        while (current != null && current != root)
        {
            parts.Push(current.name);
            current = current.parent;
        }

        parts.Push(root.name);
        return string.Join("/", parts);
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

    private enum SurfaceKind
    {
        Opaque,
        Transparent
    }

    private readonly struct MaterialRecipe
    {
        public readonly Color BaseColor;
        public readonly float Metallic;
        public readonly float Smoothness;
        public readonly SurfaceKind Surface;

        public MaterialRecipe(Color baseColor, float metallic, float smoothness, SurfaceKind surfaceKind)
        {
            BaseColor = baseColor;
            Metallic = metallic;
            Smoothness = smoothness;
            Surface = surfaceKind;
        }
    }
}
