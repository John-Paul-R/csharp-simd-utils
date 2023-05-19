// See https://aka.ms/new-console-template for more information

using BenchmarkDotNet.Running;
using System.Numerics;
using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;
using SimdUtils;

// |                      Method |      Mean |     Error |    StdDev |   Gen0 | Allocated |
// |---------------------------- |----------:|----------:|----------:|-------:|----------:|
// |            VectorizerMutate | 14.349 us | 0.2774 us | 0.3508 us | 0.3815 |  32.02 KB |
// |              ImperativeSimd |  4.884 us | 0.0596 us | 0.0498 us | 0.3891 |  32.02 KB |
// | ImperativeSeparateLoopsSimd |  6.801 us | 0.0816 us | 0.0763 us | 0.3891 |  32.02 KB |
// |        ImperativeSimpleLoop | 10.053 us | 0.1473 us | 0.1377 us | 0.3815 |  32.02 KB |
// |              VectorPipeline | 19.268 us | 0.1733 us | 0.1447 us | 0.3662 |  32.02 KB |
// |              EquivalentLinq | 94.193 us | 1.1602 us | 1.0853 us | 0.3662 |  32.35 KB |
// |     ImperativeSeparateLoops | 16.576 us | 0.0658 us | 0.0616 us | 0.3662 |  32.02 KB |

// With AggressiveInlining:
// |                      Method |      Mean |     Error |    StdDev |   Gen0 | Allocated |
// |---------------------------- |----------:|----------:|----------:|-------:|----------:|
// |            VectorizerMutate |  6.789 us | 0.1135 us | 0.1062 us | 0.3891 |  32.02 KB |
// |              ImperativeSimd |  4.831 us | 0.0864 us | 0.1123 us | 0.3891 |  32.02 KB |
// | ImperativeSeparateLoopsSimd |  6.831 us | 0.0584 us | 0.0518 us | 0.3891 |  32.02 KB |
// |        ImperativeSimpleLoop |  9.680 us | 0.0503 us | 0.0420 us | 0.3815 |  32.02 KB |
// |              VectorPipeline | 18.932 us | 0.0861 us | 0.0764 us | 0.3662 |  32.02 KB |
// |              EquivalentLinq | 92.151 us | 0.4228 us | 0.3748 us | 0.3662 |  32.35 KB |
// |     ImperativeSeparateLoops | 16.529 us | 0.1017 us | 0.0951 us | 0.3662 |  32.02 KB |

// |                                       Method |       Mean |     Error |    StdDev |   Gen0 | Allocated |
// |--------------------------------------------- |-----------:|----------:|----------:|-------:|----------:|
// |                             VectorizerMutate |  18.425 us | 0.3661 us | 0.7560 us | 0.4578 |  39.09 KB |
// |          VectorizerMutate_AggressiveInlining |  11.850 us | 0.2326 us | 0.2678 us | 0.4730 |  39.09 KB |
// | VectorizerMutate_Combined_AggressiveInlining |   8.262 us | 0.1346 us | 0.1193 us | 0.4730 |  39.09 KB |
// |                               ImperativeSimd |   6.229 us | 0.0890 us | 0.0832 us | 0.4730 |  39.09 KB |
// |                  ImperativeSeparateLoopsSimd |   8.972 us | 0.1728 us | 0.1617 us | 0.4730 |  39.09 KB |
// |                         ImperativeSimpleLoop |  12.601 us | 0.1820 us | 0.1702 us | 0.4730 |  39.09 KB |
// |                               VectorPipeline |  24.421 us | 0.2453 us | 0.2294 us | 0.4578 |  39.09 KB |
// |                               EquivalentLinq | 116.211 us | 0.6167 us | 0.5467 us | 0.3662 |  39.42 KB |
// |                      ImperativeSeparateLoops |  21.152 us | 0.2030 us | 0.1800 us | 0.4578 |  39.09 KB |


BenchmarkRunner.Run<Bench>();
// var bench = new Bench();
// bench.GlobalSetup();
// bench.VectorizerMutate_AggressiveInlining();

[MemoryDiagnoser]
public class Bench
{
    // [Params(100, 1000, 10000)]
    public static int Count = 10_001;
    private const int RandSeed = 123490;
    private static Random _rand;
    private float[] _values;

