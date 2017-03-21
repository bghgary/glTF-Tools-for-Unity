using System;
using System.Collections.Generic;
using UnityEngine;

public class PbrMaterialManager : IDisposable
{
    private static class ShaderName
    {
        public const string Standard = "Standard";
        public const string StandardSpecularSetup = "Standard (Specular setup)";
    }

    private Dictionary<SpecularMaterialInfo, MetallicMaterialInfo> specularToMetallicCache = new Dictionary<SpecularMaterialInfo, MetallicMaterialInfo>();
    private Dictionary<MetallicMaterialInfo, SpecularMaterialInfo> metallicToSpecularCache = new Dictionary<MetallicMaterialInfo, SpecularMaterialInfo>();
    private List<UnityEngine.Object> objects = new List<UnityEngine.Object>();

    public void Dispose()
    {
        this.objects.ForEach(material => UnityEngine.Object.DestroyImmediate(material));
    }

    public Material ConvertToMetallic(Material material)
    {
        MetallicMaterialInfo metallicInfo;

        var shaderName = material.shader.name;
        switch (shaderName)
        {
            case ShaderName.Standard:
                metallicInfo = material.GetInfo<MetallicMaterialInfo>();
                break;

            case ShaderName.StandardSpecularSetup:
                var specularInfo = material.GetInfo<SpecularMaterialInfo>();
                if (!this.specularToMetallicCache.TryGetValue(specularInfo, out metallicInfo))
                {
                    metallicInfo = this.ConvertToMetallic(specularInfo);
                    this.specularToMetallicCache.Add(specularInfo, metallicInfo);
                }
                break;

            default:
                throw new NotSupportedException("Shader '" + shaderName + "' is not supported");
        }

        material = new Material(Shader.Find(ShaderName.Standard));
        material.SetInfo(metallicInfo);
        this.objects.Add(material);
        return material;
    }

    public Material ConvertToSpecular(Material material)
    {
        SpecularMaterialInfo specularInfo;

        var shaderName = material.shader.name;
        switch (shaderName)
        {
            case ShaderName.Standard:
                var metallicInfo = material.GetInfo<MetallicMaterialInfo>();
                if (!this.metallicToSpecularCache.TryGetValue(metallicInfo, out specularInfo))
                {
                    specularInfo = this.ConvertToSpecular(metallicInfo);
                    this.metallicToSpecularCache.Add(metallicInfo, specularInfo);
                }
                break;

            case ShaderName.StandardSpecularSetup:
                specularInfo = material.GetInfo<SpecularMaterialInfo>();
                break;

            default:
                throw new NotSupportedException("Shader '" + shaderName + "' is not supported");
        }

        material = new Material(Shader.Find(ShaderName.StandardSpecularSetup));
        material.SetInfo(specularInfo);
        this.objects.Add(material);
        return material;
    }

    private SpecularMaterialInfo ConvertToSpecular(MetallicMaterialInfo info)
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

        var diffuseTextureFormat = TextureFormat.RGB24;
        for (int i = 0; i < baseColorPixels.Length; i++)
        {
            var metallicRoughness = new MetallicRoughness
            {
                BaseColor = baseColorPixels[i].linear,
                Metallic = Mathf.GammaToLinearSpace(metallicGlossPixels[i].r),
                Roughness = 1.0f - metallicGlossPixels[i].a,
            };

            var specularGlossiness = PbrUtilities.Convert(metallicRoughness);

            if (specularGlossiness.Diffuse.a != 1.0f)
            {
                diffuseTextureFormat = TextureFormat.ARGB32;
            }

            diffusePixels[i] = specularGlossiness.Diffuse.gamma;

            specularGlossinessPixels[i] = specularGlossiness.Specular.gamma;
            specularGlossinessPixels[i].a = specularGlossiness.Glossiness;
        }

        var diffuseTexture = new Texture2D(info._MainTex.width, info._MainTex.height, diffuseTextureFormat, false);
        diffuseTexture.SetPixels(diffusePixels);
        diffuseTexture.Apply();
        this.objects.Add(diffuseTexture);

        var specularGlossinessTexture = new Texture2D(info._MainTex.width, info._MainTex.height, TextureFormat.ARGB32, false);
        specularGlossinessTexture.SetPixels(specularGlossinessPixels);
        specularGlossinessTexture.Apply();
        this.objects.Add(specularGlossinessTexture);

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

    private MetallicMaterialInfo ConvertToMetallic(SpecularMaterialInfo info)
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

        var baseColorTextureFormat = TextureFormat.RGB24;
        for (int i = 0; i < diffusePixels.Length; i++)
        {
            var specularGlossiness = new SpecularGlossiness
            {
                Diffuse = diffusePixels[i].linear,
                Specular = specGlossPixels[i].linear,
                Glossiness = specGlossPixels[i].a
            };

            var metallicRoughness = PbrUtilities.Convert(specularGlossiness);

            if (metallicRoughness.BaseColor.a != 1.0f)
            {
                baseColorTextureFormat = TextureFormat.ARGB32;
            }

            baseColorPixels[i] = metallicRoughness.BaseColor.gamma;

            var metallic = Mathf.LinearToGammaSpace(metallicRoughness.Metallic);
            var glossiness = 1.0f - metallicRoughness.Roughness;
            metallicGlossPixels[i] = new Color(metallic, metallic, metallic, glossiness);
        }

        var baseColorTexture = new Texture2D(info._MainTex.width, info._MainTex.height, baseColorTextureFormat, false);
        baseColorTexture.SetPixels(baseColorPixels);
        baseColorTexture.Apply();
        this.objects.Add(baseColorTexture);

        var metallicGlossTexture = new Texture2D(info._MainTex.width, info._MainTex.height, TextureFormat.ARGB32, false);
        metallicGlossTexture.SetPixels(metallicGlossPixels);
        metallicGlossTexture.Apply();
        this.objects.Add(metallicGlossTexture);

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
