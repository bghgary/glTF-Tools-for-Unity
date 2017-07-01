using System.IO;
using UnityEngine;

public static class BinaryWriterExtensions
{
    public static void Write(this BinaryWriter binaryWriter, Vector2 value)
    {
        binaryWriter.Write(value.x);
        binaryWriter.Write(value.y);
    }

    public static void Write(this BinaryWriter binaryWriter, Vector3 value)
    {
        binaryWriter.Write(value.x);
        binaryWriter.Write(value.y);
        binaryWriter.Write(value.z);
    }

    public static void Write(this BinaryWriter binaryWriter, Vector4 value)
    {
        binaryWriter.Write(value.x);
        binaryWriter.Write(value.y);
        binaryWriter.Write(value.z);
        binaryWriter.Write(value.w);
    }

    public static void Write(this BinaryWriter binaryWriter, Quaternion value)
    {
        binaryWriter.Write(value.x);
        binaryWriter.Write(value.y);
        binaryWriter.Write(value.z);
        binaryWriter.Write(value.w);
    }

    public static void Write(this BinaryWriter binaryWriter, Color value)
    {
        binaryWriter.Write(value.r);
        binaryWriter.Write(value.g);
        binaryWriter.Write(value.b);
        binaryWriter.Write(value.a);
    }

    public static void Write(this BinaryWriter binaryWriter, UShortVector4 value)
    {
        binaryWriter.Write(value.x);
        binaryWriter.Write(value.y);
        binaryWriter.Write(value.z);
        binaryWriter.Write(value.w);
    }

    public static void Write(this BinaryWriter binaryWriter, ByteVector4 value)
    {
        binaryWriter.Write(value.x);
        binaryWriter.Write(value.y);
        binaryWriter.Write(value.z);
        binaryWriter.Write(value.w);
    }

    public static void Write(this BinaryWriter binaryWriter, Matrix4x4 value)
    {
        binaryWriter.Write(value[00]);
        binaryWriter.Write(value[01]);
        binaryWriter.Write(value[02]);
        binaryWriter.Write(value[03]);
        binaryWriter.Write(value[04]);
        binaryWriter.Write(value[05]);
        binaryWriter.Write(value[06]);
        binaryWriter.Write(value[07]);
        binaryWriter.Write(value[08]);
        binaryWriter.Write(value[09]);
        binaryWriter.Write(value[10]);
        binaryWriter.Write(value[11]);
        binaryWriter.Write(value[12]);
        binaryWriter.Write(value[13]);
        binaryWriter.Write(value[14]);
        binaryWriter.Write(value[15]);
    }
}
