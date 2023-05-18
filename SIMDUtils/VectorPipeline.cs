using System.Numerics;

namespace SimdUtils;

/// <summary>Encapsulates a method that has one parameter and returns a value of the type specified by the <typeparamref name="TResult" /> parameter.</summary>
/// <param name="arg">The parameter of the method that this delegate encapsulates.</param>
/// <typeparam name="T">The type of the parameter of the method that this delegate encapsulates.</typeparam>
/// <typeparam name="TResult">The type of the return value of the method that this delegate encapsulates.</typeparam>
/// <returns>The return value of the method that this delegate encapsulates.</returns>
public delegate Vector<T> VecFunc<T>(Vector<T> values)
    where T : struct;

// public delegate Vector<T> VecFunc<T, TArg>(Vector<T> values, TArg arg)
//     where T : struct;

public class VectorPipeline<T>
    where T : struct, INumber<T>
{
    private List<VecFunc<T>> _vecFuncs = new();

    public VectorPipeline<T> Select(VecFunc<T> selector)
    {
        _vecFuncs.Add(selector);
        return this;
    }

    private static VecFunc<T> Combine(ICollection<VecFunc<T>> funcs)
        => funcs.Aggregate((accum, cur) => val => cur(accum(val)));

    public T[] Run(T[] span)
    {
        var vecSize = Vector<T>.Count;
        T[] ret = new T[span.Length];
        int extraLen = span.Length % vecSize;
        int lastI = span.Length - extraLen;
        for (int i = 0; i < lastI; i += vecSize) {
            Vector<T> vec = new Vector<T>(span, i);
            for (int j = 0; j < _vecFuncs.Count; j++) {
                vec = _vecFuncs[j](vec);
            }

            if (i != lastI) {
                vec.CopyTo(ret, i);
            } else {
                for (int j = 0; j < extraLen; j++) {
                    ret[lastI + j] = vec[j];
                }
            }
        }

        return ret;
    }

    public T[] Run2(T[] span)
    {
        var vecSize = Vector<T>.Count;
        T[] ret = new T[span.Length];
        int extraLen = span.Length % vecSize;
        int lastI = span.Length - extraLen;
        var fn = Combine(_vecFuncs);
        for (int i = 0; i < span.Length; i += vecSize) {
            Vector<T> vec = new Vector<T>(span, i);
            vec = fn(vec);

            if (i != lastI) {
                vec.CopyTo(ret, i);
            } else {
                for (int j = 0; j < extraLen; j++) {
                    ret[lastI + j] = vec[j];
                }
            }
        }

        return ret;
    }
}
