using System.Collections.Generic;

using Lucene.Net.Search;

namespace Dncy.Tools.LuceneNet
{
    public class SearchModel
    {

        public SearchModel()
        {
            EnableHighLight = true;
        }

        /// <summary>
        /// initializes a new instance of the <see cref="SearchModel"/> class.
        /// </summary>
        /// <param name="query"></param>
        /// <param name="maxHits"></param>
        public SearchModel(Query query, int maxHits):this()
        {
            Query = query;
            MaxHits = maxHits;
        }


        /// <summary>
        /// initializes a new instance of the <see cref="SearchModel"/> class.
        /// </summary>
        /// <param name="query"></param>
        /// <param name="maxHits"></param>
        /// <param name="orderBy"></param>
        public SearchModel(Query query, int maxHits, List<SortField> orderBy):this()
        {
            Query = query;
            MaxHits = maxHits;
            OrderBy = orderBy.ToArray();
        }


        /// <summary>
        /// initializes a new instance of the <see cref="SearchModel"/> class.
        /// </summary>
        /// <param name="query"></param>
        /// <param name="maxHits"></param>
        /// <param name="orderBy"></param>
        /// <param name="skip"></param>
        /// <param name="take"></param>
        public SearchModel(Query query, int maxHits, List<SortField> orderBy, int skip, int take):this()
        {
            Query = query;
            MaxHits = maxHits;
            OrderBy = orderBy.ToArray();
            Skip = skip;
            Take = take;
        }



        /// <summary>
        /// initializes a new instance of the <see cref="SearchModel"/> class.
        /// </summary>
        /// <param name="query"></param>
        /// <param name="maxHits"></param>
        /// <param name="orderBy"></param>
        /// <param name="skip"></param>
        /// <param name="take"></param>
        /// <param name="score"></param>
        public SearchModel(Query query, int maxHits, List<SortField> orderBy, int skip, int take, float score):this()
        {
            Query = query;
            MaxHits = maxHits;
            OrderBy = orderBy.ToArray();
            Skip = skip;
            Take = take;
            Score = score;
        }


        /// <summary>
        /// initializes a new instance of the <see cref="SearchModel"/> class.
        /// </summary>
        /// <param name="query"></param>
        /// <param name="maxHits"></param>
        /// <param name="orderBy"></param>
        /// <param name="skip"></param>
        /// <param name="take"></param>
        /// <param name="score"></param>
        /// <param name="onlyTyped"></param>
        public SearchModel(Query query, int maxHits, List<SortField> orderBy, int skip, int take, float score, bool onlyTyped):this()
        {
            Query = query;
            MaxHits = maxHits;
            OrderBy = orderBy.ToArray();
            Skip = skip;
            Take = take;
            Score = score;
            OnlyTyped = onlyTyped;
        }


        /// <summary>
        /// 主要查询参数
        /// </summary>
        public Query Query { get; set; }

        /// <summary>
        /// 最大检索量
        /// </summary>
        public int MaxHits { get; set; }

        /// <summary>
        /// 排序字段
        /// </summary>
        public SortField[] OrderBy { get; set; }

        /// <summary>
        /// 跳过多少条
        /// </summary>
        public int? Skip { get; set; }

        /// <summary>
        /// 取多少条
        /// </summary>
        public int? Take { get; set; }


        /// <summary>
        /// 匹配度，从0开始，数值越大结果越精确
        /// </summary>
        public float Score { get; set; } = 0f;

        /// <summary>
        /// 是否仅在当前泛型约束的类型中搜索
        /// </summary>
        public bool OnlyTyped { get; set; }

        /// <summary>
        /// 高亮标签
        /// </summary>
        /// <example>("<b style='color:red'>","</b>")</example>
        public (string preTag,string postTag) HighlightTag { get; set; }

        /// <summary>
        /// 是否启用高亮 默认true
        /// </summary>
        public bool EnableHighLight { get; set; }
    }
}

