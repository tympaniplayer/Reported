using Reported.Services;

namespace Reported.Tests.Helpers;

public sealed class FakeRandomProvider : IRandomProvider
{
    private readonly int[] _returnValues;
    private int _index;

    public FakeRandomProvider(params int[] returnValues)
    {
        _returnValues = returnValues;
    }

    public int Next(int minValue, int maxValue)
    {
        if (_index >= _returnValues.Length)
            throw new InvalidOperationException(
                $"FakeRandomProvider exhausted: expected at most {_returnValues.Length} calls, but got call #{_index + 1}");

        return _returnValues[_index++];
    }
}
