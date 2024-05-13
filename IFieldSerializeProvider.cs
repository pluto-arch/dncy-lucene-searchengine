using System;

namespace Dotnetydd.Tools.LuceneNet
{
    public interface IFieldSerializeProvider : IDisposable
    {
        string Serialize(object obj);


        T? Deserialize<T>(string objStr);


        object Deserialize(string objStr, Type type);
    }
}

