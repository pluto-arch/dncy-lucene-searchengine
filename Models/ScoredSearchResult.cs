
using System.Collections.Generic;

namespace Dncy.Tools.LuceneNet
{
    public class ScoredSearchResult<T>
        where T : class,new()
    {
        /// <summary>
        /// 匹配度
        /// </summary>
        public float Score { get; set; }

        /// <summary>
        /// 文档id
        /// </summary>
        public int DocId { get; set; }

        /// <summary>
        /// 匹配到的数据高亮后的数据
        /// key为T的属性名称，value为高亮后的数据
        /// </summary>
        public Dictionary<string,string> HightLightValue { get; set; }


        public T Data { get; set; }
    }
}
