using System;
using Lucene.Net.Documents;

namespace Dncy.Tools.LuceneNet
{
    [AttributeUsage(AttributeTargets.Property)]
    public class LuceneIndexedAttribute:Attribute
    {

        /// <summary>
        /// initializes a new instance of the <see cref="LuceneIndexedAttribute"/> class.
        /// </summary>
        /// <param name="name">名称</param>
        /// <param name="isIdentityField">是否为标识字段</param>
        /// <param name="serialize">复杂对象是否进行序列化存储</param>
        public LuceneIndexedAttribute(string name, bool isIdentityField, bool serialize=false)
        {
            Name = name;
            Store = Field.Store.YES;
            IsHtml = false;
            IsSerializeStore = serialize;
            IsIdentityField = isIdentityField;
        }


        /// <summary>
        /// 索引字段名  默认字段名
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 是否为标识字段
        /// </summary>
        public bool IsIdentityField { get; set; }

        /// <summary>
        /// 是否被存储到索引库
        /// </summary>
        public Field.Store Store { get; set; }

        /// <summary>
        /// 是否是html
        /// </summary>
        public bool IsHtml { get; set; }

        /// <summary>
        /// 是否时textfield
        /// </summary>
        public bool IsTextField { get; set; }

        /// <summary>
        /// 是否高亮结果
        /// </summary>
        public bool IsHighLight { get; set; }

        /// <summary>
        /// 查询匹配的数量 - 用于高亮. 默认1
        /// 放置影响性能，请适当设置，推荐1-5之间
        /// </summary>
        public int HightLightMaxNumber { get; set; } = 1;

        /// <summary>
        /// 是否需要序列化后存储
        /// 复杂对象
        /// </summary>
        public bool IsSerializeStore { get; set; }
    }
}

