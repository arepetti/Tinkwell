namespace Tinkwell.Store.Storage.Sqlite;

static class SqlCommands
{
    public static readonly string CreateDefinitionsTable = @"
        CREATE TABLE IF NOT EXISTS Definitions (
            Name TEXT PRIMARY KEY,
            Type TEXT NOT NULL,
            Attributes INTEGER NOT NULL,
            Ttl INTEGER,
            QuantityType TEXT,
            Unit TEXT,
            Minimum REAL,
            Maximum REAL,
            Precision INTEGER
        );
    ";

    public static readonly string CreateMetadataTable = @"
        CREATE TABLE IF NOT EXISTS Metadata (
            Name TEXT PRIMARY KEY,
            CreatedAt INTEGER NOT NULL,
            Description TEXT,
            Category TEXT,
            Tags TEXT
        );
    ";

    public static readonly string CreateMeasuresTable = @"
        CREATE TABLE IF NOT EXISTS Measures (
            Name TEXT PRIMARY KEY,
            Type INTEGER NOT NULL,
            StringValue TEXT,
            DoubleValue REAL,
            Timestamp INTEGER
        );
    ";

    public static readonly string FindDefinitionByName = "SELECT * FROM Definitions WHERE Name = @Name";
    public static readonly string FindAllDefinitions = "SELECT * FROM Definitions";
    public static readonly string FindAllDefinitionsByNames = "SELECT * FROM Definitions WHERE Name IN @Names";
    public static readonly string InsertDefinition = "INSERT INTO Definitions (Name, Type, Attributes, Ttl, QuantityType, Unit, Minimum, Maximum, Precision) VALUES (@Name, @Type, @Attributes, @Ttl, @QuantityType, @Unit, @Minimum, @Maximum, @Precision)";
    public static readonly string DeleteDefinition = "DELETE FROM Definitions WHERE Name = @Name";

    public static readonly string FindMetadataByName = "SELECT * FROM Metadata WHERE Name = @Name";
    public static readonly string FindAllMetadata = "SELECT * FROM Metadata";
    public static readonly string FindAllMetadataByNames = "SELECT * FROM Metadata WHERE Name IN @Names";
    public static readonly string InsertMetadata = "INSERT INTO Metadata (Name, CreatedAt, Description, Category, Tags) VALUES (@Name, @CreatedAt, @Description, @Category, @Tags)";
    public static readonly string DeleteMetadata = "DELETE FROM Metadata WHERE Name = @Name";

    public static readonly string FindMeasureByName = "SELECT * FROM Measures WHERE Name = @Name";
    public static readonly string FindAllMeasures = "SELECT * FROM Measures";
    public static readonly string FindAllMeasuresByNames = "SELECT * FROM Measures WHERE Name IN @Names";
    public static readonly string InsertMeasure = "INSERT INTO Measures (Name, Type) VALUES (@Name, @Type)";
    public static readonly string UpdateMeasureValue = "UPDATE Measures SET Timestamp = @Timestamp, Type = @Type, StringValue = @StringValue, DoubleValue = @DoubleValue WHERE Name = @Name";
    public static readonly string DeleteMeasure = "DELETE FROM Measures WHERE Name = @Name";
}
