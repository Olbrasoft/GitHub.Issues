using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Pgvector;

namespace Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore.Converters;

/// <summary>
/// Value converter for storing Pgvector.Vector as binary in SQL Server.
/// </summary>
/// <remarks>
/// SQL Server native VECTOR type (GA summer 2025) doesn't have EF Core mapping yet.
/// This converter stores vectors as varbinary(max) for compatibility.
/// Vector search uses raw SQL with VECTOR_DISTANCE function.
/// </remarks>
public class VectorToBinaryConverter : ValueConverter<Vector, byte[]>
{
    private const int FloatSize = sizeof(float);

    public VectorToBinaryConverter()
        : base(
            v => VectorToBytes(v),
            b => BytesToVector(b))
    {
    }

    private static byte[] VectorToBytes(Vector vector)
    {
        if (vector == null)
            return Array.Empty<byte>();

        var floats = vector.ToArray();
        var bytes = new byte[floats.Length * FloatSize];

        for (int i = 0; i < floats.Length; i++)
        {
            BitConverter.GetBytes(floats[i]).CopyTo(bytes, i * FloatSize);
        }

        return bytes;
    }

    private static Vector BytesToVector(byte[] bytes)
    {
        if (bytes == null || bytes.Length == 0)
            return null!; // Return null for empty bytes - don't assume dimension

        var floatCount = bytes.Length / FloatSize;
        var floats = new float[floatCount];

        for (int i = 0; i < floatCount; i++)
        {
            floats[i] = BitConverter.ToSingle(bytes, i * FloatSize);
        }

        return new Vector(floats);
    }
}
