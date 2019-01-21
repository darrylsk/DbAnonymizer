using System.Data;
using System.Text;
using static System.Console;
using System;
using System.Linq;
using Microsoft.SqlServer.Management.Common;
using System.Data.SqlClient;
using Microsoft.SqlServer.Management.Smo;
using System.Collections.Generic;
using DbAnonymizer.Console.Helpers;

namespace DbAnonymizer.Console
{
    internal class DatabaseCopier
    {
        private readonly Database _originalDatabase;
        private readonly SqlConnectionInfo _connectionInfo;
        private readonly string _copyDatabaseName;
        private readonly Server _server;
        private readonly string _originalDatabaseName;
        private Database _copyDatabase;

        public DatabaseCopier(Server server, SqlConnectionInfo connectionInfo, string originalDatabaseName, string copyDatabaseName)
        {
            _server = server;
            _originalDatabaseName = originalDatabaseName;
            _copyDatabaseName = copyDatabaseName;
            _connectionInfo = connectionInfo;
            _originalDatabase = _server.Databases[_originalDatabaseName];
            _copyDatabase = _server.Databases[_copyDatabaseName];
        }

        public Database OriginalDatabase { get => _originalDatabase; }
        public Database CopyDatabase { get => _copyDatabase; }
        public Server Server => _server;
        public SqlConnectionInfo ConnectionInfo => _connectionInfo;

        public string OriginalDatabaseName => _originalDatabaseName;

        public bool TweakNumerics { get; internal set; }
        public int Shrinkage { get; internal set; }
        public bool Verbose { get; internal set; }
        public int MaxRows { get; internal set; }

