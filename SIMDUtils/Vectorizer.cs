using System.Numerics;

namespace SimdUtils;

public interface IVectorFunc<T> : IFunc<Vector<T>, Vector<T>>
where T : struct
{ }

// | Method                        | Count |     Mean |     Error |    StdDev | Allocated |
// |-------------------------------|-------|---------:|----------:|----------:|----------:|
// | Reference_ImperativeForLoop   | 10001 | 8.903 us | 0.0916 us | 0.0812 us |         - |
// | Reference_ImperativeSimd      | 10001 | 2.367 us | 0.0456 us | 0.0506 us |         - |
// | Array                         | 10001 | 3.057 us | 0.0114 us | 0.0107 us |      56 B |
// | StackallocSpan                | 10001 | 3.041 us | 0.0122 us | 0.0114 us |         - |
// | FullSpan                      | 10001 | 3.120 us | 0.0506 us | 0.0448 us |         - |
// | Array_InstanceMember          | 10001 | 3.061 us | 0.0178 us | 0.0166 us |      56 B |
// | StackallocSpan_InstanceMember | 10001 | 3.023 us | 0.0070 us | 0.0058 us |         - |


// these mutate
public class Vectorizer
{
    // select using the value delegate idea
    public static void Select<T, TFunc>(T[] arr, TFunc selector)
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

        // The stackalloc is slower!? (Sometimes!?)
        // --- Stackalloc ---
// |                             VectorizerMutate |  17.351 us | 0.2797 us | 0.2616 us |  17.303 us | 0.4578 |  39.09 KB |
// |          VectorizerMutate_AggressiveInlining |  11.032 us | 0.1510 us | 0.1483 us |  11.092 us | 0.4730 |  39.09 KB |
// | VectorizerMutate_Combined_AggressiveInlining |   8.042 us | 0.0748 us | 0.0700 us |   8.060 us | 0.4730 |  39.09 KB |
        // --- Array ---
// |                             VectorizerMutate |  17.376 us | 0.2595 us | 0.2300 us | 0.4578 |  39.26 KB |
// |          VectorizerMutate_AggressiveInlining |   8.731 us | 0.1581 us | 0.1401 us | 0.4730 |  39.26 KB |
// | VectorizerMutate_Combined_AggressiveInlining |   6.783 us | 0.1353 us | 0.2025 us | 0.4730 |  39.15 KB |

        // Span<T> remaining = stackalloc T[vecSize];
        // arr.AsSpan(lastI).CopyTo(remaining);
        T[] remaining = new T[vecSize];
        Array.Copy(arr, lastI, remaining, 0, extraLen);
        Vector<T> lastVec = new Vector<T>(remaining);
        lastVec = selector.Invoke(lastVec);

        for (int j = 0; j < extraLen; j++) {
            arr[lastI + j] = lastVec[j];
        }
    }

    public static void SelectStackallocSpan<T, TFunc>(T[] arr, TFunc selector)
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

    public static void SelectFullSpan<T, TFunc>(Span<T> arr, TFunc selector)
        where T : unmanaged, INumber<T>
        where TFunc : struct, IFunc<Vector<T>, Vector<T>>
    {
        var vecSize = Vector<T>.Count;
        int extraLen = arr.Length % vecSize;
        int lastI = arr.Length - extraLen;

        for (int i = 0; i < lastI; i += vecSize) {
            Span<T> sub = arr.Slice(i, vecSize);
            Vector<T> vec = selector.Invoke(new Vector<T>(sub));
            vec.CopyTo(sub);
        }

        if (lastI == arr.Length) {
            return;
        }

        Span<T> remaining = stackalloc T[vecSize];
        arr[lastI..].CopyTo(remaining);
        Vector<T> lastVec = selector.Invoke(new Vector<T>(remaining));

        for (int j = 0; j < extraLen; j++) {
            arr[lastI + j] = lastVec[j];
        }
    }
}
