namespace Reported.Services;

public interface IRandomProvider
{
    int Next(int minValue, int maxValue);
}