        public void CopyAllTables()
        {
            // Ask the user to drop the old copy of the destination database if a previous edition of it already exists from a prior run of this program.
            if (_copyDatabase != null)
            {
                Write($"Drop database {_copyDatabase.Name}? (y/n): ");
                var doDrop = ReadLine();
                if (doDrop.ToLower() != "y") return;
                _copyDatabase.DropIfExists();
            }

            // Create a new destination database.
            _copyDatabase = new Database(_server, _copyDatabaseName);
            _copyDatabase.Create();

            // Before attempting to copy tables, prepare the destination database by adding any general dependencies such as schemas and user-defined types.

            // Copy any/all schemas
            foreach (Schema originalSchema in _originalDatabase.Schemas)
            {
                if (_copyDatabase.Schemas[originalSchema.Name] != null) continue;

                var copySchema = new Schema(_copyDatabase, originalSchema.Name);
                _copyDatabase.Schemas.Add(copySchema);
                copySchema.Create();
            }

            // Copy any/all XmlSchemaColletions
            foreach (XmlSchemaCollection originalXmlSchemaCollection in _originalDatabase.XmlSchemaCollections)
            {
                var copyXmlSchemaCollection = new XmlSchemaCollection(
                    _copyDatabase, originalXmlSchemaCollection.Name, originalXmlSchemaCollection.Schema)
                {
                    Text = originalXmlSchemaCollection.Text
                };

                _copyDatabase.XmlSchemaCollections.Add(copyXmlSchemaCollection);
                copyXmlSchemaCollection.Create();
            }

            // Copy any/all user defined functions.
            var udfList = new List<UserDefinedFunction>();
            foreach (UserDefinedFunction originalFunction in _originalDatabase.UserDefinedFunctions)
            {
                udfList.Add(originalFunction);
            }

            // Copy all functions
            foreach (UserDefinedFunction originalFunction in udfList.Where(f => f.Schema != "sys"))
            {
                var copyFunction = new UserDefinedFunction(_copyDatabase, originalFunction.Name);
                copyFunction.Owner = originalFunction.Owner;
                copyFunction.QuotedIdentifierStatus = originalFunction.QuotedIdentifierStatus;
                copyFunction.Schema = originalFunction.Schema;
                copyFunction.TextBody = originalFunction.TextBody;
                copyFunction.TextHeader = originalFunction.TextHeader;
                copyFunction.TextMode = false;
                copyFunction.MethodName = originalFunction.MethodName;
                copyFunction.TableVariableName = originalFunction.TableVariableName;
                copyFunction.UserData = originalFunction.UserData;
                copyFunction.Schema = originalFunction.Schema;
                copyFunction.ImplementationType = originalFunction.ImplementationType;
                copyFunction.IsEncrypted = originalFunction.IsEncrypted;
                if (originalFunction.DataType != null) copyFunction.DataType = originalFunction.DataType;
                copyFunction.FunctionType = originalFunction.FunctionType;
                copyFunction.ExecutionContext = originalFunction.ExecutionContext;
                copyFunction.ExecutionContextPrincipal = originalFunction.ExecutionContextPrincipal;
                copyFunction.ClassName = originalFunction.ClassName;
                copyFunction.AnsiNullsStatus = originalFunction.AnsiNullsStatus;

                foreach (Column srcfncol in originalFunction.Columns)
                {
                    var fncol = new Column(copyFunction, srcfncol.Name, srcfncol.DataType);
                    copyFunction.Columns.Add(fncol);
                }

                foreach (UserDefinedFunctionParameter srcparam in originalFunction.Parameters)
                {
                    var param = new UserDefinedFunctionParameter(copyFunction, srcparam.Name, srcparam.DataType);
                    copyFunction.Parameters.Add(param);
                }
                foreach (ExtendedProperty srcprop in originalFunction.ExtendedProperties)
                {
                    var xprop = new ExtendedProperty(copyFunction, srcprop.Name, srcprop.Value) { Value = srcprop.Value };
                    copyFunction.ExtendedProperties.Add(xprop);
                }

                copyFunction.Create();
            }

            // Copy all Table Definitions and Data
            var n = 0; var tableCount = OriginalDatabase.Tables.Count;

            // Make sure that the tables having no foreign keys are copied first.
            // Attempt also to order the tables so that parent tables are created before the child that depends on them.
            //var originalTableList = ParentTablesFirst(OriginalDatabase.Tables);
            var originalTableList = ParentTablesLast(OriginalDatabase.Tables);

            foreach (Table originalTable in originalTableList)
            {
                WriteLine($"Creating table: {originalTable.Schema}.{originalTable.Name} ({++n} of {tableCount})");
                CopyTableDefinitions(originalTable);
            }

            n = 0;
            foreach (Table originalTable in originalTableList)
            {
                WriteLine($"Creating foregin keys for table: {originalTable.Schema}.{originalTable.Name} ({++n} of {tableCount})");
                CopyForeignKeys(originalTable);
            }

            var sourceConnection = new ServerConnection(_connectionInfo) { DatabaseName = _originalDatabaseName }.SqlConnectionObject;
            var destConnection = new ServerConnection(_connectionInfo) { DatabaseName = _copyDatabaseName }.SqlConnectionObject;
            n = 0;
            foreach (Table originalTable in originalTableList)
            {
                var countSql = $"select count(*) from {originalTable.Schema}.{originalTable.Name}";
                var ds = _originalDatabase.ExecuteWithResults(countSql);
                var dataTable = ds.Tables[0];
                var row = dataTable.Rows[0];
                var count = (int)row[0];

                Write($"Copying table data: {originalTable.Schema}.{originalTable.Name} ({++n} of {tableCount}) ({count:000,000} rows)");

                CopyTableData(originalTable);
                WriteLine();
            }
            sourceConnection.Close();
            sourceConnection.Dispose();
            destConnection.Close();
            destConnection.Dispose();

        }

