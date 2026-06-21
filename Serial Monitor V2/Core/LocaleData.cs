using System.Collections.Generic;

namespace 串口助手
{
    /// <summary>中英双语映射数据。中文即 key，这里只存英文映射。</summary>
    internal static class LocaleData
    {
        /// <summary>中文 key → English value。不在此表的 key 切英文时保留中文。</summary>
        internal static readonly Dictionary<string, string> EnMap = new()
        {
            // P0: 按钮本身
            ["中"] = "中",
            ["EN"] = "EN",
            ["/"] = "/",

            // P1 及以后逐面板补英文映射在此
        };
    }
}
