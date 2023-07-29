using System.Numerics;

namespace SimdUtils;

public interface IVectorFunc<T> : IFunc<Vector<T>, Vector<T>>
where T : struct
{ }


// these mutate
public class Vectorizer
{
    // select using the value delegate idea
    public static void Select<T, TFunc>(Span<T> arr, TFunc selector)
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
