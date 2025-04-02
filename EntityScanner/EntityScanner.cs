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

    // カスタム型の変換を登録するための辞書
    private readonly Dictionary<Type, Func<object, string>> _typeConverters = new();

    public EntityScanner(DuplicateEntityBehavior behavior = DuplicateEntityBehavior.ThrowException)
    {
        DuplicateBehavior = behavior;
        // IEntitySerializable を実装する型のコンバータを自動登録
        RegisterDefaultConverters();
    }

    public DuplicateEntityBehavior DuplicateBehavior { get; set; } = DuplicateEntityBehavior.ThrowException;

    /// <summary>
    ///     カスタム型のコンバータを登録
    /// </summary>
    /// <typeparam name="T">変換対象の型</typeparam>
    /// <param name="converter">変換関数</param>
    public void RegisterTypeConverter<T>(Func<T, string> converter) where T : class
    {
        _typeConverters[typeof(T)] = obj => converter((T)obj);
    }

    /// <summary>
    ///     初期化時にIEntitySerializableを実装する型のコンバータを自動登録
    /// </summary>
    private void RegisterDefaultConverters()
    {
        // アセンブリ内のIEntitySerializableを実装する全ての型を検索
        var entitySerializableTypes = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a => a.GetTypes())
            .Where(t => t.IsClass && !t.IsAbstract && typeof(IEntitySerializable).IsAssignableFrom(t));

        foreach (var type in entitySerializableTypes)
        {
            _typeConverters[type] = obj => ((IEntitySerializable)obj)?.ToEntityString();
        }
    }

    /// <summary>
    ///     指定された型がカスタムコンバータを持っているかチェック
    /// </summary>
    /// <param name="type">チェックする型</param>
    /// <returns>コンバータが登録されていればtrue</returns>
    private bool HasTypeConverter(Type type)
    {
        return _typeConverters.ContainsKey(type);
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
    /// <summary>
    ///     オーバーライドして、登録されたカスタム型を処理できるように拡張
    /// </summary>
    public IEnumerable<object> GetSeedData<T>() where T : class
    {
        var entities = GetEntities<T>();
        var result = new List<object>();

        foreach (var entity in entities)
        {
            // プロパティを収集する辞書
            var properties = new Dictionary<string, object>();

            foreach (var prop in typeof(T).GetProperties())
            {
                var value = prop.GetValue(entity);

                // null値はスキップ
                if (value == null)
                {
                    continue;
                }

                // 基本型またはIDプロパティの場合はそのまま追加
                if (IsBasicType(prop.PropertyType) || prop.Name.EndsWith("Id"))
                {
                    properties[prop.Name] = value;
                    continue;
                }

                // カスタムコンバータが登録されている型の場合
                if (HasTypeConverter(prop.PropertyType))
                {
                    var converter = _typeConverters[prop.PropertyType];
                    properties[prop.Name] = converter(value);
                    continue;
                }

                // IEntitySerializableインターフェースを実装している場合
                if (value is IEntitySerializable serializable)
                {
                    properties[prop.Name] = serializable.ToEntityString();
                }

                // その他のナビゲーションプロパティはスキップ
            }

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
                    else if (IsNavigationProperty(prop))
                    {
                        //ナビゲーションプロパティをForeignKey属性で指定しているプロパティを探す
                        var fkProp = FindForeignKeyProperty(prop);
                        if (fkProp != null)
                        {
                            var value = fkProp.GetValue(entity);
                            fkProp.SetValue(clone, value);
                            Debug.WriteLine($"  プロパティをコピー: {fkProp.Name} = {value}");
                        }
                        else
                        {
                            Debug.WriteLine($"  スキップしたプロパティ: {prop.Name} (ナビゲーションプロパティ)");
                        }
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
    /// ナビゲーションプロパティに関連付けられた外部キープロパティを探す
    /// </summary>
    /// <param name="navigationProp">ナビゲーションプロパティ</param>
    /// <returns>関連付けられた外部キープロパティ、見つからない場合はnull</returns>
    private PropertyInfo FindForeignKeyProperty(PropertyInfo navigationProperty)
    {
        if (navigationProperty == null)
        {
            return null;
        }

        var declaringType = navigationProperty.DeclaringType;
        if (declaringType == null)
        {
            return null;
        }

        // 1. [ForeignKey] 属性を持つプロパティを優先的に探す
        var properties = declaringType.GetProperties();
        foreach (var prop in properties)
        {
            var foreignKeyAttr = prop.GetCustomAttributes(typeof(System.ComponentModel.DataAnnotations.Schema.ForeignKeyAttribute), true)
                                  .FirstOrDefault() as System.ComponentModel.DataAnnotations.Schema.ForeignKeyAttribute;

            if (foreignKeyAttr != null && foreignKeyAttr.Name == navigationProperty.Name)
            {
                return prop;
            }
        }

        // 2. 一般的な命名規則に基づいて外部キープロパティを探す
        var navPropertyType = navigationProperty.PropertyType;
        var possibleFkNames = new[]
        {
            $"{navigationProperty.Name}Id",
            $"{navPropertyType.Name}Id"
        };

        foreach (var fkName in possibleFkNames)
        {
            var fkProperty = declaringType.GetProperty(fkName);
            if (fkProperty != null && IsValidForeignKeyProperty(fkProperty))
            {
                return fkProperty;
            }
        }

        // 3. 他のナビゲーションプロパティに関連する [InverseProperty] 属性を探す
        foreach (var prop in properties)
        {
            var inversePropertyAttr = prop.GetCustomAttributes(typeof(System.ComponentModel.DataAnnotations.Schema.InversePropertyAttribute), true)
                                     .FirstOrDefault() as System.ComponentModel.DataAnnotations.Schema.InversePropertyAttribute;

            if (inversePropertyAttr != null && inversePropertyAttr.Property == navigationProperty.Name)
            {
                // InversePropertyが見つかった場合、対応する外部キーを探す
                var relatedEntityType = prop.PropertyType;
                var foreignKeyProps = declaringType.GetProperties()
                                      .Where(p => p.Name.EndsWith("Id") && IsValidForeignKeyProperty(p))
                                      .ToList();

                if (foreignKeyProps.Count == 1)
                {
                    return foreignKeyProps[0]; // 単一の外部キーが見つかった場合
                }
            }
        }

        return null; // 対応する外部キープロパティが見つからなかった
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
                            throw new InvalidOperationException(
                                $"Could not find primary key for type {entityType.Name}");
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
                                    throw new InvalidOperationException(
                                        $"An entity with key {pkValue} is already being tracked");

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

                                    var hasChanges = false;
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
    ///     登録されているすべてのエンティティをModelBuilderのHasDataに適用します。
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
                // 重複エンティティの処理
                var distinctEntities = HandleDuplicateEntities(entities, entityType);

                // GetTypeInfoを使用してリフレクション情報を取得
                var entityTypeInfo = entityType.GetTypeInfo();

                // 匿名オブジェクトに変換するためのシードデータを取得（重複処理後のエンティティを使用）
                var seedData = GetSeedDataFromEntities(distinctEntities, entityType);

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
                hasDataMethod.Invoke(entityBuilder, new[] { seedDataArray });
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

    // 重複エンティティを処理するヘルパーメソッド
    private IList<object> HandleDuplicateEntities(IList<object> entities, Type entityType)
    {
        // DuplicateEntityBehaviorが無視または例外の場合は、EntityScannerに登録された段階で
        // 既に処理されているはずなので、ここでは追加の処理は不要
        if (DuplicateBehavior == DuplicateEntityBehavior.ThrowException ||
            DuplicateBehavior == DuplicateEntityBehavior.Ignore)
        {
            return entities;
        }

        // PKプロパティを見つける
        var pkProperty = FindPrimaryKeyProperty(entities.First());
        if (pkProperty == null)
        {
            Debug.WriteLine($"{entityType.Name}の主キープロパティが見つかりません");
            return entities; // PKが見つからない場合はそのまま返す
        }

        // エンティティをPK値でグループ化
        var groupedEntities = entities
            .GroupBy(e => pkProperty.GetValue(e)?.ToString())
            .ToList();

        // 重複がない場合はそのまま返す
        if (groupedEntities.All(g => g.Count() == 1))
        {
            return entities;
        }

        var result = new List<object>();

        // DuplicateEntityBehaviorに応じた処理
        switch (DuplicateBehavior)
        {
            case DuplicateEntityBehavior.Update:
                // 各グループから最後のエンティティを選択（最新とみなす）
                foreach (var group in groupedEntities)
                {
                    result.Add(group.Last());
                }

                Debug.WriteLine($"{entityType.Name}の重複エンティティを更新モードで処理しました: {entities.Count} -> {result.Count}");
                break;

            case DuplicateEntityBehavior.AddAlways:
                // AddAlwaysの場合、PKを変更する必要があるが、HasDataではPKは変更できないため
                // 警告を出して最初のエンティティのみ残す
                Debug.WriteLine("警告: ModelBuilder.HasDataでは重複PKを持つエンティティを追加できません。最初のエンティティのみ使用します。");
                foreach (var group in groupedEntities)
                {
                    result.Add(group.First());
                }

                break;

            default:
                return entities; // 他の場合はそのまま返す
        }

        return result;
    }

    // 指定されたエンティティリストからシードデータを取得するヘルパーメソッド
    private IEnumerable<object> GetSeedDataFromEntities(IList<object> entities, Type entityType)
    {
        var result = new List<object>();

        foreach (var entity in entities)
        {
            // 基本プロパティのみを含む匿名オブジェクトを作成
            var properties = entityType.GetProperties()
                .Where(p => IsBasicType(p.PropertyType))
                .ToDictionary(p => p.Name, p => p.GetValue(entity));

            result.Add(properties);
        }

        return result;
    }

    // シードデータを正しい型の配列に変換するヘルパーメソッド
    private object ConvertSeedDataToTypedArray(Type entityType, IEnumerable<object> seedData)
    {
        // GetSeedEntitiesメソッドを使用して型付きのエンティティを取得
        var getSeedEntitiesMethod = typeof(EntityScanner)
            .GetMethod(nameof(GetSeedEntities))
            .MakeGenericMethod(entityType);

        //var typedEntities = getSeedEntitiesMethod.Invoke(this, null);

        // 正しい型の配列を作成
        var arrayType = entityType.MakeArrayType();
        //空の配列が返される
        var array = Array.CreateInstance(entityType, seedData.Count());

        //seedDataはDictionary<string, object>のリスト
        //entityTypeのプロパティ名と値を保持している
        //これらをリフレクションを使って、arrayにコピーしていく
        array = ConvertSeedDataToTypedArrayInternal(entityType, seedData);


        // IEnumerable<T>をT[]に変換
        var castMethod = typeof(Enumerable)
            .GetMethod("Cast")
            .MakeGenericMethod(entityType);

        var toArrayMethod = typeof(Enumerable)
            .GetMethod("ToArray")
            .MakeGenericMethod(entityType);

        var castedCollection = castMethod.Invoke(null, new object[] { array });
        var result = toArrayMethod.Invoke(null, new[] { castedCollection });

        return result;
    }

    // シードデータを正しい型の配列に変換するヘルパーメソッド
    private Array ConvertSeedDataToTypedArrayInternal(Type entityType, IEnumerable<object> seedData)
    {
        Debug.WriteLine($"シードデータを{entityType.Name}の配列に変換します");

        // 正しい型の配列を作成
        var seedDataList = seedData.ToList();
        var array = Array.CreateInstance(entityType, seedDataList.Count);

        // seedDataはDictionary<string, object>のリスト
        // これらをリフレクションを使って、entityType型のオブジェクトに変換
        for (var i = 0; i < seedDataList.Count; i++)
        {
            var dataItem = seedDataList[i] as IDictionary<string, object>;
            if (dataItem == null)
            {
                Debug.WriteLine($"警告: インデックス{i}のシードデータをDictionaryとして取得できませんでした");
                continue;
            }

            // entityTypeの新しいインスタンスを作成
            var instance = Activator.CreateInstance(entityType);
            Debug.WriteLine($"新しい{entityType.Name}インスタンスを作成しました");

            // プロパティを設定
            foreach (var kvp in dataItem)
            {
                var property = entityType.GetProperty(kvp.Key);
                if (property != null && property.CanWrite)
                {
                    try
                    {
                        // 値の型変換が必要な場合は変換を行う
                        var value = kvp.Value;
                        if (value != null && property.PropertyType != value.GetType() &&
                            value.GetType() != Nullable.GetUnderlyingType(property.PropertyType))
                        {
                            value = Convert.ChangeType(value,
                                Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType);
                        }

                        property.SetValue(instance, value);
                        Debug.WriteLine($"  プロパティを設定: {kvp.Key} = {kvp.Value}");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"  プロパティ設定エラー: {kvp.Key} - {ex.Message}");
                    }
                }
                else
                {
                    Debug.WriteLine($"  プロパティが見つからないかWriteableではありません: {kvp.Key}");
                }
            }

            // 配列にインスタンスを設定
            array.SetValue(instance, i);
        }

        Debug.WriteLine($"{array.Length}個のアイテムを持つ{entityType.Name}[]を作成しました");
        return array;
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
        var attempts = 0;
        var newKey = originalKey;

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
        if (entity == null || navigationProperty == null || navigationValue == null)
        {
            return;
        }

        // 対応する外部キープロパティを探す
        var fkProperty = FindForeignKeyProperty(navigationProperty);
        if (fkProperty != null && fkProperty.CanWrite)
        {
            // 主キーの値を取得
            var pkProperty = FindPrimaryKeyProperty(navigationValue);
            if (pkProperty != null)
            {
                var pkValue = pkProperty.GetValue(navigationValue);
                // 外部キーに主キーの値を設定
                fkProperty.SetValue(entity, pkValue);
                Debug.WriteLine($"外部キーを設定: {entity.GetType().Name}.{fkProperty.Name} = {pkValue}");
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