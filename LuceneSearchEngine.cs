using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using Dncy.Tools.LuceneNet.Models;

using Lucene.Net.Analysis;
using Lucene.Net.Analysis.Cn.Smart;
using Lucene.Net.Analysis.Util;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.Queries;
using Lucene.Net.Search;
using Lucene.Net.Search.Highlight;
using Lucene.Net.Store;
using Lucene.Net.Util;

#if NETCOREAPP
using Microsoft.Extensions.Options;
#endif

using Directory = System.IO.Directory;

namespace Dncy.Tools.LuceneNet
{
    public class LuceneSearchEngine:IDisposable
    {

        private readonly LuceneSearchEngineOptions _options;
        private readonly IFieldSerializeProvider _fieldSerializeProvider;
        public const LuceneVersion LuceneVersion = Lucene.Net.Util.LuceneVersion.LUCENE_48;
        public Analyzer Analyzer { get; private set; }
        public FSDirectory IndexDirectory { get; private set; }

#if NETCOREAPP
        /// <summary>
        /// initializes a new instance of the <see cref="LuceneSearchEngine"/> class.
        /// </summary>
        /// <param name="analyzer">分析器</param>
        /// <param name="options">选项</param>
        /// <param name="fieldSerializeProvider">字段序列化工具</param>
        /// <param name="logger"></param>
        /// <exception cref="ArgumentNullException"></exception>
        public LuceneSearchEngine(
            Analyzer analyzer,
            IOptions<LuceneSearchEngineOptions> options, 
            IFieldSerializeProvider fieldSerializeProvider)
        {
            _options = options.Value ?? throw new ArgumentNullException(nameof(options));
            _fieldSerializeProvider = fieldSerializeProvider ?? throw new ArgumentNullException(nameof(fieldSerializeProvider));

            _ = _options.IndexDir ?? throw new ArgumentNullException(nameof(LuceneSearchEngineOptions.IndexDir));
            Analyzer = analyzer??throw new ArgumentNullException(nameof(analyzer));;
            IndexDirCache.Add(_options.IndexDir);
            IndexDirectory = OpenDirectory(_options.IndexDir);
        }
#else
        public LuceneSearchEngine(Analyzer analyzer,LuceneSearchEngineOptions options, IFieldSerializeProvider serializeProvider)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _ = _options.IndexDir ?? throw new ArgumentNullException(nameof(options.IndexDir));
            Analyzer = analyzer?? throw new ArgumentNullException(nameof(analyzer));
            _fieldSerializeProvider = serializeProvider;
            IndexDirCache.Add(_options.IndexDir);
            IndexDirectory = OpenDirectory(_options.IndexDir);
        }
#endif


        static readonly ConcurrentDictionary<Type, Dictionary<PropertyInfo, LuceneIndexedAttribute>> TypeFieldCache = new();
        static readonly ConcurrentBag<string> IndexDirCache = new();
        private bool disposedValue;

        /// <summary>
        /// 创建索引
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="datas"></param>
        /// <param name="recreate"></param>
        /// <returns></returns>
        public bool CreateIndex<T>(IEnumerable<T> datas, bool recreate = false)
        {
            if (IndexWriter.IsLocked(IndexDirectory))
            {
                //  若是索引目录被锁定（好比索引过程当中程序异常退出），则首先解锁
                //  Lucene.Net在写索引库以前会自动加锁，在close的时候会自动解锁
                IndexWriter.Unlock(IndexDirectory);
            }

            //Lucene的index模块主要负责索引的建立
            using var writer = new IndexWriter(IndexDirectory, new IndexWriterConfig(LuceneVersion, Analyzer));
            // 删除重建
            if (recreate)
            {
                writer.DeleteAll();
                writer.Commit();
            }
            // 遍历实体集，添加到索引库
            foreach (var entity in datas)
            {
                var doc = ToDocument(entity);
                writer.AddDocument(doc);
            }
            writer.Flush(true, true);
            return true;
        }


