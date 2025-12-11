using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Pgvector;

namespace Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore.Converters;

/// <summary>
/// Converts between float[]? and Pgvector.Vector for PostgreSQL pgvector support.
/// This allows using float[]? as the entity property type while storing as vector in PostgreSQL.
/// </summary>
public class FloatArrayToVectorConverter : ValueConverter<float[]?, Vector?>
{
    public FloatArrayToVectorConverter()
        : base(
            floatArray => floatArray != null ? new Vector(floatArray) : null,
            vector => vector != null ? vector.ToArray() : null)
    {
    }
}