        private void CopyForeignKeys(Table originalTable)
        {
            //var copyTable = new Table(_copyDatabase, originalTable.Name, originalTable.Schema);
            var copyTable = _copyDatabase.Tables[originalTable.Name, originalTable.Schema];

            foreach (ForeignKey origfk in originalTable.ForeignKeys)
            {
                var fk = new ForeignKey(copyTable, origfk.Name)
                {
                    DeleteAction = origfk.DeleteAction,
                    IsChecked = origfk.IsChecked,
                    IsEnabled = origfk.IsEnabled,
                    ReferencedTable = origfk.ReferencedTable,
                    ReferencedTableSchema = origfk.ReferencedTableSchema,
                    UpdateAction = origfk.UpdateAction
                };

                foreach (ForeignKeyColumn scol in origfk.Columns)
                {
                    var refcol = scol.ReferencedColumn;
                    var column = new ForeignKeyColumn(fk, scol.Name, refcol);
                    fk.Columns.Add(column);
                }
                fk.Create();
            }
        }

        /// <summary>
        /// Create a list of tables in dependency order, ensuring that a table containing a foreign key appears later in
        /// the list than the table it references.
        /// </summary>
        /// <param name="tables"></param>
        /// <returns></returns>
        private List<Table> ParentTablesFirst(TableCollection tables)
        {
            var tableArray = new Table[tables.Count];
            tables.CopyTo(tableArray, 0);

            var childTableList = new List<Table>();
            var parentTableList = tableArray.Where(t => t.ForeignKeys.Count == 0).ToList();

            foreach (Table table in tables)
            {
                AddParentBefore(tableArray, parentTableList, childTableList, table);
            }

            parentTableList.AddRange(childTableList);

            return parentTableList;
        }

        /// <summary>
        /// Create a list of tables in dependency order, ensuring that a table containing a foreign key appears earlier in
        /// the list than the table it references.
        /// </summary>
        /// <param name="tables"></param>
        /// <returns></returns>
        private static List<Table> ParentTablesLast(TableCollection tables)
        {
            var tableArray = new Table[tables.Count];
            tables.CopyTo(tableArray, 0);
            var leafTableList = tableArray.Where(t => t.ForeignKeys.Count == 0);

            var tableStack = new Stack<Table>();

            foreach (Table table in tables)
            {
                StackTable(table, tableStack, tables);
            }

            var orderedTableList = tableStack.ToList();

            return orderedTableList;
        }

        private static void StackTable(Table table, Stack<Table> tableStack, TableCollection tables)
        {
            if (table.ForeignKeys == null || table.ForeignKeys.Count == 0)
            {
                if (false == tableStack.Contains(table))
                    tableStack.Push(table);
            }
            else
            {
                foreach (ForeignKey foreignKey in table.ForeignKeys)
                {
                    StackTable(tables[foreignKey.ReferencedTable, foreignKey.ReferencedTableSchema], tableStack, tables);
                }
                if (false == tableStack.Contains(table))
                    tableStack.Push(table);
            }
        }

        private static void AddParentBefore(Table[] tableArray, List<Table> parentTableList, List<Table> childTableList, Table table)
        {
            if (table.ForeignKeys.Count > 0)
            {
                foreach (ForeignKey fk in table.ForeignKeys)
                {
                    if (parentTableList.Exists(t => t.Name == fk.ReferencedTable) == false &&
                        childTableList.Exists(t => t.Name == fk.ReferencedTable) == false &&
                        tableArray.SingleOrDefault(t => t.Name == fk.ReferencedTable) != null)
                    {
                        AddParentBefore(tableArray, parentTableList, childTableList, tableArray.Single(t => t.Name == fk.ReferencedTable));
                    }
                    var newTable = tableArray.Single(t => t.Name == fk.ReferencedTable);
                    if (childTableList.Contains(newTable) == false && parentTableList.Contains(newTable) == false)
                        childTableList.Add(newTable);
                }

                if (childTableList.Contains(table) == false)
                    childTableList.Add(table);
            }
        }

