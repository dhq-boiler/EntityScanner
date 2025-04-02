using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EntityScanner
{
    /// <summary>
    /// カスタム型をEntityScannerで処理するためのインターフェース
    /// </summary>
    public interface IEntitySerializable
    {
        /// <summary>
        /// シードデータとして使用可能な文字列表現を返す
        /// </summary>
        string ToEntityString();

        /// <summary>
        /// 文字列表現からインスタンスを生成する
        /// </summary>
        static abstract IEntitySerializable FromEntityString(string value);
    }
}