        /// <summary>
        /// 搜索
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="search">查询对象</param>
        /// <returns></returns>
        public SearchResultCollection<ScoredSearchResult<T>> Search<T>(SearchModel search) where T : class, new()
        {
            _ = search.MaxHits <= 0 ? throw new ArgumentOutOfRangeException(nameof(search.MaxHits)) : search.MaxHits;
            using var reader = DirectoryReader.Open(IndexDirectory);
            var searcher = new SearcherFactory().NewSearcher(reader);
            var sort = new Sort(search.OrderBy.ToArray());
            Expression<Func<ScoreDoc, bool>> where = m => m.Score >= search.Score;
            Filter filter = null;
            if (search.OnlyTyped)
            {
                filter = new BooleanFilter { { new QueryWrapperFilter(new TermQuery(new Term("Type", typeof(T).AssemblyQualifiedName))), Occur.MUST } };
            }
            var matches = searcher.Search(search.Query, filter, search.MaxHits, sort, true, true);
            var resultSet = new SearchResultCollection<ScoredSearchResult<T>>();
            if (matches.TotalHits <= 0)
            {
                return resultSet;
            }

            resultSet.TotalHits = matches.TotalHits;

            var hits = matches.ScoreDocs.Where(where.Compile());
            if (search.Skip.HasValue)
            {
                hits = hits.Skip(search.Skip.Value);
            }
            if (search.Take.HasValue)
            {
                hits = hits.Take(search.Take.Value);
            }
            var docs = hits.ToList();
            var scorer = new QueryScorer(search.Query);
            Highlighter highlighter = null;
            highlighter = new Highlighter(new SimpleHTMLFormatter(search.HighlightTag.preTag, search.HighlightTag.postTag), scorer)
            {
                TextFragmenter = new SimpleFragmenter()
            };
            foreach (var match in docs)
            {
                var doc = searcher.Doc(match.Doc);
                var entity = GetSearchRsultFromDocument<T>(doc, highlighter);
                entity.DocId = match.Doc;
                entity.Score = match.Score;
                resultSet.Results.Add(entity);
            }
            return resultSet;
        }


        /// <summary>
        /// 根据标识删除
        /// </summary>
        /// <param name="idFieldName">数据id字段名称</param>
        /// <param name="dataId">数据id值</param>
        /// <returns></returns>
        public bool DeleteDocumentByDataId(string idFieldName, string dataId)
        {
            if (IndexWriter.IsLocked(IndexDirectory))
            {
                //  若是索引目录被锁定（好比索引过程当中程序异常退出），则首先解锁
                //  Lucene.Net在写索引库以前会自动加锁，在close的时候会自动解锁
                IndexWriter.Unlock(IndexDirectory);
            }
            using var writer = new IndexWriter(IndexDirectory, new IndexWriterConfig(LuceneVersion, Analyzer));
            writer.DeleteDocuments(new TermQuery(new Term(idFieldName, dataId)));
            writer.Flush(true, true);
            writer.Commit();
            return true;
        }


        /// <summary>
        /// 根据标识删除
        /// </summary>
        /// <param name="idFieldName">数据id字段名称</param>
        /// <param name="dataIds">数据id集合</param>
        /// <returns></returns>
        public bool DeleteDocumentsByDataId(string idFieldName, IEnumerable<string> dataIds)
        {
            if (IndexWriter.IsLocked(IndexDirectory))
            {
                //  若是索引目录被锁定（好比索引过程当中程序异常退出），则首先解锁
                //  Lucene.Net在写索引库以前会自动加锁，在close的时候会自动解锁
                IndexWriter.Unlock(IndexDirectory);
            }
            using var writer = new IndexWriter(IndexDirectory, new IndexWriterConfig(LuceneVersion, Analyzer));
            try
            {
                foreach (var entity in dataIds)
                {
                    if (string.IsNullOrEmpty(entity))
                    {
                        continue;
                    }
                    writer.DeleteDocuments(new Term(idFieldName, entity));
                }
                writer.Flush(true, true);
                writer.Commit();
            }
            finally
            {
                writer.Rollback();
            }
            return true;
        }

        /// <summary>
        /// 根据标识更新数据
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="idFieldName"></param>
        /// <param name="dataId"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        public bool UpdateByDataId<T>(string idFieldName, string dataId,T data)
        {
            _ = idFieldName ?? throw new ArgumentNullException(nameof(idFieldName));
            DeleteDocumentByDataId(idFieldName,dataId);
            var doc = ToDocument(data);
            if (IndexWriter.IsLocked(IndexDirectory))
            {
                IndexWriter.Unlock(IndexDirectory);
            }
            using var writer = new IndexWriter(IndexDirectory, new IndexWriterConfig(LuceneVersion, Analyzer));
            writer.AddDocument(doc);
            writer.Flush(true, true);
            return true;
        }