        private void CopyTableDefinitions(Table originalTable)
        {
            var copyTable = new Table(_copyDatabase, originalTable.Name, originalTable.Schema);

            // Copy all the columns
            foreach (Column originalColumn in originalTable.Columns)
            {
                // If the column's data type is a user-defined datatype that doesn't doesn't exist in the copy 
                // database, then create it in the copy.
                Column copyColumn;
                if (_copyDatabase.UserDefinedDataTypes[originalColumn.DataType.Name] == null && _originalDatabase.UserDefinedDataTypes[originalColumn.DataType.Name] != null)
                {
                    var copyType = new UserDefinedDataType(_copyDatabase, originalColumn.DataType.Name);
                    copyType.Length = originalColumn.DataType.MaximumLength;
                    copyType.SystemType = "nvarchar";
                    copyType.Create();

                    copyColumn = new Column(copyTable, originalColumn.Name, DataType.UserDefinedDataType(copyType.Name));
                    copyColumn.DataType.MaximumLength = copyType.Length;
                }
                else
                {
                    copyColumn = new Column(copyTable, originalColumn.Name, originalColumn.DataType);
                }

                copyColumn.UserData = originalColumn.UserData;
                copyColumn.Nullable = originalColumn.Nullable;
                copyColumn.Computed = originalColumn.Computed;
                copyColumn.ComputedText = originalColumn.ComputedText;
                copyColumn.Default = originalColumn.Default;

                if (originalColumn.DefaultConstraint != null)
                {
                    var tabname = copyTable.Name;
                    var constrname = originalColumn.DefaultConstraint.Name;
                    copyColumn.AddDefaultConstraint(tabname + "_" + constrname);
                    copyColumn.DefaultConstraint.Text = originalColumn.DefaultConstraint.Text;
                }

                copyColumn.IsPersisted = originalColumn.IsPersisted;
                copyColumn.DefaultSchema = originalColumn.DefaultSchema;
                copyColumn.RowGuidCol = originalColumn.RowGuidCol;

                if (Server.VersionMajor >= 10)
                {
                    copyColumn.IsFileStream = originalColumn.IsFileStream;
                    copyColumn.IsSparse = originalColumn.IsSparse;
                    copyColumn.IsColumnSet = originalColumn.IsColumnSet;
                }

                copyTable.Columns.Add(copyColumn);
            }

            // Copy all the indexes.
            foreach (Index origx in originalTable.Indexes)
            {
                var copyx = new Index
                {
                    Name = origx.Name,
                    Parent = copyTable,
                    PadIndex = origx.PadIndex,
                    IsUnique = origx.IsUnique,
                    IndexType = origx.IndexType,
                    IsClustered = origx.IsClustered,
                    IndexKeyType = origx.IndexKeyType,
                    IsFullTextKey = origx.IsFullTextKey,
                    ParentXmlIndex = origx.ParentXmlIndex,
                    IndexedXmlPathName = origx.IndexedXmlPathName,
                    IgnoreDuplicateKeys = origx.IgnoreDuplicateKeys,
                    SecondaryXmlIndexType = origx.SecondaryXmlIndexType,
                    FileGroup = origx.FileGroup
                };

                foreach (IndexedColumn srccol in origx.IndexedColumns)
                {
                    // A computed column cannot be included in an index because it is nondeterministic
                    // Some databases appear to have done this, noetheless.  If we come accros it, don't try to copy it.

                    if (srccol.IsComputed) continue;
                    IndexedColumn column =
                     new IndexedColumn(copyx, srccol.Name, srccol.Descending);
                    column.IsIncluded = srccol.IsIncluded;
                    copyx.IndexedColumns.Add(column);
                }

                // If an indexed column could not be included, this may leave the index with no columns at all.
                if (copyx.IndexedColumns.Count > 0) copyTable.Indexes.Add(copyx);
            }

            copyTable.Create();
        }

