using System.Numerics;
using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;
using SimdUtils;

namespace Benchmarks;

// |                   Method | Count |      Mean |     Error |    StdDev |   Gen0 | Allocated |
// |------------------------- |------ |----------:|----------:|----------:|-------:|----------:|
// | Reference_ImperativeSimd | 10001 |  6.871 us | 0.1335 us | 0.1828 us | 0.4730 |  39.09 KB |
// |                     Func | 10001 | 11.198 us | 0.1865 us | 0.1995 us | 0.4730 |  39.09 KB |
// |                   Struct | 10001 |  7.414 us | 0.1106 us | 0.1034 us | 0.4730 |  39.09 KB |

[MemoryDiagnoser]
public class Bench_FuncVsStruct
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
        var ret = _values.ToArray();

        SelectHardcoded(ret);

        return ret;
    }

    private static readonly Vector<float> _five = new(5f);

    [Benchmark]
    public object Func()
    {
        var ret = _values.ToArray();

        SelectFunc(ret, (val) => (val * 5 + _five) / _five);

        return ret;
    }

    [Benchmark]
    public object Struct()
    {
        var ret = _values.ToArray();

        SelectStruct(ret, new CombinedSelector_AggressiveInlining());

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

    public static void SelectStruct<T, TFunc>(T[] arr, TFunc selector)
        where T : unmanaged, INumber<T>
        where TFunc : struct, IFunc<Vector<T>, Vector<T>>
    {
        var vecSize = Vector<T>.Count;
        int extraLen = arr.Length % vecSize;
        int lastI = arr.Length - extraLen;
        for (int i = 0; i < lastI; i += vecSize) {
            Vector<T> vec = new Vector<T>(arr, i);
            vec = selector.Invoke(vec);
            vec.CopyTo(arr, i);
        }

        if (lastI == arr.Length) {
            return;
        }

        Span<T> remaining = stackalloc T[vecSize];
        arr.AsSpan(lastI).CopyTo(remaining);
        Vector<T> lastVec = new Vector<T>(remaining);
        lastVec = selector.Invoke(lastVec);

        for (int j = 0; j < extraLen; j++) {
            arr[lastI + j] = lastVec[j];
        }
    }

    public static void SelectFunc<T>(T[] arr, Func<Vector<T>, Vector<T>> selector)
        where T : unmanaged, INumber<T>
    {
        var vecSize = Vector<T>.Count;
        int extraLen = arr.Length % vecSize;
        int lastI = arr.Length - extraLen;
        for (int i = 0; i < lastI; i += vecSize) {
            Vector<T> vec = new Vector<T>(arr, i);
            vec = selector.Invoke(vec);
            vec.CopyTo(arr, i);
        }

        if (lastI == arr.Length) {
            return;
        }

        Span<T> remaining = stackalloc T[vecSize];
        arr.AsSpan(lastI).CopyTo(remaining);
        Vector<T> lastVec = new Vector<T>(remaining);
        lastVec = selector.Invoke(lastVec);

        for (int j = 0; j < extraLen; j++) {
            arr[lastI + j] = lastVec[j];
        }
    }

    public static void SelectHardcoded(float[] arr)
    {
        var vecSize = Vector<float>.Count;
        int extraLen = arr.Length % vecSize;
        int lastI = arr.Length - extraLen;
        for (int i = 0; i < lastI; i += vecSize) {
            Vector<float> vec = new Vector<float>(arr, i);
            vec = (vec * 5 + _five) / _five;
            vec.CopyTo(arr, i);
        }

        if (lastI == arr.Length) {
            return;
        }

        Span<float> remaining = stackalloc float[vecSize];
        arr.AsSpan(lastI).CopyTo(remaining);
        Vector<float> lastVec = new Vector<float>(remaining);
        lastVec = (lastVec * 5 + _five) / _five;

        for (int j = 0; j < extraLen; j++) {
            arr[lastI + j] = lastVec[j];
        }
    }
}