        /// <summary>
        /// 根据标识更新数据
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="datas"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        public bool UpdateByDataId<T>(List<UpdateModel<T>> datas)
        {
            if (IndexWriter.IsLocked(IndexDirectory))
            {
                IndexWriter.Unlock(IndexDirectory);
            }
            using var writer = new IndexWriter(IndexDirectory, new IndexWriterConfig(LuceneVersion, Analyzer));
            try
            {
                foreach (var item in datas)
                {
                    if (string.IsNullOrEmpty(item.IdFieldName)||string.IsNullOrEmpty(item.DataId))
                    {
                        continue;
                    }
                    DeleteDocumentByDataId(item.IdFieldName,item.DataId);
                    var doc = ToDocument(item);
                    writer.AddDocument(doc);
                }
                writer.Flush(true, true);
                return true;
            }
            finally
            {
                writer.Rollback();
            }
        }


        /// <summary>
        /// 当前索引信息
        /// </summary>
        /// <returns></returns>
        public IndexInfo CurrentIndexInfo()
        {
            var size = (from strFile in IndexDirectory.ListAll() select IndexDirectory.FileLength(strFile)).Sum();
            using IndexReader reader = DirectoryReader.Open(IndexDirectory);
            var di = new System.IO.DriveInfo(_options.IndexDir);
            var model = new IndexInfo
            {
                Dir = _options.IndexDir,
                DocumentCount = reader.NumDocs,
                LastAccessTimeUtc = IndexDirectory.Directory.LastAccessTimeUtc,
                CreateTimeUtc = IndexDirectory.Directory.CreationTimeUtc,
                LastWriteTimeUtc = IndexDirectory.Directory.LastWriteTimeUtc,
                DriveTotalFreeSpace = di.TotalFreeSpace,
                DriveTotalSize = di.TotalSize,
                DriveAvailableFreeSpace = di.AvailableFreeSpace,
                IndexSize = size
            };
            return model;
        }


        /// <summary>
        /// 获取索引信息
        /// </summary>
        /// <returns></returns>
        public List<IndexInfo> IndexInfos()
        {
            var res = new List<IndexInfo>();
            foreach (var dir in IndexDirCache)
            {

                var di = new System.IO.DriveInfo(_options.IndexDir);
                var model = new IndexInfo
                {
                    Dir = dir,
                    DriveTotalFreeSpace = di.TotalFreeSpace,
                    DriveTotalSize = di.TotalSize,
                    DriveAvailableFreeSpace = di.AvailableFreeSpace,

                };
                var directory = OpenDirectory(dir);
                if (directory == null)
                {
                    continue;
                }
                var size = (from strFile in directory.ListAll() select directory.FileLength(strFile)).Sum();
                model.IndexSize = size;
                model.LastAccessTimeUtc = directory.Directory.LastAccessTimeUtc;
                model.CreateTimeUtc = directory.Directory.CreationTimeUtc;
                using IndexReader reader = DirectoryReader.Open(directory);
                model.DocumentCount = reader.NumDocs;
                res.Add(model);
            }
            return res;
        }



        /// <summary>
        /// 切换索引目录
        /// </summary>
        /// <returns></returns>
        public IDisposable ChangeIndexDir(string dir)
        {
            if (!Directory.Exists(dir))
            {
                throw new DirectoryNotFoundException($"索引目录不存在：{dir}");
            }

            var newIdxDir = OpenDirectory(dir);
            var parentScope = IndexDirectory;
            IndexDirectory = newIdxDir;
            IndexDirCache.Add(dir);
            return new DisposeAction(() =>
            {
                IndexDirectory = parentScope;
                newIdxDir?.Dispose();
            });
        }


        /// <summary>
        /// 切换分析器
        /// </summary>
        /// <returns></returns>
        public IDisposable ChangeAnalyzer(Analyzer analyzer)
        {
            if (analyzer == null)
            {
                throw new ArgumentNullException(nameof(analyzer));
            }
            var parentScope = Analyzer;
            Analyzer = analyzer;
            return new DisposeAction(() =>
            {
                Analyzer = parentScope;
                analyzer?.Dispose();
            });
        }


