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
using System.Runtime.InteropServices;
using SK.Libretro.Utilities;

namespace SK.Libretro
{
    public partial class Wrapper
    {
        public void RetroLogPrintf(retro_log_level log_level, string format, IntPtr args)
        {
            if (log_level > retro_log_level.RETRO_LOG_INFO)
            {
                string message = FormatRetroLog(format, args);
                Log.Info($"{log_level}: {message}", "Libretro.Wrapper.RetroLogPrintf");
            }
        }

        private static string FormatRetroLog(string format, IntPtr args)
        {
            string msg = format.TrimEnd('\n', '\r');
            if (args == IntPtr.Zero || !msg.Contains('%'))
                return msg;

            // On x64, args is the first variadic argument as a raw register value.
            // Handle the common single-%s case (e.g. "Failed to load bios at: %s").
            int pct = msg.IndexOf('%');
            if (pct >= 0 && pct + 1 < msg.Length)
            {
                char spec = msg[pct + 1];
                if (spec == 's')
                {
                    string argStr = Marshal.PtrToStringAnsi(args);
                    if (argStr != null)
                        return msg.Substring(0, pct) + argStr + msg.Substring(pct + 2);
                }
                else if (spec == 'd' || spec == 'i' || spec == 'u')
                {
                    return msg.Substring(0, pct) + args.ToInt64() + msg.Substring(pct + 2);
                }
            }

            return msg;
        }
    }
}
