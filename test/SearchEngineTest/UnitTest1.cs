using Dotnetydd.Tools.LuceneNet;
using Dotnetydd.Tools.LuceneNet.Models;
using Lucene.Net.Analysis;
using Lucene.Net.Analysis.Cn.Smart;
using Lucene.Net.Index;
using Lucene.Net.Search;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;

namespace SearchEngineTest
{
    public class Tests
    {
        private IServiceProvider _service;
        [SetUp]
        public void Setup()
        {
            var service = new ServiceCollection();
            service.AddOptions();
            service.Configure<LuceneSearchEngineOptions>(o =>
            {
                o.IndexDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "luceneIndexs");
            });

            // SmartChineseAnalyzer 的停用词可以自定义
            service.AddScoped<Analyzer>(s => new SmartChineseAnalyzer(LuceneSearchEngine.LuceneVersion));
            service.AddScoped<LuceneSearchEngine>();
            service.AddSingleton<IFieldSerializeProvider, NewtonsoftFieldSerializeProvider>();
            _service =  service.BuildServiceProvider();
        }

        [Test]
        public void Test1()
        {
            var dataList=new List<Person>
            {
                new Person
                {
                    Id = 1,
                    Name = "张三",
                    Remarks = "爱学习"
                },
                new Person
                {
                    Id = 2,
                    Name = "李四",
                    Remarks = "爱玩游戏"
                },
                new Person
                {
                    Id = 3,
                    Name = "wangwu",
                    Remarks = "爱钓鱼"
                }
            };

            var engine = _service.GetService<LuceneSearchEngine>();
            var successed = engine.CreateIndex(dataList);

            var query = new TermQuery(new Term("Remarks", "钓鱼"));

            var searchResult = engine.Search<Person>(new SearchModel(query, 100)
            {
                OrderBy = new SortField[] { SortField.FIELD_SCORE }, // 排序信息
                Skip = 0, // 分页skip
                Take = 20, // 分页 take
                Score = 0, // 权重 0开始越大标识越精确
                OnlyTyped = true, // 是否只在指定的泛型类型索引文档中搜索，false标识不区分泛型type约束全部查询
                HighlightTag = ($"<a style='color:#e9851e'>", "</a>") // 高亮标记
            });

        }


        public class Person
        {
            [LuceneIndexed("Id", true)]
            public long Id { get; set; }

            [LuceneIndexed("Name", false)]
            public string Name { get; set; }

            [LuceneIndexed("Remarks", false, IsTextField = true, IsHighLight = true, HightLightMaxNumber = 1)]
            public string Remarks { get; set; }
        }

        /// <summary>
        /// 全文检索复杂字段序列化提供者
        /// </summary>
        public class NewtonsoftFieldSerializeProvider : IFieldSerializeProvider
        {
            /// <inheritdoc />
            public string Serialize(object obj)
            {
                return JsonConvert.SerializeObject(obj);
            }

            /// <inheritdoc />
            public T? Deserialize<T>(string objStr)
            {
                return JsonConvert.DeserializeObject<T>(objStr);
            }

            /// <inheritdoc />
            public object? Deserialize(string objStr, Type type)
            {
                return JsonConvert.DeserializeObject(objStr, type);
            }

            public void Dispose()
            { }
        }
    }
}