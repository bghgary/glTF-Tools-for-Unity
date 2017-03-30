using System;
using UnityEngine;

public struct MetallicInfo
{
    public Color _Color;
    public Texture2D _MainTex;
    public float _Metallic;
    public float _Glossiness;
    public Texture2D _MetallicGlossMap;
    public float _GlossMapScale;
    public float _SmoothnessTextureChannel;
}

public struct SpecularInfo
{
    public Color _Color;
    public Texture2D _MainTex;
    public Color _SpecColor;
    public float _Glossiness;
    public Texture2D _SpecGlossMap;
    public float _GlossMapScale;
    public float _SmoothnessTextureChannel;
}

public struct AlphaInfo
{
    public float _Mode;
    public float _Cutoff;
}

public struct NormalInfo
{
    public Texture2D _BumpMap;
    public float _BumpScale;
}

public struct OcclusionInfo
{
    public Texture2D _OcclusionMap;
    public float _OcclusionStrength;
}

public struct EmissiveInfo
{
    public Color _EmissionColor;
    public Texture2D _EmissionMap;
}

public static class PbrMaterialExtensions
{
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
}
