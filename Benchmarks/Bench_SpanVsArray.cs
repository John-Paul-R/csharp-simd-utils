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

// Ah. I was including the TArray in the benchmark. That was most of the time. E: nope, not true, wtf am I seeing here?
// |                        Method | Count |      Mean |     Error |    StdDev |    Median | Allocated |
// |------------------------------ |------ |----------:|----------:|----------:|----------:|----------:|
// |      Reference_ImperativeSimd | 10001 | 12.706 us | 0.5144 us | 1.5006 us | 12.930 us |   40968 B |
// |                         Array | 10001 | 16.751 us | 0.9815 us | 2.7845 us | 15.108 us |     992 B |
// |                StackallocSpan | 10001 |  3.296 us | 0.0695 us | 0.1679 us |  3.236 us |     936 B |
// |                      FullSpan | 10001 |  3.438 us | 0.0461 us | 0.0360 us |  3.421 us |     936 B |
// |          Array_InstanceMember | 10001 | 17.149 us | 0.2935 us | 0.2451 us | 17.098 us |     992 B |
// | StackallocSpan_InstanceMember | 10001 |  3.212 us | 0.0679 us | 0.0859 us |  3.185 us |     936 B |

// aha, [IterationSetup] is cursed, don't use it for microbenchmarks
// |                        Method | Count |     Mean |     Error |    StdDev | Allocated |
// |------------------------------ |------ |---------:|----------:|----------:|----------:|
// |   Reference_ImperativeForLoop | 10001 | 9.032 us | 0.0990 us | 0.0926 us |         - |
// |      Reference_ImperativeSimd | 10001 | 2.362 us | 0.0466 us | 0.0779 us |         - |
// |                         Array | 10001 | 3.060 us | 0.0195 us | 0.0182 us |      56 B |
// |                StackallocSpan | 10001 | 3.036 us | 0.0126 us | 0.0118 us |         - |
// |                      FullSpan | 10001 | 3.113 us | 0.0602 us | 0.0694 us |         - |
// |          Array_InstanceMember | 10001 | 3.044 us | 0.0077 us | 0.0068 us |      56 B |
// | StackallocSpan_InstanceMember | 10001 | 3.039 us | 0.0048 us | 0.0038 us |         - |

[MemoryDiagnoser]
public class Bench_SpanVsArray
{
// 101, 1001,
    [Params(10001)]
    public static int Count = 10_001;
    private const int RandSeed = 123490;
    private static Random _rand = null!;
    private float[] _values = null!;

    private float[] _valuesForBench = null!;

    [GlobalSetup]
    public void GlobalSetup()
    {
        _rand = new(RandSeed);
        _values = Enumerable.Range(0, Count)
            .Select(_ => _rand.NextSingle())
            .ToArray();
        _valuesForBench = _values.ToArray();
    }

    [Benchmark]
    public object Reference_ImperativeForLoop()
    {
        var ret = _valuesForBench;

        for (int i = 0; i < _valuesForBench.Length; i++) {
            ret[i] = (_valuesForBench[i] * 5 + 5) / 5;
        }

        return ret;
    }

    [Benchmark]
    public object Reference_ImperativeSimd()
    {
        int vecSize = Vector<float>.Count;
        Vector<float> vectorFives = new(5F);
        float[] ret = _valuesForBench;

        int extraLen = _valuesForBench.Length % vecSize;
        int vectorizableCount = _valuesForBench.Length - extraLen;
        for (int i = 0; i < vectorizableCount; i += vecSize) {
            var vec = new Vector<float>(_valuesForBench, i);
            ((vec * 5 + vectorFives) / vectorFives).CopyTo(ret, i);
        }

        for (int i = vectorizableCount; i < _valuesForBench.Length; i++) {
            ret[i] = (_valuesForBench[i] * 5 + 5) / 5;
        }

        return ret;
    }

    [Benchmark]
    public object Array()
    {
        var ret = _valuesForBench;

        Vectorizer.Select(ret, new CombinedSelector_AggressiveInlining());

        return ret;
    }

    [Benchmark]
    public object StackallocSpan()
    {
        var ret = _valuesForBench;

        Vectorizer.SelectStackallocSpan(ret, new CombinedSelector_AggressiveInlining());

        return ret;
    }

    [Benchmark]
    public object FullSpan()
    {
        var ret = _valuesForBench;

        Vectorizer.SelectFullSpan(ret.AsSpan(), new CombinedSelector_AggressiveInlining());

        return ret;
    }

    [Benchmark]
    public object Array_InstanceMember()
    {
        var ret = _valuesForBench;

        Vectorizer.Select(ret, new CombinedSelector_AggressiveInlining_InstanceMember());

        return ret;
    }

    [Benchmark]
    public object StackallocSpan_InstanceMember()
    {
        var ret = _valuesForBench;

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
