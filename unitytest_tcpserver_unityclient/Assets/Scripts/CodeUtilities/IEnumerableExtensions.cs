using System;
using System.Collections.Generic;
using System.Linq;

public static class IEnumerableExtensions
{
	/// <summary>
	/// Returns the element with the maximum value in a generic sequence.
	/// </summary>
	/// <typeparam name="TSource"></typeparam>
	/// <typeparam name="TKey"></typeparam>
	/// <param name="source"></param>
	/// <param name="keySelector"></param>
	/// <returns></returns>
	public static TSource MaxBy<TSource, TKey>(this IEnumerable<TSource> source, Func<TSource, TKey> keySelector) where TKey : IComparable
	{
		return source.Aggregate((cur, next) => keySelector(cur).CompareTo(keySelector(next)) < 0 ? next : cur);
	}

	/// <summary>
	/// Returns the element with the maximum value in a generic sequence.
	/// </summary>
	/// <typeparam name="TSource"></typeparam>
	/// <typeparam name="TKey"></typeparam>
	/// <param name="source"></param>
	/// <param name="keySelector"></param>
	/// <returns></returns>
	public static TSource MaxByOrDefault<TSource, TKey>(this IEnumerable<TSource> source, Func<TSource, TKey> keySelector) where TKey : IComparable
	{
		var elements = source as List<TSource> ?? source.ToList();
		return elements.Any() ? elements.Aggregate((cur, next) => keySelector(cur).CompareTo(keySelector(next)) < 0 ? next : cur) : default(TSource);
	}

	/// <summary>
	/// Returns the element with the minimum value in a generic sequence.
	/// </summary>
	/// <typeparam name="TSource"></typeparam>
	/// <typeparam name="TKey"></typeparam>
	/// <param name="source"></param>
	/// <param name="keySelector"></param>
	/// <returns></returns>
	public static TSource MinBy<TSource, TKey>(this IEnumerable<TSource> source, Func<TSource, TKey> keySelector) where TKey : IComparable
	{
		return source.Aggregate((cur, next) => keySelector(cur).CompareTo(keySelector(next)) > 0 ? next : cur);
	}

	/// <summary>
	/// Returns the element with the minimum value in a generic sequence.
	/// </summary>
	/// <typeparam name="TSource"></typeparam>
	/// <typeparam name="TKey"></typeparam>
	/// <param name="source"></param>
	/// <param name="keySelector"></param>
	/// <returns></returns>
	public static TSource MinByOrDefault<TSource, TKey>(this IEnumerable<TSource> source, Func<TSource, TKey> keySelector) where TKey : IComparable
	{
		var elements = source as List<TSource> ?? source.ToList();
		return elements.Any() ? elements.Aggregate((cur, next) => keySelector(cur).CompareTo(keySelector(next)) > 0 ? next : cur) : default(TSource);
	}

	/// <summary>
	/// Returns the element closest to samplingPoint using linear sampling.
	/// </summary>
	/// <typeparam name="TSource"></typeparam>
	/// <param name="source"></param>
	/// <param name="samplePoint"></param>
	/// <param name="dataPointSelector">Determines the weight for each element</param>
	/// <returns></returns>
	public static TSource GetByWeightedSample<TSource>(this IEnumerable<TSource> source, double samplePoint, Func<TSource, int> dataPointSelector)
	{
		if(samplePoint < 0 || 1 < samplePoint)
			throw new ArgumentOutOfRangeException(nameof(samplePoint), "Sampling must be chosen in intervall 0-1");
		
		var elements = source as List<TSource> ?? source.ToList();
		var totalWeight = elements.Sum(dataPointSelector);
		if(totalWeight == 0)
			throw new IndexOutOfRangeException("No values have any weight above 0.");

		if (samplePoint == 1.0)
		{
			return elements.LastOrDefault(x => dataPointSelector(x) > 0);
		}

		var targetWeight = totalWeight * samplePoint;
		var samplePositionWeight = 0;

		foreach(var element in elements)
		{
			samplePositionWeight += dataPointSelector(element);
			if(samplePositionWeight > targetWeight)
			{
				return element;
			}
		}

		throw new KeyNotFoundException("Uknown error, could not find a sampled value.");
	}

    /// <summary>
	/// Returns the element closest to samplingPoint using linear sampling. Note: Float version not tested for accuracy!
	/// </summary>
	/// <typeparam name="TSource"></typeparam>
	/// <param name="source"></param>
	/// <param name="samplePoint"></param>
	/// <param name="dataPointSelector">Determines the weight for each element</param>
	/// <returns></returns>
	public static TSource GetByWeightedSample<TSource>(this IEnumerable<TSource> source, float samplePoint, Func<TSource, float> dataPointSelector)
    {
        if (samplePoint < 0 || 1 < samplePoint)
            throw new ArgumentOutOfRangeException(nameof(samplePoint), "Sampling must be chosen in intervall 0-1");

        var elements = source as List<TSource> ?? source.ToList();
        var totalWeight = elements.Sum(dataPointSelector);
        if (totalWeight == 0)
            throw new IndexOutOfRangeException("No values have any weight above 0.");

        if (samplePoint == 1.0)
        {
            return elements.LastOrDefault(x => dataPointSelector(x) > 0);
        }

        var targetWeight = totalWeight * samplePoint;
        var samplePositionWeight = 0f;

        foreach (var element in elements)
        {
            samplePositionWeight += dataPointSelector(element);
            if (samplePositionWeight > targetWeight)
            {
                return element;
            }
        }

        throw new KeyNotFoundException("Uknown error, could not find a sampled value.");
    }

    /// <summary>
    /// Applies an accumulator function over a sequence. Every accumulated element will be concated together with specified delimitter.
    /// </summary>
    /// <typeparam name="TSource"></typeparam>
    /// <param name="func">An accumulator function to be invoked on each element.</param>
    /// <returns></returns>
    public static string JoinStrings<TSource>(this IEnumerable<TSource> source, Func<TSource, string> func, string delimiter = " ")
    {
        return string.Join(delimiter, source.Select(func));
    }
}
