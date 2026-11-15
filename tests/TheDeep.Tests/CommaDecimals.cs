using System;
using System.Globalization;

namespace TheDeep.Tests;

/// <summary>Runs a check on a machine whose locale writes 1,5 (instructions/principles.md#invariant-culture).</summary>
public static class CommaDecimals
{
    /// <summary>What <paramref name="run"/> returns with the current culture writing decimals with a comma.</summary>
    public static T Under<T>(Func<T> run)
    {
        CultureInfo was = CultureInfo.CurrentCulture;
        var comma = (CultureInfo)CultureInfo.InvariantCulture.Clone();
        comma.NumberFormat.NumberDecimalSeparator = ",";
        CultureInfo.CurrentCulture = comma;
        try
        {
            return run();
        }
        finally
        {
            CultureInfo.CurrentCulture = was;
        }
    }
}
