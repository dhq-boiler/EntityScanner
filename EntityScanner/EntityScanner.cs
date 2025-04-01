﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Reflection;
using Microsoft.EntityFrameworkCore;

namespace EntityScanner;

/// <summary>
///     Entity Framework Core向けのシードデータを階層的に管理するユーティリティクラス。
///     リレーショナルデータの外部キーを自動的に設定します。
/// </summary>
/// <remarks>
///     .NET Standard 2.0に対応しており、.NET Core 3.1+、.NET 5+、.NET Framework 4.6.1+で利用可能です。
/// </remarks>
public class EntityScanner
{
    private readonly Dictionary<Type, IList<object>> _entities = new();
    private readonly HashSet<object> _processedEntities = new();

    /// <summary>
    ///     エンティティを登録します。ナビゲーションプロパティも再帰的に処理されます。
    /// </summary>
    /// <typeparam name="T">エンティティの型</typeparam>
    /// <param name="entity">エンティティのインスタンス</param>
    /// <returns>登録されたエンティティ</returns>
    public T RegisterEntity<T>(T entity) where T : class
    {
        if (entity == null)
        {
            throw new ArgumentNullException(nameof(entity));
        }

        var type = typeof(T);
        if (!_entities.ContainsKey(type))
        {
            _entities[type] = new List<object>();
        }

        // 同じエンティティが登録されないようにする
        if (!_entities[type].Contains(entity))
        {
            _entities[type].Add(entity);
        }

        // ナビゲーションプロパティを再帰的に走査する
        ScanNavigationProperties(entity);

        return entity;
    }

    /// <summary>
    ///     複数のエンティティをまとめて登録します。
    /// </summary>
    /// <typeparam name="T">エンティティの型</typeparam>
    /// <param name="entities">エンティティのコレクション</param>
    /// <returns>登録されたエンティティのコレクション</returns>
    public IEnumerable<T> RegisterEntities<T>(IEnumerable<T> entities) where T : class
    {
        if (entities == null)
        {
            throw new ArgumentNullException(nameof(entities));
        }

        var result = new List<T>();
        foreach (var entity in entities)
        {
            result.Add(RegisterEntity(entity));
        }

        return result;
    }

    /// <summary>
    ///     指定した型のエンティティをすべて取得します。
    /// </summary>
    /// <typeparam name="T">エンティティの型</typeparam>
    /// <returns>登録されたエンティティのコレクション</returns>
    public IEnumerable<T> GetEntities<T>() where T : class
    {
        var type = typeof(T);
        if (!_entities.ContainsKey(type))
        {
            return Enumerable.Empty<T>();
        }

        return _entities[type].Cast<T>();
    }

    /// <summary>
    ///     HasData用の匿名オブジェクトを生成します。ナビゲーションプロパティを除外し、
    ///     主キーと外部キーのみを含む新しいオブジェクトを作成します。
    /// </summary>
    /// <typeparam name="T">エンティティの型</typeparam>
    /// <returns>HasDataで使用可能なオブジェクトのコレクション</returns>
    public IEnumerable<object> GetSeedData<T>() where T : class
    {
        var entities = GetEntities<T>();
        var result = new List<object>();

        foreach (var entity in entities)
        {
            // 基本プロパティのみを含む匿名オブジェクトを作成
            var properties = typeof(T).GetProperties()
                .Where(p => IsBasicType(p.PropertyType))
                .ToDictionary(p => p.Name, p => p.GetValue(entity));

            result.Add(properties);
        }

        return result;
    }