        /// <summary>
        /// 转化未document模型
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="data"></param>
        /// <returns></returns>
        public Document ToDocument<T>(T data)
        {
            var doc = new Document();
            var type = typeof(T);

            doc.Add(new StringField("Type", type.AssemblyQualifiedName, Field.Store.YES));

            var propertys = TypeFieldCache.GetOrAdd(type, m =>
             {
                 var properties = m.GetProperties();
                 var dic = new Dictionary<PropertyInfo, LuceneIndexedAttribute>();
                 foreach (var property in properties)
                 {
                     var attr = property.GetCustomAttribute<LuceneIndexedAttribute>();
                     if (attr == null)
                     {
                         continue;
                     }
                     dic.Add(property, attr);
                 }
                 return dic;
             });

            if (propertys != null && propertys.Any())
            {
                foreach (var item in propertys)
                {
                    var property = item.Key;
                    var attr = item.Value;
                    var value = property.GetValue(data);
                    if (value == null)
                    {
                        continue;
                    }
                    string name = !string.IsNullOrEmpty(attr.Name) ? attr.Name : property.Name;
                    var field = GetField(value, name, attr);
                    doc.Add(field);
                }

                return doc;
            }

            var classProperties = type.GetProperties();
            foreach (PropertyInfo propertyInfo in classProperties)
            {
                if (propertyInfo.GetCustomAttributes<LuceneIndexedAttribute>().FirstOrDefault() is not LuceneIndexedAttribute attr)
                {
                    continue;
                }
                TypeFieldCache.AddOrUpdate(type, new Dictionary<PropertyInfo, LuceneIndexedAttribute> { { propertyInfo, attr } }, (_, _) => new Dictionary<PropertyInfo, LuceneIndexedAttribute> { { propertyInfo, attr } });
                var value = propertyInfo.GetValue(data);
                if (value == null)
                {
                    continue;
                }
                string name = !string.IsNullOrEmpty(attr.Name) ? attr.Name : propertyInfo.Name;
                var field = GetField(value, name, attr);
                doc.Add(field);
            }
            return doc;
        }

        /// <summary>
        /// 分词
        /// </summary>
        /// <param name="searchText"></param>
        /// <param name="stopWords">自定义停止词</param>
        /// <param name="withDefaultStopWord">是否使用默认停止词</param>
        /// <returns></returns>
        public static List<string> GetKeyWords(string searchText, bool withDefaultStopWord=false,List<string> stopWords = null)
        {
            var keyworkds = new List<string>();
            CharArraySet stopwords = null;
            var arrat = new List<string>();
            if (stopWords!=null&&stopWords.Any())
            {
                arrat.AddRange(new CharArraySet(LuceneSearchEngine.LuceneVersion, stopWords, true).ToArray());
            }

            if (withDefaultStopWord)
            {
                var defaultStop = CharArraySet.UnmodifiableSet(
                    WordlistLoader.GetWordSet(IOUtils.GetDecodingReader(typeof(SmartChineseAnalyzer), "stopwords.txt", Encoding.UTF8), "//", LuceneSearchEngine.LuceneVersion));
                arrat.AddRange(defaultStop.ToArray());
            }

            if (arrat.Any())
            {
                stopwords = new CharArraySet(LuceneSearchEngine.LuceneVersion, arrat, true);
            }

            var analyzer = new SmartChineseAnalyzer(LuceneVersion.LUCENE_48,stopwords);
            using var ts = analyzer.GetTokenStream(null, searchText);
            ts.Reset();
            var ct = ts.GetAttribute<Lucene.Net.Analysis.TokenAttributes.ICharTermAttribute>();
            while (ts.IncrementToken())
            {
                StringBuilder keyword = new StringBuilder();
                for (int i = 0; i < ct.Length; i++)
                {
                    keyword.Append(ct.Buffer[i]);
                }
                string item = keyword.ToString();
                if (!keyworkds.Contains(item))
                {
                    keyworkds.Add(item);
                }
            }

            return keyworkds;
        }



        #region private members

