using System;
using UnityEngine;

public struct MetallicMaterialInfo
{
    public Color _Color;
    public Texture2D _MainTex;
    public float _Metallic;
    public float _Glossiness;
    public Texture2D _MetallicGlossMap;
    public float _GlossMapScale;
    public float _SmoothnessTextureChannel;
}

public struct SpecularMaterialInfo
{
    public Color _Color;
    public Texture2D _MainTex;
    public Color _SpecColor;
    public float _Glossiness;
    public Texture2D _SpecGlossMap;
    public float _GlossMapScale;
    public float _SmoothnessTextureChannel;
}

public struct CommonMaterialInfo
{
    public float _Mode;
    public float _Cutoff;
    public Texture2D _BumpMap;
    public float _BumpScale;
    public Texture2D _OcclusionMap;
    public float _OcclusionStrength;
    public Color _EmissionColor;
    public Texture2D _EmissionMap;
}

public static class PbrMaterialExtensions
{
    private static class ShaderName
    {
        public const string Standard = "Standard";
        public const string StandardSpecularSetup = "Standard (Specular setup)";
    }

    public static T GetInfo<T>(this Material material)
    {
        object info = default(T);

        foreach (var field in typeof(T).GetFields())
        {
            object value;

            if (field.FieldType == typeof(Color))
            {
                value = material.GetColor(field.Name);
            }
            else if (field.FieldType == typeof(Texture2D))
            {
                value = (Texture2D)material.GetTexture(field.Name);
            }
            else if (field.FieldType == typeof(float))
            {
                value = material.GetFloat(field.Name);
            }
            else
            {
                throw new NotSupportedException();
            }

            field.SetValue(info, value);
        }

        return (T)info;
    }

    public static void SetInfo<T>(this Material material, T info)
    {
        foreach (var field in typeof(T).GetFields())
        {
            var value = field.GetValue(info);

            if (field.FieldType == typeof(Color))
            {
                material.SetColor(field.Name, (Color)value);
            }
            else if (field.FieldType == typeof(Texture2D))
            {
                material.SetTexture(field.Name, (Texture2D)value);
            }
            else if (field.FieldType == typeof(float))
            {
                material.SetFloat(field.Name, (float)value);
            }
            else
            {
                throw new NotSupportedException();
            }
        }
    }

    public static Material ToMetallic(this Material material)
    {
        MetallicMaterialInfo metallicInfo;

        var shaderName = material.shader.name;
        switch (shaderName)
        {
            case ShaderName.Standard:
                metallicInfo = material.GetInfo<MetallicMaterialInfo>();
                break;

            case ShaderName.StandardSpecularSetup:
                metallicInfo = ConvertToMetallic(material.GetInfo<SpecularMaterialInfo>());
                break;

            default:
                throw new NotSupportedException("Shader '" + shaderName + "' is not supported");
        }

        material = new Material(Shader.Find(ShaderName.Standard));
        material.SetInfo(metallicInfo);
        return material;
    }

    public static Material ToSpecular(this Material material)
    {
        SpecularMaterialInfo specularInfo;

        var shaderName = material.shader.name;
        switch (shaderName)
        {
            case ShaderName.Standard:
                specularInfo = ConvertToSpecular(material.GetInfo<MetallicMaterialInfo>());
                break;

            case ShaderName.StandardSpecularSetup:
                specularInfo = material.GetInfo<SpecularMaterialInfo>();
                break;

            default:
                throw new NotSupportedException("Shader '" + shaderName + "' is not supported");
        }

        material = new Material(Shader.Find(ShaderName.StandardSpecularSetup));
        material.SetInfo(specularInfo);
        return material;
    }

