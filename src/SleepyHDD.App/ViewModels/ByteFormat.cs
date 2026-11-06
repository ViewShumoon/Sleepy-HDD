namespace SleepyHDD.App.ViewModels;

/// <summary>
/// 字节数到易读容量文本的格式化。
/// </summary>
internal static class ByteFormat
{
    private const double BytesPerGiB = 1024 * 1024 * 1024;

    /// <summary>以二进制单位格式化；0 或负值（无法获取容量）显示为未知。</summary>
    public static string Format(long bytes)
    {
        if (bytes <= 0)
        {
            return "容量未知";
        }

        double gib = bytes / BytesPerGiB;
        return gib >= 1024
            ? string.Format("{0:F1} TB", gib / 1024)
            : string.Format("{0:F0} GB", gib);
    }

    /// <summary>卷用量文本，如 "已用 123 GB / 共 466 GB"；数据缺失时返回空。</summary>
    public static string FormatUsage(long? totalBytes, long? usedBytes)
    {
        if (totalBytes is not { } total || total <= 0 || usedBytes is not { } used)
        {
            return string.Empty;
        }

        return string.Format("已用 {0} / 共 {1}", Format(used), Format(total));
    }
}
