using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Gltf.Serialization
{
    internal sealed partial class Exporter
    {
        private class KHR_materials_pbrSpecularGlossiness : Extension
        {
            private static class Schema
            {
                [Serializable]
                public class Material
                {
                    public IEnumerable<float> DiffuseFactor;
                    public Gltf.Schema.MaterialTexture DiffuseTexture;
                    public IEnumerable<float> SpecularFactor;
                    public float GlossinessFactor;
                    public Gltf.Schema.MaterialTexture SpecularGlossinessTexture;

                    public bool ShouldSerializeDiffuseFactor() { return this.DiffuseFactor != null && !this.DiffuseFactor.SequenceEqual(new[] { 1.0f, 1.0f, 1.0f, 1.0f }); }
                    public bool ShouldSerializeDiffuseTexture() { return this.DiffuseTexture != null; }
                    public bool ShouldSerializeSpecularFactor() { return this.SpecularFactor != null && !this.SpecularFactor.SequenceEqual(new[] { 1.0f, 1.0f, 1.0f }); }
                    public bool ShouldSerializeGlossinessFactor() { return this.GlossinessFactor != 1.0f; }
                    public bool ShouldSerializeSpecularGlossinessTexture() { return this.SpecularGlossinessTexture != null; }
                }
            }

            private readonly Dictionary<OcclusionInfo, Texture2D> occlusionInfoToTextureCache = new Dictionary<OcclusionInfo, Texture2D>();

            public KHR_materials_pbrSpecularGlossiness(Exporter exporter)
                : base(exporter)
            {
                this.exporter.extensionsUsed.Add(this.GetType().Name);
            }

            public override bool ExportMaterial(Material unityMaterial, out int index)
            {
                exporter.ExportMaterialCore(unityMaterial, false, out index);
                this.ExportMaterialOcclusion(unityMaterial, index);
                return true;
            }

            private void ExportMaterialOcclusion(Material unityMaterial, int index)
            {
                var info = unityMaterial.GetInfo<OcclusionInfo>();
                var material = this.exporter.materials[index];

                if (info._OcclusionMap != null)
                {
                    Texture2D texture;
                    if (!this.occlusionInfoToTextureCache.TryGetValue(info, out texture))
                    {
                        var pixels = info._OcclusionMap.GetPixels();
                        for (int i = 0; i < pixels.Length; i++)
                        {
                            pixels[i] = pixels[i].linear;
                        }

                        texture = this.exporter.objectTracker.Add(new Texture2D(info._OcclusionMap.width, info._OcclusionMap.height, TextureFormat.RGB24, false));
                        texture.SetPixels(pixels);
                        texture.Apply();

                        material.OcclusionTexture = new Gltf.Schema.MaterialOcclusionTexture
                        {
                            Index = this.exporter.ExportTexture(texture, FormatMaterialTextureName("occlusion", index)),
                            Strength = info._OcclusionStrength,
                        };
                    }
                }
            }

            public override void PostExportMaterial(int index, Material unityMaterial)
            {
                var info = this.exporter.pbrMaterialManager.ConvertToSpecular(unityMaterial).GetInfo<SpecularInfo>();

                if (info._SmoothnessTextureChannel != 0)
                {
                    throw new NotImplementedException();
                }

                var specularGlossiness = new Schema.Material
                {
                    DiffuseFactor = ColorToArray(info._Color.linear, 4),
                };

                if (info._MainTex != null)
                {
                    specularGlossiness.DiffuseTexture = new Gltf.Schema.MaterialTexture
                    {
                        Index = this.exporter.ExportTexture(info._MainTex, FormatMaterialTextureName("diffuse", index)),
                    };
                }

                if (info._SpecGlossMap == null)
                {
                    specularGlossiness.SpecularFactor = ColorToArray(info._SpecColor.linear, 3);
                    specularGlossiness.GlossinessFactor = info._Glossiness;
                }
                else
                {
                    specularGlossiness.GlossinessFactor = info._GlossMapScale;
                    specularGlossiness.SpecularGlossinessTexture = new Gltf.Schema.MaterialTexture
                    {
                        Index = this.exporter.ExportTexture(info._SpecGlossMap, FormatMaterialTextureName("specularGlossiness", index)),
                    };
                }

                var material = this.exporter.materials[index];

                if (material.Extensions == null)
                {
                    material.Extensions = new Dictionary<string, object>();
                }

                material.Extensions.Add(this.GetType().Name, specularGlossiness);
            }
        }
    }
}
