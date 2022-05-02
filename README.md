## 基于lucene.net的全文检索工具

> 基于lucene.net 实现的一款全文检索工具，支持结果短语查询，中英文分词查询，关键字查询，也可以自行安装符合Lucene.Net的分析器以及分词工具。本项目最低支持net framework4.5.1 目前大多数项目中可以引入。索引数据的操作需要自行实现，比如使用ef/efcore的change tracking 、savechange拦截器、领域事件等方式实现索引的操作与项目中数据的同步。

> 注意：该工具仅适用于简单搜索场景，不适用于分布式应用以及复杂的场景，专业项目请考虑使用大型的搜索引擎中间件做支撑，如：ElasticSearch，或考虑数据库全文检索，或数据库正则。
> 注意：对于复杂字段的索引，目前只是进行序列化方式，需要自行实现 `IFieldSerializeProvider` 接口以实现字段的序列化，并且字段的特性`LuceneIndexed` 中 `IsSerializeStore` 要设置成true
> 注意：请数据自定标识字段，并且字段的特性`LuceneIndexed` 中 `IsIdentityField` 要设置成true
> 注意：需要指定版本的地方请使用`LuceneSearchEngine.LuceneVersion`

### 使用方式

1. net framework 
```csharp
// net framework 中直接new就可以。或者使用依赖注入框架。
using var searchEngine = new LuceneSearchEngine(new SmartChineseAnalyzer(LuceneSearchEngine.LuceneVersion),new LuceneSearchEngineOptions
{
    IndexDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,  "luceneIndexs"),
},new NewtonsoftMessageSerializeProvider());



```

2. netcore/net5/net6中使用

```csharp
builder.Services.Configure<LuceneSearchEngineOptions>(o =>
{
    o.IndexDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "luceneIndexs");
});
// SmartChineseAnalyzer 的停用词可以自定义
builder.Services.AddScoped<Analyzer>(s => new SmartChineseAnalyzer(LuceneSearchEngine.LuceneVersion));
builder.Services.AddScoped<LuceneSearchEngine>();

// other service
public class Demo
{
    private readonly LuceneSearchEngine _luceneSearchEngine;
	public Demo(LuceneSearchEngine luceneSearchEngine)
	{
		_luceneSearchEngine = luceneSearchEngine;
	}
}
```

### 自定义停止词

```csharp
var defaultStop= CharArraySet.UnmodifiableSet(WordlistLoader.GetWordSet(IOUtils.GetDecodingReader(typeof(SmartChineseAnalyzer), "stopwords.txt", Encoding.UTF8), "//", LuceneSearchEngine.LuceneVersion));
var stopwords = new CharArraySet(LuceneSearchEngine.LuceneVersion, new string[] {"的", "sb"}, true);
```



### 从数据集合创建索引

```csharp
// demo model
public class Person
{
    [LuceneIndexed("Id", true)]
    public long Id { get; set; }

    [LuceneIndexed("Name", false)]
    public string Name { get; set; }

    [LuceneIndexed("Remarks", false, IsTextField = true, IsHighLight = true, HightLightMaxNumber = 1)]
    public string Remarks { get; set; }
}

var dataList=new List<Person>();

var successed = _luceneSearchEngine.CreateIndex(dataList);
```


### 搜索
- 构建query
1. 直接构建：
```csharp
var query = new TermQuery(new Term("Remarks", "搜索"));

var query = new PhraseQuery(new Term("Remarks", "搜索"));

var query = new BooleanQuery()
queryd.Add(new TermQuery(new Term(nameof(Person.Remarks), keyWords)), Occur.MUST);
```

2. 通过QueryParser和MultiFieldQueryParser构建
```csharp
 var parser = new QueryParser(LuceneVersion.LUCENE_48, nameof(Person.Remarks), _luceneSearchEngine.Analyzer);
 var query = parser.Parse(keyWords);

 var ab = new MultiFieldQueryParser(LuceneSearchEngine.LuceneVersion, new[] { nameof(Person.Name), nameof(Person.Remarks) }, _luceneSearchEngine.Analyzer);
 var query = ab.Parse(keyWords);
```

3. 通过QueryBuilder 构建  [不支持多条件]
```csharp
var builder = new QueryBuilder();
 var query = builder.BuildQuery(keyWords);
```

- 执行查询
```csharp
 var searchResult = _luceneSearchEngine.Search<Person>(new SearchModel(query, 100)
            {
                OrderBy = new SortField[] { SortField.FIELD_SCORE }, // 排序信息
                Skip = 0, // 分页skip
                Take = 20, // 分页 take
                Score = 0, // 权重 0开始越大标识越精确
                OnlyTyped = true, // 是否只在指定的泛型类型索引文档中搜索，false标识不区分泛型type约束全部查询
                HighlightTag = ($"<a style='color:{highlight}'>", "</a>") // 高亮标记
            });
```