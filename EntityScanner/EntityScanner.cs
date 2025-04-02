using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
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

    public DuplicateEntityBehavior DuplicateBehavior { get; set; } = DuplicateEntityBehavior.ThrowException;

    public EntityScanner(DuplicateEntityBehavior behavior = DuplicateEntityBehavior.ThrowException)
    {
        DuplicateBehavior = behavior;
    }

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

    public IEnumerable<TEntity> GetSeedEntities<TEntity>() where TEntity : class
    {
        var entities = GetEntities<TEntity>().ToList();
        var result = new List<TEntity>();

        Debug.WriteLine($"GetSeedEntities: エンティティ数 = {entities.Count}");

        foreach (var entity in entities)
        {
            // ナビゲーションプロパティを含まない新しいインスタンスを作成
            var clone = Activator.CreateInstance<TEntity>();
            Debug.WriteLine($"新しいインスタンスを作成: {typeof(TEntity).Name}");

            // 基本プロパティとFKプロパティをコピー
            foreach (var prop in typeof(TEntity).GetProperties())
            {
                if (prop.CanWrite)
                {
                    if (IsBasicType(prop.PropertyType) || prop.Name.EndsWith("Id"))
                    {
                        var value = prop.GetValue(entity);
                        prop.SetValue(clone, value);
                        Debug.WriteLine($"  プロパティをコピー: {prop.Name} = {value}");
                    }
                    else
                    {
                        Debug.WriteLine($"  スキップしたプロパティ: {prop.Name} (ナビゲーションプロパティ)");
                    }
                }
            }

            result.Add(clone);
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

        // 追跡されているエンティティを記録するセット
        var trackedEntities = new HashSet<object>();

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

                foreach (var entity in entities)
                {
                    try
                    {
                        // プライマリキーを取得
                        var pkProperty = FindPrimaryKeyProperty(entity);
                        if (pkProperty == null)
                        {
                            throw new InvalidOperationException($"Could not find primary key for type {entityType.Name}");
                        }

                        var pkValue = pkProperty.GetValue(entity);

                        // 同じプライマリキーを持つエンティティがすでに追跡されているかチェック
                        var existingTrackedEntity = trackedEntities
                            .FirstOrDefault(e =>
                                e.GetType() == entityType &&
                                FindPrimaryKeyProperty(e)?.GetValue(e)?.Equals(pkValue) == true);

                        if (existingTrackedEntity != null)
                        {
                            switch (DuplicateBehavior)
                            {
                                case DuplicateEntityBehavior.ThrowException:
                                    throw new InvalidOperationException($"An entity with key {pkValue} is already being tracked");

                                case DuplicateEntityBehavior.Ignore:
                                    continue; // 既存のエンティティを無視して次へ

                                case DuplicateEntityBehavior.Update:
                                case DuplicateEntityBehavior.AddAlways:
                                    // これらのモードは後で処理
                                    break;
                            }
                        }

                        // エンティティを追跡対象に追加
                        trackedEntities.Add(entity);

                        // 既存のエンティティを検索
                        var existingEntity = dbSet.Find(pkValue);

                        if (existingEntity == null)
                        {
                            // エンティティが存在しない場合は追加
                            dbSet.Add((dynamic)entity);
                        }
                        else
                        {
                            // エンティティが存在する場合の処理
                            switch (DuplicateBehavior)
                            {
                                case DuplicateEntityBehavior.ThrowException:
                                    // プロパティを比較
                                    var properties = entityType.GetProperties()
                                        .Where(p => p.CanWrite && !IsNavigationProperty(p));

                                    bool hasChanges = false;
                                    foreach (var prop in properties)
                                    {
                                        var newValue = prop.GetValue(entity);
                                        var existingValue = prop.GetValue(existingEntity);
                                        if (!Equals(newValue, existingValue))
                                        {
                                            hasChanges = true;
                                            break;
                                        }
                                    }

                                    if (hasChanges)
                                    {
                                        // 異なるプロパティがある場合は例外をスロー
                                        throw new InvalidOperationException(
                                            $"An entity with the same primary key {pkValue} but different properties already exists.");
                                    }
                                    break;

                                case DuplicateEntityBehavior.Update:
                                    // 既存のエンティティを新しいエンティティの値で更新
                                    UpdateEntityProperties(existingEntity, entity);
                                    break;

                                case DuplicateEntityBehavior.Ignore:
                                    // すでに存在するため何もしない
                                    break;

                                case DuplicateEntityBehavior.AddAlways:
                                    // 新しいエンティティを常に追加（主キーを変更）
                                    AddEntityAlways(dbSet, entity, pkProperty);
                                    break;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        throw new InvalidOperationException(
                            $"Error processing entity of type {entityType.Name}: {ex.Message}", ex);
                    }
                }
            }
        }
    }

    /// <summary>
    /// 登録されているすべてのエンティティをModelBuilderのHasDataに適用します。
    /// </summary>
    /// <param name="modelBuilder">ModelBuilder</param>
    public void ApplyToModelBuilder(ModelBuilder modelBuilder)
    {
        if (modelBuilder == null)
        {
            throw new ArgumentNullException(nameof(modelBuilder));
        }

        foreach (var kvp in _entities)
        {
            var entityType = kvp.Key;
            var entities = kvp.Value;

            Debug.WriteLine($"処理中のエンティティタイプ: {entityType.Name}, エンティティ数: {entities.Count}");

            if (!entities.Any())
            {
                Debug.WriteLine($"{entityType.Name}にはエンティティがありません");
                continue; // エンティティがなければスキップ
            }

            try
            {
                // GetTypeInfoを使用してリフレクション情報を取得
                var entityTypeInfo = entityType.GetTypeInfo();

                // 匿名オブジェクトに変換するためのシードデータを取得
                var getSeedDataMethod = typeof(EntityScanner)
                    .GetMethod(nameof(GetSeedData))
                    .MakeGenericMethod(entityType);

                var seedData = getSeedDataMethod.Invoke(this, null) as IEnumerable<object>;

                if (seedData == null || !seedData.Any())
                {
                    Debug.WriteLine($"{entityType.Name}のシードデータが空です");
                    continue; // シードデータがなければスキップ
                }

                Debug.WriteLine($"{entityType.Name}のシードデータ数: {seedData.Count()}");

                // シードデータの内容を確認
                foreach (var item in seedData)
                {
                    var dict = item as IDictionary<string, object>;
                    if (dict != null)
                    {
                        Debug.WriteLine("シードデータの内容:");
                        foreach (var pair in dict)
                        {
                            Debug.WriteLine($"  {pair.Key}: {pair.Value}");
                        }
                    }
                }

                // ModelBuilderのEntityメソッドを取得
                var entityMethod = typeof(ModelBuilder).GetMethods()
                    .Where(m => m.Name == "Entity")
                    .Where(m => m.IsGenericMethod)
                    .Where(m => m.GetParameters().Length == 0)
                    .FirstOrDefault();

                if (entityMethod == null)
                {
                    Debug.WriteLine($"{entityType.Name}のEntityメソッドが見つかりません");
                    throw new InvalidOperationException($"Entity method not found for {entityType.Name}");
                }

                // EntityTypeBuilderを生成
                var genericEntityMethod = entityMethod.MakeGenericMethod(entityType);
                var entityBuilder = genericEntityMethod.Invoke(modelBuilder, null);
                Debug.WriteLine($"{entityType.Name}のEntityTypeBuilderを生成しました");

                // entityBuilderの型を取得
                var builderType = entityBuilder.GetType();

                // シードデータを正しい型の配列に変換
                var seedDataArray = ConvertSeedDataToTypedArray(entityType, seedData);
                Debug.WriteLine($"変換後のシードデータの型: {seedDataArray.GetType().FullName}");

                // HasDataメソッドを正しいシグネチャで取得
                var hasDataMethod = FindApplicableHasDataMethod(builderType, entityType);

                if (hasDataMethod == null)
                {
                    Debug.WriteLine($"{entityType.Name}のHasDataメソッドが見つかりません");
                    throw new InvalidOperationException($"Compatible HasData method not found for {entityType.Name}");
                }

                Debug.WriteLine($"HasDataメソッドを呼び出します: {hasDataMethod.Name}");
                // HasDataメソッドを呼び出し
                hasDataMethod.Invoke(entityBuilder, new object[] { seedDataArray });
                Debug.WriteLine($"{entityType.Name}のHasDataメソッドの呼び出しが完了しました");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"エラー発生: {ex.Message}");
                Debug.WriteLine($"詳細: {ex.StackTrace}");
                if (ex.InnerException != null)
                {
                    Debug.WriteLine($"内部例外: {ex.InnerException.Message}");
                    Debug.WriteLine($"内部例外詳細: {ex.InnerException.StackTrace}");
                }
                throw new InvalidOperationException(
                    $"Error applying seed data for entity type {entityType.Name}: {ex.Message}", ex);
            }
        }
    }

    // シードデータを正しい型の配列に変換するヘルパーメソッド
    private object ConvertSeedDataToTypedArray(Type entityType, IEnumerable<object> seedData)
    {
        // GetSeedEntitiesメソッドを使用して型付きのエンティティを取得
        var getSeedEntitiesMethod = typeof(EntityScanner)
            .GetMethod(nameof(GetSeedEntities))
            .MakeGenericMethod(entityType);

        var typedEntities = getSeedEntitiesMethod.Invoke(this, null);

        // 正しい型の配列を作成
        var arrayType = entityType.MakeArrayType();
        var array = Array.CreateInstance(entityType, seedData.Count());

        // IEnumerable<T>をT[]に変換
        var castMethod = typeof(Enumerable)
            .GetMethod("Cast")
            .MakeGenericMethod(entityType);

        var toArrayMethod = typeof(Enumerable)
            .GetMethod("ToArray")
            .MakeGenericMethod(entityType);

        var castedCollection = castMethod.Invoke(null, new object[] { typedEntities });
        var result = toArrayMethod.Invoke(null, new object[] { castedCollection });

        return result;
    }

    // 適切なHasDataメソッドを探すヘルパーメソッド
    private MethodInfo FindApplicableHasDataMethod(Type builderType, Type entityType)
    {
        var methods = builderType.GetMethods().Where(m => m.Name == "HasData").ToList();

        // 最適なHasDataメソッドを探す
        // 1. 単一のエンティティを受け取るメソッド
        var singleEntityMethod = methods.FirstOrDefault(m =>
            m.GetParameters().Length == 1 &&
            m.GetParameters()[0].ParameterType == entityType);

        if (singleEntityMethod != null)
        {
            return singleEntityMethod;
        }

        // 2. エンティティの配列を受け取るメソッド
        var arrayMethod = methods.FirstOrDefault(m =>
            m.GetParameters().Length == 1 &&
            m.GetParameters()[0].ParameterType.IsArray &&
            m.GetParameters()[0].ParameterType.GetElementType() == entityType);

        if (arrayMethod != null)
        {
            return arrayMethod;
        }

        // 3. IEnumerable<エンティティ>を受け取るメソッド
        var enumerableMethod = methods.FirstOrDefault(m =>
            m.GetParameters().Length == 1 &&
            m.GetParameters()[0].ParameterType.IsGenericType &&
            m.GetParameters()[0].ParameterType.GetGenericTypeDefinition() == typeof(IEnumerable<>) &&
            m.GetParameters()[0].ParameterType.GetGenericArguments()[0] == entityType);

        return enumerableMethod;
    }

    // エンティティのプロパティを更新するヘルパーメソッド
    private void UpdateEntityProperties(object existingEntity, object newEntity)
    {
        var properties = existingEntity.GetType().GetProperties()
            .Where(p => p.CanWrite && !IsNavigationProperty(p));

        foreach (var prop in properties)
        {
            var newValue = prop.GetValue(newEntity);
            prop.SetValue(existingEntity, newValue);
        }
    }

    // 新しいエンティティを常に追加するためのヘルパーメソッド
    private void AddEntityAlways(dynamic dbSet, object entity, PropertyInfo pkProperty)
    {
        // 重複を避けるため、主キーを一意にする戦略
        // オプション1: 一時的な一意のキーを生成
        var originalPkValue = pkProperty.GetValue(entity);
        pkProperty.SetValue(entity, GenerateUniqueKey(dbSet, originalPkValue));

        dbSet.Add((dynamic)entity);
    }

    // 一意のキーを生成するヘルパーメソッド
    private object GenerateUniqueKey(dynamic dbSet, object originalKey)
    {
        // データベース内の最大キー値を取得し、新しいキーを生成
        // 注意: これは簡単な実装で、実際のユースケースでは more robust な実装が必要
        int attempts = 0;
        object newKey = originalKey;

        while (dbSet.Find(newKey) != null && attempts < 100)
        {
            // キーを増分
            if (newKey is int intKey)
            {
                newKey = intKey + 1;
            }
            else if (newKey is long longKey)
            {
                newKey = longKey + 1;
            }
            else if (newKey is Guid)
            {
                newKey = Guid.NewGuid();
            }
            // 他の型の場合は適切な一意キー生成ロジックを追加

            attempts++;
        }

        if (attempts >= 100)
        {
            throw new InvalidOperationException("Could not generate a unique key");
        }

        return newKey;
    }

    // ナビゲーションプロパティを判定するヘルパーメソッド
    private bool IsNavigationProperty(PropertyInfo property)
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