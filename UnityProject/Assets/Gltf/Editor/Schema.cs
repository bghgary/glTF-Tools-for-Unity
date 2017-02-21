using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Gltf.Schema
{
    [Serializable]
    public enum AccessorType
    {
        SCALAR,
        VEC2,
        VEC3,
        VEC4,
    }

    [Serializable]
    public enum AccessorComponentType
    {
        BYTE = 5120,
        UNSIGNED_BYTE = 5121,
        SHORT = 5122,
        UNSIGNED_SHORT = 5123,
        FLOAT = 5126,
    }

    [Serializable]
    public enum PrimitiveMode
    {
        POINTS = 0,
        LINE = 1,
        LINE_LOOP = 2,
        TRIANGLES = 4,
        TRIANGLE_STRIP = 5,
        TRIANGLE_FAN = 6,
    }

    [Serializable]
    public enum BufferViewTarget
    {
        ARRAY_BUFFER = 34962,
        ELEMENT_ARRAY_BUFFER = 34963,
    }

    [Serializable]
    public class ChildOfRootProperty
    {
        public string Name;
        public Dictionary<string, object> Extensions;
        public object Extras;

        public bool ShouldSerializeName() { return this.Name != null; }
        public bool ShouldSerializeExtensions() { return this.Extensions != null; }
        public bool ShouldSerializeExtras() { return this.Extras != null; }
    }

    [Serializable]
    public class Accessor : ChildOfRootProperty
    {
        public int BufferView;
        public long ByteOffset;
        public int ByteStride;
        public AccessorComponentType ComponentType;
        public bool Normalized;
        public int Count;

        [JsonConverterAttribute(typeof(StringEnumConverter))]
        public AccessorType Type;

        public IEnumerable<object> Max;
        public IEnumerable<object> Min;

        public bool ShouldSerializeByteStride() { return this.ByteStride != 0; }
        public bool ShouldSerializeNormalized() { return this.Normalized; }
    }

    [Serializable]
    public class Asset : ChildOfRootProperty
    {
        public string Version;
    }

    [Serializable]
    public class Buffer : ChildOfRootProperty
    {
        public string Uri;
        public long ByteLength;

        public bool ShouldSerializeUri() { return this.Uri != null; }
    }

    [Serializable]
    public class BufferView : ChildOfRootProperty
    {
        public int Buffer;
        public long ByteOffset;
        public long ByteLength;
        public BufferViewTarget Target;
    }

    [Serializable]
    public class Image : ChildOfRootProperty
    {
        public string Uri;
        public string MimeType;
        public int? BufferView;

        public bool ShouldSerializeUri() { return this.Uri != null; }
        public bool ShouldSerializeMimeType() { return this.MimeType != null; }
        public bool ShouldSerializeBufferView() { return this.BufferView.HasValue; }
    }

    [Serializable]
    public class MaterialPbrMetallicRoughness
    {
        public IEnumerable<float> BaseColorFactor;
        public MaterialTexture BaseColorTexture;
        public float MetallicFactor;
        public float RoughnessFactor;
        public MaterialTexture MetallicRoughnessTexture;

        public bool ShouldSerializeBaseColorFactor() { return this.BaseColorFactor != null && !this.BaseColorFactor.SequenceEqual(new[] { 1.0f, 1.0f, 1.0f, 1.0f }); }
        public bool ShouldSerializeBaseColorTexture() { return this.BaseColorTexture != null; }
        public bool ShouldSerializeMetallicFactor() { return this.MetallicFactor != 1.0f; }
        public bool ShouldSerializeRoughnessFactor() { return this.RoughnessFactor != 1.0f; }
        public bool ShouldSerializeMetallicRoughnessTexture() { return this.MetallicRoughnessTexture != null; }
    }

    [Serializable]
    public class MaterialTexture
    {
        public int Index;
        public int TexCoord;

        public bool ShouldSerializeTexCoord() { return this.TexCoord != 0; }
    }

    [Serializable]
    public class MaterialNormalTexture : MaterialTexture
    {
        public float Scale;

        public bool ShouldSerializeScale() { return this.Scale != 1.0f; }
    }

    [Serializable]
    public class MaterialOcclusionTexture : MaterialTexture
    {
        public float Strength;

        public bool ShouldSerializeStrength() { return this.Strength != 1.0f; }
    }

    [Serializable]
    public class Material : ChildOfRootProperty
    {
        public MaterialPbrMetallicRoughness PbrMetallicRoughness;
        public MaterialNormalTexture NormalTexture;
        public MaterialOcclusionTexture OcclusionTexture;
        public IEnumerable<float> EmissiveFactor;
        public MaterialTexture EmissiveTexture;

        public bool ShouldSerializePbrMetallicRoughness() { return this.PbrMetallicRoughness != null; }
        public bool ShouldSerializeNormalTexture() { return this.NormalTexture != null; }
        public bool ShouldSerializeOcclusionTexture() { return this.OcclusionTexture != null; }
        public bool ShouldSerializeEmissiveFactor() { return this.EmissiveFactor != null && !this.EmissiveFactor.SequenceEqual(new[] { 0.0f, 0.0f, 0.0f }); }
        public bool ShouldSerializeEmissiveTexture() { return this.EmissiveTexture != null; }
    }

    [Serializable]
    public class MeshPrimitive : ChildOfRootProperty
    {
        public IEnumerable<KeyValuePair<string, int>> Attributes;
        public int Indices;
        public int Material;
        public PrimitiveMode Mode;
    }

    [Serializable]
    public class Mesh : ChildOfRootProperty
    {
        public IEnumerable<MeshPrimitive> Primitives;
    }

    [Serializable]
    public class Node : ChildOfRootProperty
    {
        public IEnumerable<int> Children;
        public int? Mesh;
        public IEnumerable<float> Rotation;
        public IEnumerable<float> Scale;
        public IEnumerable<float> Translation;

        public bool ShouldSerializeChildren() { return this.Children != null; }
        public bool ShouldSerializeMesh() { return this.Mesh.HasValue; }
        public bool ShouldSerializeRotation() { return this.Rotation != null && !this.Rotation.SequenceEqual(new[] { 0.0f, 0.0f, 0.0f, 1.0f }); }
        public bool ShouldSerializeScale() { return this.Scale != null && !this.Scale.SequenceEqual(new[] { 1.0f, 1.0f, 1.0f }); }
        public bool ShouldSerializeTranslation() { return this.Translation != null && !this.Translation.SequenceEqual(new[] { 0.0f, 0.0f, 0.0f }); }
    }

    [Serializable]
    public class Sampler : ChildOfRootProperty
    {
    }

    [Serializable]
    public class Scene : ChildOfRootProperty
    {
        public IEnumerable<int> Nodes;
    }

    [Serializable]
    public class Texture : ChildOfRootProperty
    {
        public int Sampler;
        public int Source;
    }

    [Serializable]
    public class Gltf
    {
        public IEnumerable<Accessor> Accessors;
        public Asset Asset;
        public IEnumerable<BufferView> BufferViews;
        public IEnumerable<Buffer> Buffers;
        public IEnumerable<string> ExtensionsUsed;
        public IEnumerable<Image> Images;
        public IEnumerable<Mesh> Meshes;
        public IEnumerable<Material> Materials;
        public IEnumerable<Node> Nodes;
        public IEnumerable<Sampler> Samplers;
        public int Scene;
        public IEnumerable<Scene> Scenes;
        public IEnumerable<Texture> Textures;
    }
}
