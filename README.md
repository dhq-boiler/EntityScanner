# EntityScanner

## Overview

EntityScanner is an advanced seed data management utility for Entity Framework Core. It simplifies handling complex object graphs and automatically manages foreign key relationships.

## Key Features

- 🔗 **Automatic Foreign Key Management**: Effortlessly set foreign keys for entities with complex relationships
- 🌳 **Hierarchical Data Support**: Handle nested object structures with ease
- 🔄 **Many-to-Many Relationship Handling**: Simplify management of complex join tables
- 🛠️ **Flexible Duplicate Entity Handling**: Multiple strategies for dealing with duplicate entities

## Installation

Install via NuGet Package Manager:

```bash
dotnet add package EntityScanner
```

## Usage Examples

### Basic Usage

```csharp
// Create a category and a book
var category = new Category { Name = "Programming" };
var book = new Book 
{ 
    Title = "Introduction to C#", 
    Category = category 
};

// Use EntityScanner to automatically set foreign keys
var entityScanner = new EntityScanner();
entityScanner.RegisterEntity(book);

// Apply to DbContext
using (var context = new YourDbContext(options))
{
    entityScanner.ApplyToContext(context);
    context.SaveChanges();
}
```

### Handling Duplicate Entities

```csharp
// Set duplicate entity handling strategy
var entityScanner = new EntityScanner(DuplicateEntityBehavior.Update);

// Update: Existing entities will be updated with new values
entityScanner.RegisterEntity(existingBook);
```

## Supported Relationships

- One-to-Many
- Many-to-One
- Many-to-Many
- One-to-One
- Self-Referencing (Hierarchical Data)

## Requirements

- .NET Standard 2.0
- Entity Framework Core 3.1+

## Use Cases

- Seed data management
- Database migrations
- Test data generation
- Processing complex object graphs

## Considerations

- Foreign keys are automatically set, but compatibility with database schema is essential
- Be mindful of performance when processing large amounts of data

## Duplicate Entity Behaviors

- `ThrowException`: Throw an error when duplicate entities are detected
- `Update`: Update existing entities with new values
- `Ignore`: Skip duplicate entities
- `AddAlways`: Always add new entities, allowing duplicates

## License

[LICENSE](LICENSE)

## Contributing

Report bugs or feature requests through the GitHub issue tracker.

## Best Practices

- Register root entities to automatically process related entities
- Use `RegisterEntity` for complex object graphs
- Choose appropriate duplicate entity behavior based on your use case