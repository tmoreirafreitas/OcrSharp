using System;
using System.Text.RegularExpressions;

namespace OcrSharp.Service.Extensions
{
    public static class DateTimeExtension
    {
        public static string GetDateNowEngFormat(this DateTime date) => Regex.Replace(date.ToString("yyyy-MM-dd HH:mm:ss"), @"[-:\s]+", string.Empty);
    }
}
