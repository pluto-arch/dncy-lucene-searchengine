﻿using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Dotnetydd.Tools.LuceneNet
{
    public static partial class StringExtensions
    {
        /// <summary>
        /// 移除字符串的指定字符
        /// </summary>
        /// <param name="s"></param>
        /// <param name="chars"></param>
        /// <returns></returns>
        internal static string RemoveCharacters(this string s, IEnumerable<char> chars)
        {
            return string.IsNullOrEmpty(s) ? string.Empty : new string(s.Where(c => !chars.Contains(c)).ToArray());
        }

        /// <summary>
        /// 去除html标签后并截取字符串
        /// </summary>
        /// <param name="html">源html</param>
        /// <returns></returns>
        internal static string RemoveHtmlTag(this string html)
        {
            var strText = Regex.Replace(html, "<[^>]+>", "");
            strText = Regex.Replace(strText, "&[^;]+;", "");
            return strText;
        }
    }
}

