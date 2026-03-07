using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Toolchains.InProcess.Emit;

namespace Benchmark;

/// <summary>使用 InProcess 工具链避免 BDN 重新编译引发的 Deterministic 冲突</summary>
public class AntiVirusFriendlyConfig : ManualConfig
{
    public AntiVirusFriendlyConfig()
    {
        AddJob(Job.Default.WithToolchain(InProcessEmitToolchain.Instance));
    }
}