        private Field GetField(object value, string name, LuceneIndexedAttribute attr)
        {
            if (attr.IsIdentityField)
            {
                return new StringField(name, value.ToString(), attr.Store);
            }
            switch (value)
            {
                case DateTime time:
                    return new StringField(name, time.ToString("yyyy-MM-dd HH:mm:ss"), attr.Store);
                case int num:
                    return new Int32Field(name, num, attr.Store);
                case uint num:
                    return new Int32Field(name, (int)num, attr.Store);
                case long num:
                    return new Int64Field(name, num, attr.Store);
                case ulong num:
                    return new Int64Field(name, (long)num, attr.Store);
                case float num:
                    return new SingleField(name, num, attr.Store);
                case short num:
                    return new SingleField(name, num, attr.Store);
                case double num:
                    return new DoubleField(name, num, attr.Store);
                case decimal num:
                    return new DoubleField(name, (double)num, attr.Store);
                case string _:
                    var htmlValue = attr.IsHtml ? value.ToString().RemoveHtmlTag() : value.ToString();
                    if (attr.IsTextField)
                    {
                        return new TextField(name, htmlValue, attr.Store);
                    }
                    else
                    {
                        return new StringField(name, htmlValue, attr.Store);
                    }
                default:
                    var serValue = _fieldSerializeProvider.Serialize(value);
                    return new StringField(name, serValue, attr.Store);
            }
        }

        private FSDirectory OpenDirectory(string dir = null)
        {
            dir ??= _options.IndexDir;
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            var directory = FSDirectory.Open(new DirectoryInfo(dir));
            return directory;
        }

        private ScoredSearchResult<T> GetSearchRsultFromDocument<T>(Document doc, Highlighter highlighter = null) where T : class, new()
        {
            var t = typeof(T);
            var type = typeof(ScoredSearchResult<>).MakeGenericType(t);
            var obj = (ScoredSearchResult<T>)Activator.CreateInstance(type);
            var dataInstance = Activator.CreateInstance(t);
            var dic = new Dictionary<string, string>();
            if (TypeFieldCache.ContainsKey(t))
            {
                var properties = TypeFieldCache[t];
                foreach (var p in properties)
                {
                    p.Key.SetValue(dataInstance, doc.Get(p.Key.Name, p.Key.PropertyType, _fieldSerializeProvider));
                    var attr = p.Value;
                    if (attr.IsHighLight)
                    {
                        var value = doc.GetField(p.Key.Name).GetStringValue();
                        value = GetHightLightPreviewStr(value, p.Key.Name, attr.HightLightMaxNumber, highlighter);
                        dic.Add(p.Key.Name, value);
                    }
                }
            }
            else
            {
                var propDir = new Dictionary<PropertyInfo, LuceneIndexedAttribute>();
                foreach (var p in t.GetProperties().Where(p => p.GetCustomAttributes<LuceneIndexedAttribute>().Any()))
                {
                    p.SetValue(dataInstance, doc.Get(p.Name, p.PropertyType, _fieldSerializeProvider));
                    var attr = p.GetCustomAttribute<LuceneIndexedAttribute>();
                    if (attr == null)
                    {
                        continue;
                    }
                    if (attr.IsHighLight)
                    {
                        var value = doc.GetField(p.Name).GetStringValue();
                        value = GetHightLightPreviewStr(value, p.Name, attr.HightLightMaxNumber, highlighter);
                        dic.Add(p.Name, value);
                    }
                    propDir.Add(p, attr);
                }
                TypeFieldCache.AddOrUpdate(t, propDir, (_, _) => propDir);
            }

            obj.Data = (T)dataInstance;
            obj.HightLightValue = dic;
            return obj;
        }

        private string GetHightLightPreviewStr(string value, string field, int maxNumFragments, Highlighter highlighter)
        {
            if (highlighter==null)
            {
                return value;
            }
            using var stream = Analyzer.GetTokenStream(field, new StringReader(value));
            var previews = highlighter.GetBestFragments(stream, value, maxNumFragments);
            var preview = string.Join("\n", previews.Select(html => html.Trim()));
            return preview;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    Analyzer?.Dispose();
                    IndexDirectory?.Dispose();
                    _fieldSerializeProvider?.Dispose();
                }
                disposedValue = true;
            }
        }

        // // TODO: 仅当“Dispose(bool disposing)”拥有用于释放未托管资源的代码时才替代终结器
        // ~LuceneSearchEngine()
        // {
        //     // 不要更改此代码。请将清理代码放入“Dispose(bool disposing)”方法中
        //     Dispose(disposing: false);
        // }

        public void Dispose()
        {
            // 不要更改此代码。请将清理代码放入“Dispose(bool disposing)”方法中
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
        #endregion

    }
}