namespace LibRetroFE_WrapperOnly.Compatibility
{
    internal class Debug
    {
        internal static void Log(string v)
        {
            System.Diagnostics.Debug.WriteLine(v);
        }
    }
}
