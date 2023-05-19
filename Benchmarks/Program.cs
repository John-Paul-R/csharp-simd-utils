using BenchmarkDotNet.Running;
using Benchmarks;

// BenchmarkRunner.Run<Bench_SpanVsArray>();
// BenchmarkRunner.Run<Bench_Scratch>();
BenchmarkRunner.Run<Bench_FuncVsStruct>();