    [GlobalSetup]
    public void GlobalSetup()
    {
        _rand = new(RandSeed);
        _values = Enumerable.Range(0, Count)
            .Select(_ => _rand.NextSingle())
            .ToArray();
    }

    private static readonly Vector<float> _vectorFives = new(5F);
    private VectorPipeline<float> _pipeline = new VectorPipeline<float>()
        .Select(vec => vec * 5)
        .Select(vec => vec + _vectorFives)
        .Select(vec => vec / _vectorFives)
        ;

#region Benchmarks
    [Benchmark]
    public object VectorizerMutate()
    {
        var ret = _values.ToArray();

        Vectorizer.Select(ret, new TimesFiveSelector());
        Vectorizer.Select(ret, new AddFiveSelector());
        Vectorizer.Select(ret, new DivByFiveSelector());

        return ret;
    }

    [Benchmark]
    public object VectorizerMutate_AggressiveInlining()
    {
        var ret = _values.ToArray();

        Vectorizer.Select(ret, new TimesFiveSelector_AggressiveInlining());
        Vectorizer.Select(ret, new AddFiveSelector_AggressiveInlining());
        Vectorizer.Select(ret, new DivByFiveSelector_AggressiveInlining());

        return ret;
    }

    [Benchmark]
    public object VectorizerMutate_Combined_AggressiveInlining()
    {
        var ret = _values.ToArray();

        Vectorizer.Select(ret, new CombinedSelector_AggressiveInlining());

        return ret;
    }

    [Benchmark]
    public object ImperativeSimd()
    {
        int vecSize = Vector<float>.Count;
        float[] ret = new float[_values.Length];

        int extraLen = _values.Length % vecSize;
        int vectorizableCount = _values.Length - extraLen;
        for (int i = 0; i < vectorizableCount; i += vecSize) {
            var vec = new Vector<float>(_values, i);
            ((vec * 5 + _vectorFives) / _vectorFives).CopyTo(ret, i);
        }

        for (int i = vectorizableCount; i < _values.Length; i++) {
            ret[i] = (_values[i] * 5 + 5) / 5;
        }

        return ret;
    }

    [Benchmark]
    public object ImperativeSeparateLoopsSimd()
    {
        int vecSize = Vector<float>.Count;
        float[] ret = _values.ToArray();

        int extraLen = _values.Length % vecSize;
        int vectorizableCount = _values.Length - extraLen;

        {
            for (int i = 0; i < vectorizableCount; i += vecSize) {
                var vec = new Vector<float>(ret, i);
                (vec * 5).CopyTo(ret, i);
            }

            for (int i = vectorizableCount; i < ret.Length; i++) {
                ret[i] = ret[i] * 5;
            }
        }

        {
            for (int i = 0; i < vectorizableCount; i += vecSize) {
                var vec = new Vector<float>(ret, i);
                (vec + _vectorFives).CopyTo(ret, i);
            }

            for (int i = vectorizableCount; i < ret.Length; i++) {
                ret[i] = ret[i] + 5;
            }
        }

        {
            for (int i = 0; i < vectorizableCount; i += vecSize) {
                var vec = new Vector<float>(ret, i);
                (vec / _vectorFives).CopyTo(ret, i);
            }

            for (int i = vectorizableCount; i < ret.Length; i++) {
                ret[i] = ret[i] / 5;
            }
        }

        return ret;
    }

    [Benchmark]
    public object ImperativeSimpleLoop()
    {
        float[] ret = new float[_values.Length];
        for (int i = 0; i < ret.Length; i++) {
            ret[i] = (_values[i] * 5 + 5) / 5;
        }

        return ret;
    }

    [Benchmark]
    public object VectorPipeline()
    {
        return _pipeline.Run(_values);
    }

    [Benchmark]
    public object EquivalentLinq()
    {
        return _values
            .Select(v => v * 5)
            .Select(v => v + 5)
            .Select(v => v / 5)
            .ToArray()
            ;
    }

    [Benchmark]
    public object ImperativeSeparateLoops()
    {
        float[] ret = new float[_values.Length];

        for (int i = 0; i < ret.Length; i++) {
            ret[i] = _values[i] * 5;
        }

        for (int i = 0; i < ret.Length; i++) {
            ret[i] = _values[i] + 5;
        }

        for (int i = 0; i < ret.Length; i++) {
            ret[i] = _values[i] / 5;
        }

