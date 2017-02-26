using System.IO;
using UnityEngine;

public static class BinaryWriterExtensions
{
    public static void Align(this BinaryWriter writer, int size, byte fillByte)
    {
        var remainder = checked((int)writer.BaseStream.Position) % size;
        if (remainder != 0)
        {
            for (var i = remainder; i < size; i++)
            {
                writer.Write(fillByte);
            }
        }
    }

    public static void Align(this BinaryWriter writer, int size, char fillChar)
    {
        var remainder = checked((int)writer.BaseStream.Position) % size;
        if (remainder != 0)
        {
            for (var i = remainder; i < size; i++)
            {
                writer.Write(fillChar);
            }
        }
    }

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

    public static void Write(this BinaryWriter binaryWriter, Color value)
    {
        binaryWriter.Write(value.r);
        binaryWriter.Write(value.g);
        binaryWriter.Write(value.b);
        binaryWriter.Write(value.a);
    }
}
