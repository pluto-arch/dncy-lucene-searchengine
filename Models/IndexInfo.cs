using System;

namespace Dncy.Tools.LuceneNet.Models
{
    public class IndexInfo
    {
        /// <summary>
        /// 索引目录
        /// </summary>
        public string Dir { get; set; }

        /// <summary>
        /// 文档数量
        /// </summary>
        public int DocumentCount { get; set; }

        /// <summary>
        /// 创建时间
        /// </summary>
        public DateTime CreateTimeUtc { get; set; }

        /// <summary>
        /// 上次访问时间
        /// </summary>
        public DateTime LastAccessTimeUtc { get; set; }

        /// <summary>
        /// 上次写入时间
        /// </summary>
        public DateTime LastWriteTimeUtc { get; set; }

        /// <summary>
        /// 存储设备可用空闲空间大小 单位字节
        /// </summary>
        public long DriveAvailableFreeSpace { get; set; }

        /// <summary>
        /// 存储设备大小 单位字节
        /// </summary>
        public long DriveTotalSize { get; set; }

        /// <summary>
        /// 存储设备总空闲空间大小 单位字节
        /// </summary>
        public long DriveTotalFreeSpace { get; set; }

        /// <summary>
        /// 索引大小，单位字节
        /// </summary>
        public long IndexSize { get; set; }
    }
}