        /// <summary>
        /// Copy data from the original table into the new table in the copy database.
        /// </summary>
        /// <param name="originalTable"></param>
        private void CopyTableData(Table originalTable)
        {

            // Todo: Cannot copy HierarchyId or Geography column data (and probably not Geometry, but haven't tried) .
            // Just avoid copying certain types of columns until/unless we can figure out a way to read them.
            // * The DataReader object seems to choke on HierarchyId and Geography.
            // * The DataAdapter seems to have trouble with user defined types based on nvarchar(1) as evidenced with the flag and NameStyle types
            //   in the AdventureWorks2017 database.

            var selectList = SelectListBuilder(originalTable);

            var selectString = MaxRows > 0
                ? $"select top {MaxRows} {selectList} from {originalTable.Schema}.{originalTable.Name}"
                : $"select {selectList} from {originalTable.Schema}.{originalTable.Name}";

            // Note: For now, shrinkage should be uses with caution as it can cause very bad performance
            // and heavy memory use.
            if (MaxRows == 0 && Shrinkage > 0) // MaxRows overrides Shrinkage factor if both are set.
            {
                var countSql = $"select count(*) from {originalTable.Schema}.{originalTable.Name}";
                var countDs = _originalDatabase.ExecuteWithResults(countSql);
                DataTable countDt = countDs.Tables[0];
                DataRow row = countDt.Rows[0];
                var count = (int)row[0];

                var numRows = Math.Max(count - (int)(count * Shrinkage / 100.0), 1);

                selectString =
                    $"select top {numRows} {selectList} from {originalTable.Schema}.{originalTable.Name}";
            }

            // Create data adapters connected to the source and copy databases.
            SqlDataAdapter sourceAdapter = new SqlDataAdapter(selectString, new ServerConnection(_connectionInfo) { DatabaseName = _originalDatabaseName }.SqlConnectionObject);

            // Create a data adapter to add records to the copy database.
            var selectConnection = new ServerConnection(_connectionInfo) { DatabaseName = _copyDatabaseName }.SqlConnectionObject;
            SqlDataAdapter copyAdapter = new SqlDataAdapter(selectString, selectConnection);

            // Generate a SQL insert command based on the current table defination.
            SqlCommandBuilder commandBuilder = new SqlCommandBuilder(copyAdapter);
            copyAdapter.InsertCommand = commandBuilder.GetInsertCommand();

            // Copy the contents from the original database table into a dataset.
            var ds = _originalDatabase.ExecuteWithResults(selectString);

            AddRelatedRecords(ds, originalTable, selectConnection);


            foreach (DataTable dataTable in ds.Tables)
            {
                // Call accept changes on the dataset, to reset any pending changes, then set each row to "added" state.
                dataTable.AcceptChanges();
                var rowct = 0;
                Write(" - Adding Row: ");
                foreach (DataRow row in dataTable.Rows)
                {
                    foreach (ForeignKey fk in originalTable.ForeignKeys)
                    {
                        var dt = BuildDataTableFrom(fk, row);
                    }

                    row.SetAdded();
                    if (++rowct > 1000) return;
                    Write($"{rowct:000,000}\x08\x08\x08\x08\x08\x08\x08");
                    foreach (DataColumn column in dataTable.Columns)
                    {
                        AnonomizeColumn(originalTable, row, column);
                    }
                }
            }
            try
            {
                copyAdapter.Update(ds);
            }
            catch (Exception ex)
            {
                if (Verbose)
                {
                    WriteLine($"Table: {originalTable.Schema}.{originalTable.Name}");

                    WriteLine(ex.Message);
                    if (ex.InnerException != null)
                    {
                        WriteLine(ex.InnerException.Message);
                    }
                }
            }
        }

