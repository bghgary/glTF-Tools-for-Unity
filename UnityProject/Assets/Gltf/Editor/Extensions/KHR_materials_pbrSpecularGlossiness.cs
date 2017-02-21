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

            public KHR_materials_pbrSpecularGlossiness(Exporter exporter)
                : base(exporter)
            {
                this.exporter.extensionsUsed.Add(this.GetType().Name);
            }

            public override void PostExportMaterial(int index, Material unityMaterial)
            {
                var info = unityMaterial.ToSpecular().GetInfo<SpecularMaterialInfo>();

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
                        Index = this.exporter.ExportTexture(info._MainTex),
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
                        Index = this.exporter.ExportTexture(info._SpecGlossMap),
                    };
                }

                var material = this.exporter.materials[index];

                if (material.Extensions == null)
                {
                    material.Extensions = new Dictionary<string, object>();
                }

                material.Extensions.Add(this.GetType().Name, specularGlossiness);
            }

            //private void ExportCommon(Material unityMaterial, Dictionary<string, object> values)
            //{
            //    var unityRenderingMode = (uint)info._Mode;
            //    var alphaRenderingMode = Schema.AlphaRenderingMode.Opaque;
            //    switch (unityRenderingMode)
            //    {
            //        case 0: // Opaque
            //            alphaRenderingMode = Schema.AlphaRenderingMode.Opaque;
            //            break;
            //        case 1: // Cutout
            //            alphaRenderingMode = Schema.AlphaRenderingMode.Cutout;
            //            break;
            //        case 2: // Fade
            //            throw new NotSupportedException();
            //        case 3: // Transparent
            //            alphaRenderingMode = Schema.AlphaRenderingMode.Transparent;
            //            break;
            //    }
            //    values.Add("alphaRenderingMode", alphaRenderingMode);
            //    if (alphaRenderingMode == Schema.AlphaRenderingMode.Cutout)
            //    {
            //        values.Add("alphaCutoff", info._Cutoff);
            //    }
            //}
        }
    }
}