    /// <summary>
    ///     登録されているすべてのエンティティをDbContextに追加します。
    /// </summary>
    /// <param name="context">DbContext</param>
    public void ApplyToContext(DbContext context)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        // エンティティの型ごとに処理
        foreach (var kvp in _entities)
        {
            var entityType = kvp.Key;
            var entities = kvp.Value;

            // DbSetプロパティを取得
            var dbSetProperty = context.GetType().GetProperties()
                .FirstOrDefault(p => p.PropertyType.IsGenericType &&
                                     p.PropertyType.GetGenericTypeDefinition() == typeof(DbSet<>) &&
                                     p.PropertyType.GetGenericArguments()[0] == entityType);

            if (dbSetProperty != null)
            {
                // DbSetを取得
                dynamic dbSet = dbSetProperty.GetValue(context);

                // エンティティをDbSetに追加
                foreach (var entity in entities)
                {
                    dbSet.Add((dynamic)entity);
                }
            }
        }
    }

    /// <summary>
    ///     ナビゲーションプロパティを再帰的に走査し、関連する外部キープロパティを自動的に設定します。
    /// </summary>
    /// <param name="entity">走査するエンティティ</param>
    private void ScanNavigationProperties(object entity)
    {
        if (entity == null || _processedEntities.Contains(entity))
        {
            return;
        }

        // 循環参照防止のために処理済みエンティティを記録
        _processedEntities.Add(entity);

        var entityType = entity.GetType();

        // プロパティを取得
        var properties = entityType.GetProperties(BindingFlags.Public | BindingFlags.Instance);

        foreach (var property in properties)
        {
            // 単一のナビゲーションプロパティ（参照ナビゲーション）
            if (IsReferenceNavigationProperty(property))
            {
                var value = property.GetValue(entity);
                if (value != null)
                {
                    // 外部キープロパティの自動設定
                    SetForeignKeyProperty(entity, property, value);

                    // ナビゲーションプロパティの値を登録
                    var method = GetType().GetMethod(nameof(RegisterEntity))
                        .MakeGenericMethod(property.PropertyType);
                    method.Invoke(this, new[] { value });
                }
            }
            // コレクションナビゲーションプロパティ
            else if (IsCollectionNavigationProperty(property))
            {
                var collection = property.GetValue(entity) as IEnumerable;
                if (collection != null)
                {
                    var elementType = GetCollectionElementType(property.PropertyType);
                    if (elementType != null)
                    {
                        // コレクションの各要素の外部キープロパティを自動設定
                        foreach (var item in collection)
                        {
                            if (item != null)
                            {
                                // 逆方向のナビゲーションプロパティを検索して、外部キープロパティを設定
                                SetInverseNavigationForeignKey(item, entity);
                            }
                        }

                        // コレクションの各要素を登録
                        var method = GetType().GetMethod(nameof(RegisterEntities))
                            .MakeGenericMethod(elementType);
                        method.Invoke(this, new[] { collection });
                    }
                }
            }
        }
    }

    /// <summary>
    ///     外部キープロパティを自動的に設定します。
    /// </summary>
    /// <param name="entity">対象のエンティティ</param>
    /// <param name="navigationProperty">ナビゲーションプロパティ</param>
    /// <param name="navigationValue">ナビゲーションプロパティの値</param>
    private void SetForeignKeyProperty(object entity, PropertyInfo navigationProperty, object navigationValue)
    {
        var entityType = entity.GetType();
        var navValueType = navigationValue.GetType();

        // 一般的な外部キー命名パターン
        var possibleFkNames = new[]
        {
            $"{navigationProperty.Name}Id",
            $"{navValueType.Name}Id"
        };

        foreach (var fkName in possibleFkNames)
        {
            var fkProperty = entityType.GetProperty(fkName);
            if (fkProperty != null && IsValidForeignKeyProperty(fkProperty))
            {
                // 主キーの値を取得
                var pkProperty = FindPrimaryKeyProperty(navigationValue);
                if (pkProperty != null)
                {
                    var pkValue = pkProperty.GetValue(navigationValue);

                    // 外部キーに主キーの値を設定
                    fkProperty.SetValue(entity, pkValue);
                    break;
                }
            }
        }
    }

    /// <summary>
    ///     コレクションナビゲーションの逆方向ナビゲーションプロパティの外部キーを設定します。
    /// </summary>
    /// <param name="childEntity">子エンティティ</param>
    /// <param name="parentEntity">親エンティティ</param>
    private void SetInverseNavigationForeignKey(object childEntity, object parentEntity)
    {
        var childType = childEntity.GetType();
        var parentType = parentEntity.GetType();

        // 親エンティティへの参照ナビゲーションプロパティを探す
        var parentNavProperties = childType.GetProperties()
            .Where(p => IsReferenceNavigationProperty(p) && p.PropertyType == parentType)
            .ToList();

        foreach (var navProperty in parentNavProperties)
        {
            // 親エンティティへの参照を設定
            navProperty.SetValue(childEntity, parentEntity);

            // 外部キーを設定
            SetForeignKeyProperty(childEntity, navProperty, parentEntity);
        }

        // 親エンティティへの参照がなければ、外部キーのみを探して設定
        if (!parentNavProperties.Any())
        {
            var parentName = parentType.Name;
            var fkProperty = childType.GetProperty($"{parentName}Id");

            if (fkProperty != null && IsValidForeignKeyProperty(fkProperty))
            {
                var pkProperty = FindPrimaryKeyProperty(parentEntity);
                if (pkProperty != null)
                {
                    fkProperty.SetValue(childEntity, pkProperty.GetValue(parentEntity));
                }
            }
        }
    }

    /// <summary>
    ///     プロパティが参照ナビゲーションプロパティかどうかを判定します。
    /// </summary>
    private bool IsReferenceNavigationProperty(PropertyInfo property)
    {
        if (property == null)
        {
            return false;
        }

        // 基本型でなく、コレクションでもない場合は参照ナビゲーションプロパティと見なす
        return !IsBasicType(property.PropertyType) &&
               (!typeof(IEnumerable).IsAssignableFrom(property.PropertyType) ||
                property.PropertyType == typeof(string));
    }

    /// <summary>
    ///     プロパティがコレクションナビゲーションプロパティかどうかを判定します。
    /// </summary>
    private bool IsCollectionNavigationProperty(PropertyInfo property)
    {
        if (property == null)
        {
            return false;
        }

        // IEnumerableを実装していて、文字列でない場合はコレクションナビゲーションプロパティと見なす
        return typeof(IEnumerable).IsAssignableFrom(property.PropertyType) &&
               property.PropertyType != typeof(string);
    }

    /// <summary>
    ///     コレクション型の要素の型を取得します。
    /// </summary>
    private Type GetCollectionElementType(Type collectionType)
    {
        if (collectionType == null)
        {
            return null;
        }

        // IEnumerable<T>のTを取得
        if (collectionType.IsGenericType &&
            collectionType.GetGenericTypeDefinition() == typeof(IEnumerable<>))
        {
            return collectionType.GetGenericArguments()[0];
        }

        // ICollection<T>、List<T>などの場合
        foreach (var interfaceType in collectionType.GetInterfaces())
        {
            if (interfaceType.IsGenericType &&
                interfaceType.GetGenericTypeDefinition() == typeof(IEnumerable<>))
            {
                return interfaceType.GetGenericArguments()[0];
            }
        }

        return null;
    }

    /// <summary>
    ///     基本型かどうかを判定します。
    /// </summary>
    private bool IsBasicType(Type type)
    {
        if (type == null)
        {
            return false;
        }

        return type.IsPrimitive ||
               type == typeof(string) ||
               type == typeof(decimal) ||
               type == typeof(DateTime) ||
               type == typeof(DateTimeOffset) ||
               type == typeof(TimeSpan) ||
               type == typeof(Guid) ||
               type.IsEnum ||
               Nullable.GetUnderlyingType(type) != null;
    }

    /// <summary>
    ///     プロパティが有効な外部キープロパティかどうかを判定します。
    /// </summary>
    private bool IsValidForeignKeyProperty(PropertyInfo property)
    {
        if (property == null)
        {
            return false;
        }

        var type = property.PropertyType;

        // 一般的な主キーの型
        return type == typeof(int) ||
               type == typeof(long) ||
               type == typeof(Guid) ||
               type == typeof(string) ||
               Nullable.GetUnderlyingType(type) == typeof(int) ||
               Nullable.GetUnderlyingType(type) == typeof(long) ||
               Nullable.GetUnderlyingType(type) == typeof(Guid);
    }

    /// <summary>
    ///     エンティティの主キープロパティを探します。
    /// </summary>
    private PropertyInfo FindPrimaryKeyProperty(object entity)
    {
        if (entity == null)
        {
            return null;
        }

        var type = entity.GetType();

        // 一般的な主キープロパティの名前パターン
        var possiblePkNames = new[]
        {
            "Id",
            $"{type.Name}Id"
        };

        foreach (var pkName in possiblePkNames)
        {
            var pkProperty = type.GetProperty(pkName);
            if (pkProperty != null && IsValidForeignKeyProperty(pkProperty))
            {
                return pkProperty;
            }
        }

        return null;
    }

    /// <summary>
    ///     キーと値のペアから匿名オブジェクトを動的に作成します。
    /// </summary>
    /// <param name="properties">プロパティ名と値の辞書</param>
    /// <returns>匿名オブジェクト</returns>
    private object CreateAnonymousObject(Dictionary<string, object> properties)
    {
        // ExpandoObjectを使用して動的オブジェクトを作成
        var expando = new ExpandoObject() as IDictionary<string, object>;
        foreach (var kvp in properties)
        {
            expando[kvp.Key] = kvp.Value;
        }

        return expando;
    }

    /// <summary>
    ///     すべての登録済みエンティティをクリアします。
    /// </summary>
    public void Clear()
    {
        _entities.Clear();
        _processedEntities.Clear();
    }
}