    private static SpecularMaterialInfo ConvertToSpecular(MetallicMaterialInfo info)
    {
        if (info._MainTex == null || info._MetallicGlossMap == null)
        {
            throw new NotImplementedException();
        }

        if (info._MainTex.width != info._MetallicGlossMap.width ||
            info._MainTex.height != info._MetallicGlossMap.height)
        {
            throw new NotImplementedException();
        }

        var baseColorPixels = info._MainTex.GetPixels();
        var metallicGlossPixels = info._MetallicGlossMap.GetPixels();

        var diffusePixels = new Color[baseColorPixels.Length];
        var specularGlossinessPixels = new Color[baseColorPixels.Length];

        for (int i = 0; i < baseColorPixels.Length; i++)
        {
            var metallicRoughness = new MetallicRoughness
            {
                BaseColor = baseColorPixels[i].linear,
                Metallic = Mathf.GammaToLinearSpace(metallicGlossPixels[i].r),
                Roughness = 1.0f - metallicGlossPixels[i].a,
            };

            var specularGlossiness = PbrUtilities.Convert(metallicRoughness);

            diffusePixels[i] = specularGlossiness.Diffuse.gamma;
            specularGlossinessPixels[i] = specularGlossiness.Specular.gamma;
            specularGlossinessPixels[i].a = specularGlossiness.Glossiness;
        }

        var diffuseTexture = new Texture2D(info._MainTex.width, info._MainTex.height, TextureFormat.ARGB32, false);
        diffuseTexture.SetPixels(diffusePixels);
        diffuseTexture.Apply();

        var specularGlossinessTexture = new Texture2D(info._MainTex.width, info._MainTex.height, TextureFormat.ARGB32, false);
        specularGlossinessTexture.SetPixels(specularGlossinessPixels);
        specularGlossinessTexture.Apply();

        return new SpecularMaterialInfo
        {
            _Color = Color.white,
            _MainTex = diffuseTexture,
            _SpecColor = Color.white,
            _Glossiness = 1.0f,
            _SpecGlossMap = specularGlossinessTexture,
            _GlossMapScale = 1.0f,
            _SmoothnessTextureChannel = 0.0f,
        };
    }

    private static MetallicMaterialInfo ConvertToMetallic(SpecularMaterialInfo info)
    {
        if (info._MainTex == null || info._SpecGlossMap == null)
        {
            throw new NotImplementedException();
        }

        if (info._MainTex.width != info._SpecGlossMap.width ||
            info._MainTex.height != info._SpecGlossMap.height)
        {
            throw new NotImplementedException();
        }

        var diffusePixels = info._MainTex.GetPixels();
        var specGlossPixels = info._SpecGlossMap.GetPixels();

        var baseColorPixels = new Color[diffusePixels.Length];
        var metallicGlossPixels = new Color[diffusePixels.Length];

        for (int i = 0; i < diffusePixels.Length; i++)
        {
            var specularGlossiness = new SpecularGlossiness
            {
                Diffuse = diffusePixels[i].linear,
                Specular = specGlossPixels[i].linear,
                Glossiness = specGlossPixels[i].a
            };

            var metallicRoughness = PbrUtilities.Convert(specularGlossiness);

            baseColorPixels[i] = metallicRoughness.BaseColor.gamma;

            var metallic = Mathf.LinearToGammaSpace(metallicRoughness.Metallic);
            var glossiness = 1.0f - metallicRoughness.Roughness;
            metallicGlossPixels[i] = new Color(metallic, metallic, metallic, glossiness);
        }

        var baseColorTexture = new Texture2D(info._MainTex.width, info._MainTex.height, TextureFormat.ARGB32, false);
        baseColorTexture.SetPixels(baseColorPixels);
        baseColorTexture.Apply();

        var metallicGlossTexture = new Texture2D(info._MainTex.width, info._MainTex.height, TextureFormat.ARGB32, false);
        metallicGlossTexture.SetPixels(metallicGlossPixels);
        metallicGlossTexture.Apply();

        return new MetallicMaterialInfo
        {
            _Color = Color.white,
            _MainTex = baseColorTexture,
            _Metallic = 1.0f,
            _Glossiness = 0.0f,
            _MetallicGlossMap = metallicGlossTexture,
            _GlossMapScale = 1.0f,
            _SmoothnessTextureChannel = 0.0f,
        };
    }
}