        private void AnonomizeColumn(Table originalTable, DataRow row, DataColumn column)
        {
            var originalColumn = originalTable.Columns[column.ColumnName];
            var dataTypeName = originalColumn.DataType.Name;
            var isPkColumn = originalTable.Columns[column.ColumnName].InPrimaryKey;
            var isFkColumn = originalTable.Columns[column.ColumnName].IsForeignKey;
            var isTextColumn = originalTable.Columns[column.ColumnName].DataType.IsStringType;
            var isNumericColumn = originalTable.Columns[column.ColumnName].DataType.IsNumericType;
            var isDateTimeColumn = originalTable.Columns[column.ColumnName].DataType.SqlDataType == SqlDataType.DateTime;
            var fieldLength = originalTable.Columns[column.ColumnName].DataType.MaximumLength;
            var isNVarcharMax = originalTable.Columns[column.ColumnName].DataType.SqlDataType == SqlDataType.NVarCharMax;
            if (originalColumn.DataType.Name == "Name")
            {
                row.SetField(column, fieldLength.RandomStringOfLength(StringStyles.AlphaMixed));
            }

            // Do not manipulate the values of primary or foreign key fields.
            if (isPkColumn || isFkColumn) return;

            // If the column's value is null, there is nothing to be done.
            if (row[column.ColumnName] == null || row[column.ColumnName].ToString() == "") return;

            if (isNumericColumn && TweakNumerics)
            {
                if (dataTypeName.Contains("int"))
                {
                    row.SetField(column, int.Parse(row[column.ColumnName].ToString()).Tweak());
                }
                else if (dataTypeName.Contains("decimal") || dataTypeName.Contains("money") ||
                    dataTypeName.Contains("double"))
                {
                    row.SetField(column, decimal.Parse(row[column.ColumnName].ToString()).Tweak());
                }
            }

            if (isDateTimeColumn)
            {
                // For datetime values, choose a random value that is fairly close to the present.
                row.SetField(column, DateTime.Now.Tweak());
            }

            if (isTextColumn && isNVarcharMax == false)
            {
                if (column.ColumnName.ToLower().Contains("email"))
                    row.SetField(column, fieldLength.RandomStringOfLength(StringStyles.Email));
                else if (column.ColumnName.ToLower().Contains("phone"))
                    row.SetField(column, fieldLength.RandomStringOfLength(StringStyles.Phone));
                else
                    row.SetField(column, fieldLength.RandomStringOfLength(StringStyles.AlphaLower));
            }

            if (_copyDatabase.UserDefinedDataTypes[dataTypeName] != null)
            {
                switch (dataTypeName)
                {
                    case "AccountNumber":
                        row.SetField(column, fieldLength.RandomStringOfLength(StringStyles.Numeric));
                        break;
                    case "Flag":
                        // Todo: DataTable column type comes out as int32 instead of string/char.   Need to fix.
                        break;
                    case "Name":
                        row.SetField(column, fieldLength.RandomStringOfLength(StringStyles.AlphaUpper));
                        break;
                    case "NameStyle":
                        break;
                    case "OrderNumber":
                        row.SetField(column, fieldLength.RandomStringOfLength(StringStyles.Numeric));
                        break;
                    case "Phone":
                        var length = Math.Max(fieldLength, 14);
                        row.SetField(column, length.RandomStringOfLength(StringStyles.Phone));
                        break;
                    case "Email":
                        row.SetField(column, fieldLength.RandomStringOfLength(StringStyles.Email));
                        break;
                    default:
                        row.SetField(column, fieldLength.RandomStringOfLength(StringStyles.AlphaLower));
                        break;
                }
            }
        }

        private object BuildDataTableFrom(ForeignKey fk, DataRow row)
        {
            var sql = BuildSqlFrom(fk, row);
            var ds = _originalDatabase.ExecuteWithResults(sql);

            return fk;
        }

