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

            // SmartChineseAnalyzer ��ͣ�ôʿ����Զ���
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
                    Name = "����",
                    Remarks = "��ѧϰ"
                },
                new Person
                {
                    Id = 2,
                    Name = "����",
                    Remarks = "������Ϸ"
                },
                new Person
                {
                    Id = 3,
                    Name = "wangwu",
                    Remarks = "������"
                }
            };

            var engine = _service.GetService<LuceneSearchEngine>();
            var successed = engine.CreateIndex(dataList);

            var query = new TermQuery(new Term("Remarks", "����"));

            var searchResult = engine.Search<Person>(new SearchModel(query, 100)
            {
                OrderBy = new SortField[] { SortField.FIELD_SCORE }, // ������Ϣ
                Skip = 0, // ��ҳskip
                Take = 20, // ��ҳ take
                Score = 0, // Ȩ�� 0��ʼԽ���ʶԽ��ȷ
                OnlyTyped = true, // �Ƿ�ֻ��ָ���ķ������������ĵ���������false��ʶ�����ַ���typeԼ��ȫ����ѯ
                HighlightTag = ($"<a style='color:#e9851e'>", "</a>") // �������
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
        /// ȫ�ļ��������ֶ����л��ṩ��
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