using System.Collections.Generic;

namespace Dncy.Tools.LuceneNet
{
    public class SearchResultCollection<T>
        where T : class, new()
    {
        /// <summary>
        /// 实体集
        /// </summary>
        public IList<T> Results { get; set; }

        /// <summary>
        /// 总条数
        /// </summary>
        public int TotalHits { get; set; }

        public SearchResultCollection()
        {
            Results = new List<T>();
        }
    }
}

