using System.Collections.Generic;
using UnityEngine;

/// <summary>灰盒材质共用，避免每颗球及每次重开克隆原生 Material。</summary>
public static class ViewMats
{
    private static readonly Dictionary<Color, Material> s_materials = new Dictionary<Color, Material>();

    public static Material GetMat(Color color)
    {
        if (s_materials.TryGetValue(color, out var material) && material != null)
            return material;
        var shader = Shader.Find("ChainSix/Graybox");
        if (shader == null || !shader.isSupported)
            shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null || !shader.isSupported)
            shader = Shader.Find("Sprites/Default");
        material = new Material(shader);
        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color"))
            material.SetColor("_Color", color);
        s_materials[color] = material;
        return material;
    }
}
