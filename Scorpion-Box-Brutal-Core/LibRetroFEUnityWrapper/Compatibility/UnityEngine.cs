using System;

namespace LibRetroFE_WrapperOnly.Compatibility
{
    internal static class UnityEngine
    {
        internal static UnityApplication Application = new UnityApplication();
        internal static UnityJsonUtility JsonUtility = new UnityJsonUtility();
    }

    internal class UnityJsonUtility
    {
        internal T FromJson<T>(string jsonString) where T : class
        {
            throw new NotImplementedException();
        }

        internal string ToJson<T>(T sourceObject, bool v)
        {
            throw new NotImplementedException();
        }
    }

    internal class UnityApplication
    {
        public string streamingAssetsPath
        {
            get
            {
                throw new NotImplementedException();
            }
        }
    }
}
