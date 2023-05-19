using System.Numerics;
using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;
using SimdUtils;

namespace Benchmarks;

// Wha.... this is closer to what I expected...
// This does _not_ match what I observed before, though (span was slower)... though that was admittedly comparing between benchmark runs...

// BenchmarkDotNet=v0.13.5, OS=manjaro
// AMD Ryzen Threadripper 2920X, 1 CPU, 24 logical and 12 physical cores
// .NET SDK=7.0.203
//   [Host]     : .NET 7.0.5 (7.0.523.17405), X64 RyuJIT AVX2
//   DefaultJob : .NET 7.0.5 (7.0.523.17405), X64 RyuJIT AVX2
//
//
// |                   Method | Count |        Mean |      Error |     StdDev |   Gen0 | Allocated |
// |------------------------- |------ |------------:|-----------:|-----------:|-------:|----------:|
// | Reference_ImperativeSimd |   101 |    82.88 ns |   1.718 ns |   2.045 ns | 0.0051 |     432 B |
// |                    Array |   101 |   136.88 ns |   1.442 ns |   1.349 ns | 0.0057 |     488 B |
// |           StackallocSpan |   101 |   127.87 ns |   1.543 ns |   1.443 ns | 0.0050 |     432 B |
// | Reference_ImperativeSimd |  1001 |   744.10 ns |  13.577 ns |  13.942 ns | 0.0477 |    4032 B |
// |                    Array |  1001 |   794.49 ns |   4.516 ns |   4.224 ns | 0.0486 |    4088 B |
// |           StackallocSpan |  1001 |   798.10 ns |   6.558 ns |   6.134 ns | 0.0477 |    4032 B |
// | Reference_ImperativeSimd | 10001 | 6,332.03 ns |  90.937 ns |  75.937 ns | 0.4730 |   40032 B |
// |                    Array | 10001 | 7,497.83 ns | 143.720 ns | 171.089 ns | 0.4730 |   40088 B |
// |           StackallocSpan | 10001 | 7,425.76 ns |  84.308 ns |  74.737 ns | 0.4730 |   40032 B |

[MemoryDiagnoser]
public class Bench_SpanVsArray
{
// 101, 1001,
    [Params(10001)]
    public static int Count = 10_001;
    private const int RandSeed = 123490;
    private static Random _rand = null!;
    private float[] _values = null!;

    [GlobalSetup]
    public void GlobalSetup()
    {
        _rand = new(RandSeed);
        _values = Enumerable.Range(0, Count)
            .Select(_ => _rand.NextSingle())
            .ToArray();
    }

    [Benchmark]
    public object Reference_ImperativeSimd()
    {
        int vecSize = Vector<float>.Count;
        Vector<float> vectorFives = new(5F);
        float[] ret = new float[_values.Length];

        int extraLen = _values.Length % vecSize;
        int vectorizableCount = _values.Length - extraLen;
        for (int i = 0; i < vectorizableCount; i += vecSize) {
            var vec = new Vector<float>(_values, i);
            ((vec * 5 + vectorFives) / vectorFives).CopyTo(ret, i);
        }

        for (int i = vectorizableCount; i < _values.Length; i++) {
            ret[i] = (_values[i] * 5 + 5) / 5;
        }

        return ret;
    }

    [Benchmark]
    public object Array()
    {
        var ret = _values.ToArray();

        Vectorizer.Select(ret, new CombinedSelector_AggressiveInlining());

        return ret;
    }

    [Benchmark]
    public object StackallocSpan()
    {
        var ret = _values.ToArray();

        Vectorizer.SelectStackallocSpan(ret, new CombinedSelector_AggressiveInlining());

        return ret;
    }

    [Benchmark]
    public object Array_InstanceMember()
    {
        var ret = _values.ToArray();

        Vectorizer.Select(ret, new CombinedSelector_AggressiveInlining_InstanceMember());

        return ret;
    }

    [Benchmark]
    public object StackallocSpan_InstanceMember()
    {
        var ret = _values.ToArray();

        Vectorizer.SelectStackallocSpan(ret, new CombinedSelector_AggressiveInlining_InstanceMember());

        return ret;
    }

    struct CombinedSelector_AggressiveInlining : IVectorFunc<float>
    {
        private readonly Vector<float> _five = new(5f);

        public CombinedSelector_AggressiveInlining()
        { }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector<float> Invoke(Vector<float> val) => (val * 5 + _five) / _five;
    }

    struct CombinedSelector_AggressiveInlining_InstanceMember : IVectorFunc<float>
    {
        private readonly Vector<float> _five = new(5f);

        public CombinedSelector_AggressiveInlining_InstanceMember()
        { }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector<float> Invoke(Vector<float> val) => (val * 5 + _five) / _five;
    }
}
