using UnityEngine;

// All values are in linear space
public struct MetallicRoughness
{
    public Color BaseColor;
    public float Metallic;
    public float Roughness;
}

// All values are in linear space
public struct SpecularGlossiness
{
    public Color Diffuse;
    public Color Specular;
    public float Glossiness;
}

public static class PbrUtilities
{
    private readonly static Color DielectricSpecular = new Color(0.04f, 0.04f, 0.04f);
    private const float Epsilon = 1e-6f;

    public static SpecularGlossiness Convert(MetallicRoughness metallicRoughness)
    {
        var baseColor = metallicRoughness.BaseColor;
        var metallic = metallicRoughness.Metallic;
        var roughness = metallicRoughness.Roughness;

        var specular = Color.Lerp(DielectricSpecular, baseColor, metallic);

        var oneMinusSpecularStrength = 1.0f - specular.maxColorComponent;
        var diffuse = (oneMinusSpecularStrength < Epsilon) ? Color.black : baseColor * (1.0f - DielectricSpecular.r) * (1.0f - metallic) / oneMinusSpecularStrength;
        diffuse.a = baseColor.a;

        return new SpecularGlossiness
        {
            Diffuse = diffuse,
            Specular = specular,
            Glossiness = 1.0f - roughness,
        };
    }

    public static MetallicRoughness Convert(SpecularGlossiness specularGlossiness)
    {
        var diffuse = specularGlossiness.Diffuse;
        var specular = specularGlossiness.Specular;
        var glossiness = specularGlossiness.Glossiness;

        var oneMinusSpecularStrength = 1.0f - specular.maxColorComponent;
        var metallic = SolveMetallic(DielectricSpecular.r, GetPerceivedBrightness(diffuse), GetPerceivedBrightness(specular), oneMinusSpecularStrength);

        var baseColorFromDiffuse = diffuse * oneMinusSpecularStrength / ((1.0f - DielectricSpecular.r) * Mathf.Max(1.0f - metallic, Epsilon));
        var baseColorFromSpecular = (specular - DielectricSpecular * (1.0f - metallic)) / Mathf.Max(metallic, Epsilon);
        var baseColor = Color.Lerp(baseColorFromDiffuse, baseColorFromSpecular, metallic * metallic);
        baseColor.a = diffuse.a;

        return new MetallicRoughness
        {
            BaseColor = baseColor,
            Metallic = metallic,
            Roughness = 1.0f - glossiness,
        };
    }

    private static float GetPerceivedBrightness(Color linearColor)
    {
        var r = linearColor.r;
        var b = linearColor.b;
        var g = linearColor.g;
        return Mathf.Sqrt(0.299f * r * r + 0.587f * g * g + 0.114f * b * b);
    }

    private static float SolveMetallic(float dielectricSpecular, float diffuse, float specular, float oneMinusSpecularStrength)
    {
        if (specular < dielectricSpecular)
        {
            return 0.0f;
        }

        var a = dielectricSpecular;
        var b = diffuse * oneMinusSpecularStrength / (1 - dielectricSpecular) + specular - 2.0f * dielectricSpecular;
        var c = dielectricSpecular - specular;
        var D = b * b - 4.0f * a * c;
        return Mathf.Clamp01((-b + Mathf.Sqrt(D)) / (2.0f * a));
    }
}
