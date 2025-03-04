using System;

namespace Envelopes.Util;

public static class BooleanArrayPacker
{
    public static bool[] Pack(bool[,] input)
    {
        var rows = input.GetLength(0);
        var cols = input.GetLength(1);
        var flatArray = new bool[rows * cols];
        var index = 0;

        for (var i = 0; i < rows; i++)
        {
            for (var j = 0; j < cols; j++)
            {
                flatArray[index++] = input[i, j];
            }
        }

        return flatArray;
    }

    public static bool[,] Unpack(bool[] input, int dimensions)
    {
        if (input.Length != dimensions * dimensions)
        {
            throw new ArgumentException("The length of the array does not match the specified dimensions.");
        }

        var array = new bool[dimensions, dimensions];
        var index = 0;

        for (var i = 0; i < dimensions; i++)
        {
            for (var j = 0; j < dimensions; j++)
            {
                array[i, j] = input[index++];
            }
        }

        return array;
    }

    public static byte[] PackToByteArray(bool[] array)
    {
        var totalBits = array.Length;
        var byteCount = (totalBits + 7) / 8;
        var bytes = new byte[byteCount];

        for (var i = 0; i < totalBits; i++)
        {
            if (array[i])
            {
                bytes[i / 8] |= (byte)(1 << (i % 8));
            }
        }

        return bytes;
    }

    public static bool[] UnpackFromByteArray(byte[] bytes)
    {
        var maxBits = bytes.Length * 8;
        var result = new bool[maxBits];

        for (var i = 0; i < maxBits; i++)
        {
            result[i] = (bytes[i / 8] & (1 << (i % 8))) != 0;
        }

        return result;
    }

    public static bool[,] UnpackFromByteArray(byte[] bytes, int dimensions)
    {
        var linearArray = UnpackFromByteArray(bytes);

        return Unpack(linearArray, dimensions);
    }
}