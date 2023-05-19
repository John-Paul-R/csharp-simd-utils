using System.Numerics;

namespace SimdUtils;

public interface IVectorFunc<T> : IFunc<Vector<T>, Vector<T>>
where T : struct
{ }

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

        Span<T> remaining = stackalloc T[vecSize];
        arr.AsSpan()[lastI..].CopyTo(remaining);
        Vector<T> lastVec = new Vector<T>(remaining);
        lastVec = selector.Invoke(lastVec);

        for (int j = 0; j < extraLen; j++) {
            arr[lastI + j] = lastVec[j];
        }
    }
}
