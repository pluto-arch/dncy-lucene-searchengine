namespace Dncy.Tools.LuceneNet.Models
{
    public class UpdateModel<T>
    {
        public string IdFieldName { get; set; }

        public string DataId { get; set; }

        public T Data { get; set; }
    }
}

