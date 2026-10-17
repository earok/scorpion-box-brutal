/* MIT License

 * Copyright (c) 2020 Skurdt
 *
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:

 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software.

 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE. */

using System;
using System.IO;
using LibRetroFE_WrapperOnly.Compatibility;

namespace SK.Libretro.Utilities
{
    public static class Log
    {
        private static StreamWriter _log;

        static Log()
        {
            var path = "log.txt";
            if (File.Exists(path))
                File.Delete(path);

            var fs = new FileStream(
                path,
                FileMode.Create,
                FileAccess.Write,
                FileShare.Read,
                bufferSize: 1,
                useAsync: false
            );

            _log = new StreamWriter(fs)
            {
                AutoFlush = true
            };

            Console.SetOut(_log);
            Console.SetError(_log);
        }

        public static void Info(string message, string caller = null)
        {
            LogInternal("[INFO]", message, caller);
        }

        public static void Success(string message, string caller = null)
        {
            LogInternal("[SUCCESS]", message, caller);
        }

        public static void Warning(string message, string caller = null)
        {
            LogInternal("[WARNING]", message, caller);
        }

        public static void Error(string message, string caller = null)
        {
            LogInternal("[ERROR]", message, caller);
        }

        public static void Exception(Exception e, string caller = null)
        {
            LogInternal("[EXCEPTION]", e.Message, caller);
        }

        private static void LogInternal(string prefix, string message, string caller)
        {
            _log.WriteLine(prefix + ":" + message);
            Debug.Log($"{prefix} {(string.IsNullOrEmpty(caller) ? "" : $"<color=lightblue>[{caller}]</color> ")}{message}");
        }
        public static void ClearConsole()
        {
        }
    }
}