        private void AddRelatedRecords(DataSet ds, Table sourceTable, SqlConnection destConnection)
        {

            var dt = ds.Tables[0];
            foreach (DataRow row in dt.Rows)
            {
                foreach (ForeignKey fk in sourceTable.ForeignKeys)
                {
                    var fkTable = _originalDatabase.Tables[fk.ReferencedTable, fk.ReferencedTableSchema];
                    if (Verbose) Write($"--> {fkTable} ");
                    var select = BuildSqlFrom(fk, row);

                    // If, for example, a foreign key column has a null value then there can be no matching record
                    // in the primary key base table, so don't bother trying to fetch one.  The 'BuildSqlFrom'
                    // method will detect this condition and return an empty string when it happens.
                    if (string.IsNullOrEmpty(select)) continue;

                    var ds2 = _originalDatabase.ExecuteWithResults(select);

                    // Recursively call this method to add rows to the deepest referenced tab
                    if (fkTable.ForeignKeys.Count > 0)
                        AddRelatedRecords(ds2, fkTable, destConnection);

                    var relatedTable = ds2.Tables[0];
                    relatedTable.AcceptChanges();
                    foreach (DataRow rtrow in relatedTable.Rows)
                    {
                        rtrow.SetAdded();
                        foreach (DataColumn column in relatedTable.Columns)
                        {
                            AnonomizeColumn(fkTable, rtrow, column);
                        }
                    }
                    var copyAdapter = new SqlDataAdapter(select, destConnection);

                    // Generate a SQL insert command based on the current table defination.
                    var commandBuilder = new SqlCommandBuilder(copyAdapter);
                    copyAdapter.InsertCommand = commandBuilder.GetInsertCommand();

                    try
                    {
                        copyAdapter.Update(relatedTable);

                    }
                    catch (SqlException ex)
                    {
                        // We'll commonly get primary key violations here, because several fk's may reference the same
                        // database record.  We can safely ignore these errors, since they only result from the fact that
                        // the work we are trying to do has already been done.
                        if (Verbose) WriteLine(ex.Message);
                    }
                }
            }
        }

        private static string SelectListBuilder(Table originalTable)
        {
            var skipColumns = new string[] {
                DataType.HierarchyId.ToString(),
                DataType.Geography.ToString(),
                DataType.Geometry.ToString(),
                "Flag",
                "NameStyle"
            };

            var selectListBuilder = new StringBuilder();
            foreach (Column column in originalTable.Columns)
            {
                selectListBuilder.Append(
                    Array.Exists(skipColumns, el => el == column.DataType.ToString())
                        ? $"null as [{column.Name}], "
                        : $"[{column.Name}], ");
            }
            selectListBuilder.Remove(selectListBuilder.Length - 2, 1);
            return selectListBuilder.ToString();
        }

        private string BuildSqlFrom(ForeignKey fk, DataRow row)
        {
            var referencedTable = OriginalDatabase.Tables[fk.ReferencedTable, fk.ReferencedTableSchema];
            var selectList = SelectListBuilder(referencedTable);

            var sbWhereClause = new StringBuilder();
            foreach (ForeignKeyColumn column in fk.Columns)
            {
                var fkvalue = row[column.Name].ToString();
                if (string.IsNullOrEmpty(fkvalue)) return "";

                var coltype = _originalDatabase.Tables[fk.Parent.Name, fk.Parent.Schema]
                    .Columns[column.Name].DataType.ToString();
                sbWhereClause.Append(
                    coltype.Contains("varchar") || coltype.Contains("nchar")
                        ? $"{column.ReferencedColumn} = '{fkvalue}' and "
                        : $"{column.ReferencedColumn} = {fkvalue} and ");
            }
            sbWhereClause.Remove(sbWhereClause.Length - 4, 4);

            return $"select {selectList} from {fk.ReferencedTableSchema}.{fk.ReferencedTable} where {sbWhereClause}";
        }

        public static bool PrintDatabaseList(Server svr)
        {
            // Write out the instance name.
            WriteLine(svr.Name);

            // Write out a list of the databases in the instance.
            try
            {
                foreach (Database db in svr.Databases)
                {
                    WriteLine(db.Name);
                }
                return true;
            }
            catch (Exception e)
            {
                do
                {
                    WriteLine($"Error: {e.Message}");
                    e = e.InnerException;
                } while (e != null);
                return false;
            }

        }

    }
}
