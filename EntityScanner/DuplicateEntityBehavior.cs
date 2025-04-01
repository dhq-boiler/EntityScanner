using System;
using System.Collections.Generic;
using System.Text;

namespace EntityScanner
{
    /// <summary>
    /// 同じとみなされる（即ち主キーが同じ）エンティティが既に存在する場合の振る舞いを表します。
    /// </summary>
    public enum DuplicateEntityBehavior
    {
        /// <summary>
        /// 同じ主キーを持つエンティティが存在する場合に例外をスローします。
        /// </summary>
        ThrowException,

        /// <summary>
        /// 既存のエンティティを新しいエンティティの値で更新します。
        /// </summary>
        Update,

        /// <summary>
        /// 既存のエンティティを無視し、何も行いません。
        /// </summary>
        Ignore,

        /// <summary>
        /// 常に新しいエンティティを追加します（重複を許可）。
        /// </summary>
        AddAlways
    }
}