        return ret;
    }

#endregion Benchmarks

    struct CombinedSelector_AggressiveInlining : IVectorFunc<float>
    {
        private static readonly Vector<float> _five = new(5f);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector<float> Invoke(Vector<float> val) => (val * 5 + _five) / _five;
    }

    struct TimesFiveSelector_AggressiveInlining : IVectorFunc<float>
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector<float> Invoke(Vector<float> val) => val * 5;
    }

    struct AddFiveSelector_AggressiveInlining : IVectorFunc<float>
    {
        private static readonly Vector<float> _five = new(5f);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector<float> Invoke(Vector<float> val) => val + _five;
    }

    struct DivByFiveSelector_AggressiveInlining : IVectorFunc<float>
    {
        private static readonly Vector<float> _five = new(5f);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector<float> Invoke(Vector<float> val) => val / _five;
    }

    struct TimesFiveSelector : IVectorFunc<float>
    {
        public Vector<float> Invoke(Vector<float> val) => val * 5;
    }

    struct AddFiveSelector : IVectorFunc<float>
    {
        private static readonly Vector<float> _five = new(5f);
        public Vector<float> Invoke(Vector<float> val) => val + _five;
    }

    struct DivByFiveSelector : IVectorFunc<float>
    {
        private static readonly Vector<float> _five = new(5f);
        public Vector<float> Invoke(Vector<float> val) => val / _five;
    }
}

