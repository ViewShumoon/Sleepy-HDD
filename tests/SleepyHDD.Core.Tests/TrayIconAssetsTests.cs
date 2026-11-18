using System.Buffers.Binary;
using Xunit;

namespace SleepyHDD.Core.Tests;

/// <summary>
/// 闪退回归守卫：托盘图标资产必须是合法的 ICONDIR 容器（单帧 PNG-in-ICO），
/// 否则 System.Drawing.Icon 构造会抛 ArgumentException（Argument 'picture' must be a
/// picture that can be used as a Icon），并经原生消息泵直接杀死进程。
/// 注意：本测试只能校验容器结构——H.NotifyIcon 的 Icon DP 转换发生在原生消息泵回调内，
/// 无法在单测中复现；真正的端到端验证 = 提权启动 App 观察 30s+ 存活（watch-crash 回路）。
/// </summary>
public class TrayIconAssetsTests
{
    [Theory]
    [InlineData("tray-normal.ico")]
    [InlineData("tray-sleeping.ico")]
    public void TrayIco_IsWellFormedSingleFramePngIco(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Assets", fileName);
        Assert.True(File.Exists(path), $"托盘 ICO 未复制到测试输出：{path}");

        var bytes = File.ReadAllBytes(path);
        Assert.True(bytes.Length >= 22, $"{fileName} 小于 ICONDIR+首帧表头的 22 字节");

        // ICONDIR：reserved=0、type=1、count>=1
        Assert.Equal(0, BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(0, 2)));
        Assert.Equal(1, BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(2, 2)));
        Assert.True(BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(4, 2)) >= 1);

        // 首帧表项：32x32、planes=1、bpp=32
        Assert.Equal(32, bytes[6]);
        Assert.Equal(32, bytes[7]);
        Assert.Equal(1, BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(10, 2)));
        Assert.Equal(32, BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(12, 2)));

        // 单帧：bytesInRes 应恰为表头之后的全部字节，imageOffset=22
        var size = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(14, 4));
        var offset = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(18, 4));
        Assert.Equal(22u, offset);
        Assert.Equal((uint)bytes.Length - 22, size);

        // 载荷以 PNG 签名开头（本机已验证 PNG-in-ICO 可被 Icon API 加载，同 app.ico 容器样式）
        ReadOnlySpan<byte> pngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        Assert.True(bytes.AsSpan(22, 8).SequenceEqual(pngSignature), $"{fileName} 首帧不是 PNG-in-ICO");
    }
}