// // * Summary *
//
// BenchmarkDotNet=v0.13.5, OS=manjaro
// AMD Ryzen Threadripper 2920X, 1 CPU, 24 logical and 12 physical cores
// .NET SDK=7.0.203
//   [Host]     : .NET 7.0.5 (7.0.523.17405), X64 RyuJIT AVX2
//   DefaultJob : .NET 7.0.5 (7.0.523.17405), X64 RyuJIT AVX2
//
//
// |                              Method |   Count |             Mean |         Error |        StdDev |     Gen0 |     Gen1 |     Gen2 | Allocated |
// |------------------------------------ |-------- |-----------------:|--------------:|--------------:|---------:|---------:|---------:|----------:|
// |                    VectorizerMutate |     100 |               NA |            NA |            NA |        - |        - |        - |         - |
// | VectorizerMutate_AggressiveInlining |     100 |               NA |            NA |            NA |        - |        - |        - |         - |
// |                      ImperativeSimd |     100 |         73.29 ns |      1.271 ns |      1.248 ns |   0.0050 |        - |        - |     424 B |
// |         ImperativeSeparateLoopsSimd |     100 |        113.99 ns |      2.333 ns |      3.492 ns |   0.0050 |        - |        - |     424 B |
// |                ImperativeSimpleLoop |     100 |        139.44 ns |      1.939 ns |      1.814 ns |   0.0050 |        - |        - |     424 B |
// |                      VectorPipeline |     100 |        242.20 ns |      1.374 ns |      1.218 ns |   0.0048 |        - |        - |     424 B |
// |                      EquivalentLinq |     100 |      1,277.13 ns |      2.477 ns |      2.317 ns |   0.0076 |        - |        - |     760 B |
// |             ImperativeSeparateLoops |     100 |        225.15 ns |      1.602 ns |      1.420 ns |   0.0050 |        - |        - |     424 B |
// |                    VectorizerMutate |   10000 |     17,843.81 ns |    351.102 ns |    799.637 ns |   0.4578 |        - |        - |   40024 B |
// | VectorizerMutate_AggressiveInlining |   10000 |      8,247.73 ns |    127.718 ns |    119.467 ns |   0.4730 |        - |        - |   40024 B |
// |                      ImperativeSimd |   10000 |      5,995.37 ns |    104.911 ns |    124.889 ns |   0.4730 |        - |        - |   40024 B |
// |         ImperativeSeparateLoopsSimd |   10000 |      7,981.64 ns |     62.522 ns |     58.483 ns |   0.4730 |        - |        - |   40024 B |
// |                ImperativeSimpleLoop |   10000 |     11,996.99 ns |    123.441 ns |    115.467 ns |   0.4730 |        - |        - |   40024 B |
// |                      VectorPipeline |   10000 |     23,653.61 ns |    459.084 ns |    471.446 ns |   0.4578 |        - |        - |   40024 B |
// |                      EquivalentLinq |   10000 |    113,520.84 ns |    534.705 ns |    474.002 ns |   0.3662 |        - |        - |   40360 B |
// |             ImperativeSeparateLoops |   10000 |     20,449.60 ns |    225.575 ns |    188.365 ns |   0.4578 |        - |        - |   40024 B |
// |                    VectorizerMutate | 1000000 |  2,425,132.10 ns | 43,450.805 ns | 63,689.627 ns | 394.5313 | 394.5313 | 394.5313 | 4000283 B |
// | VectorizerMutate_AggressiveInlining | 1000000 |  1,669,762.64 ns | 33,183.177 ns | 62,326.050 ns | 398.4375 | 398.4375 | 398.4375 | 4000284 B |
// |                      ImperativeSimd | 1000000 |  1,246,000.54 ns | 24,903.647 ns | 54,664.109 ns | 199.2188 | 199.2188 | 199.2188 | 4000156 B |
// |         ImperativeSeparateLoopsSimd | 1000000 |  1,706,908.67 ns | 33,626.197 ns | 47,139.232 ns | 398.4375 | 398.4375 | 398.4375 | 4000284 B |
// |                ImperativeSimpleLoop | 1000000 |  1,802,051.06 ns | 32,063.977 ns | 32,927.344 ns | 207.0313 | 207.0313 | 207.0313 | 4000161 B |
// |                      VectorPipeline | 1000000 |  2,909,414.73 ns | 48,129.179 ns | 45,020.063 ns | 316.4063 | 316.4063 | 316.4063 | 4000233 B |
// |                      EquivalentLinq | 1000000 | 11,752,178.05 ns | 74,476.903 ns | 69,665.741 ns | 375.0000 | 375.0000 | 375.0000 | 4000618 B |
// |             ImperativeSeparateLoops | 1000000 |  2,655,220.22 ns | 44,187.522 ns | 41,333.035 ns | 394.5313 | 394.5313 | 394.5313 | 4000283 B |
//
// Benchmarks with issues:
//   Bench.VectorizerMutate: DefaultJob [Count=100]
//   Bench.VectorizerMutate_AggressiveInlining: DefaultJob [Count=100]
//
// // * Hints *
// Outliers
//   Bench.ImperativeSimd: Default              -> 2 outliers were removed (80.10 ns, 80.91 ns)
//   Bench.ImperativeSimpleLoop: Default        -> 2 outliers were detected (139.82 ns, 140.11 ns)
//   Bench.VectorPipeline: Default              -> 1 outlier  was  removed (254.45 ns)
//   Bench.ImperativeSeparateLoops: Default     -> 1 outlier  was  removed (235.08 ns)
//   Bench.ImperativeSimd: Default              -> 4 outliers were removed (6.43 us..6.81 us)
//   Bench.EquivalentLinq: Default              -> 1 outlier  was  removed (118.20 us)
//   Bench.ImperativeSeparateLoops: Default     -> 2 outliers were removed (21.02 us, 21.19 us)
//   Bench.VectorizerMutate: Default            -> 4 outliers were removed (2.66 ms..2.79 ms)
//   Bench.ImperativeSeparateLoopsSimd: Default -> 1 outlier  was  removed (1.93 ms)
//   Bench.ImperativeSimpleLoop: Default        -> 2 outliers were removed (1.91 ms, 1.91 ms)
//   Bench.VectorPipeline: Default              -> 1 outlier  was  removed (3.03 ms)
//
// // * Legends *
//   Count     : Value of the 'Count' parameter
//   Mean      : Arithmetic mean of all measurements
//   Error     : Half of 99.9% confidence interval
//   StdDev    : Standard deviation of all measurements
//   Gen0      : GC Generation 0 collects per 1000 operations
//   Gen1      : GC Generation 1 collects per 1000 operations
//   Gen2      : GC Generation 2 collects per 1000 operations
//   Allocated : Allocated memory per single operation (managed only, inclusive, 1KB = 1024B)
//   1 ns      : 1 Nanosecond (0.000000001 sec)
//
// // * Diagnostic Output - MemoryDiagnoser *
//
//
// // ***** BenchmarkRunner: End *****
// Run time: 00:09:13 (553.09 sec), executed benchmarks: 24
//
// Global total time: 00:09:18 (558.68 sec), executed benchmarks: 24
// // * Artifacts cleanup *
