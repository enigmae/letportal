﻿using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Threading.Tasks;
using LetPortal.Core.Persistences;
using LetPortal.Core.Utils;
using LetPortal.Portal.Constants;
using LetPortal.Portal.Entities.Databases;
using LetPortal.Portal.Entities.Shared;
using LetPortal.Portal.Mappers;
using LetPortal.Portal.Mappers.SqlServer;
using LetPortal.Portal.Models;
using LetPortal.Portal.Models.Databases;

namespace LetPortal.Portal.Executions.SqlServer
{
    public class SqlServerExecutionDatabase : IExecutionDatabase
    {
        public ConnectionType ConnectionType => ConnectionType.SQLServer;

        private readonly ISqlServerMapper _sqlServerMapper;

        private readonly ICSharpMapper _cSharpMapper;

        public SqlServerExecutionDatabase(ISqlServerMapper sqlServerMapper, ICSharpMapper cSharpMapper)
        {
            _sqlServerMapper = sqlServerMapper;
            _cSharpMapper = cSharpMapper;
        }

        public async Task<ExecuteDynamicResultModel> Execute(DatabaseConnection databaseConnection, string formattedString, IEnumerable<ExecuteParamModel> parameters)
        {
            var result = new ExecuteDynamicResultModel();
            using (var sqlDbConnection = new SqlConnection(databaseConnection.ConnectionString))
            {
                sqlDbConnection.Open();
                using (var command = new SqlCommand(formattedString, sqlDbConnection))
                {
                    var upperFormat = formattedString.ToUpper().Trim();
                    var isQuery = upperFormat.StartsWith("SELECT ") && upperFormat.Contains("FROM ");
                    var isInsert = upperFormat.StartsWith("INSERT INTO ");
                    var isUpdate = upperFormat.StartsWith("UPDATE ");
                    var isDelete = upperFormat.StartsWith("DELETE ");
                    var isStoreProcedure = upperFormat.StartsWith("EXEC ");

                    var listParams = new List<SqlParameter>();
                    if (parameters != null)
                    {
                        foreach (var parameter in parameters)
                        {
                            var fieldParam = StringUtil.GenerateUniqueName();
                            formattedString = formattedString.Replace("{{" + parameter.Name + "}}", "@" + fieldParam);
                            listParams.Add(
                                new SqlParameter(fieldParam, GetSqlDbType(parameter.Name, parameter.ReplaceValue, out var castObject))
                                {
                                    Value = castObject,
                                    Direction = ParameterDirection.Input
                                });
                        }
                    }

                    command.Parameters.AddRange(listParams.ToArray());
                    command.CommandText = formattedString;
                    if (isQuery)
                    {
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            using (var dt = new DataTable())
                            {
                                dt.Load(reader);
                                result.IsSuccess = true;
                                if (dt.Rows.Count > 0)
                                {
                                    var str = ConvertUtil.SerializeObject(dt, true);
                                    result.Result = ConvertUtil.DeserializeObject<dynamic>(str);
                                    result.IsSuccess = true;
                                }
                                else
                                {
                                    result.IsSuccess = false;
                                }
                            }
                        }
                    }
                    else if (isInsert || isUpdate || isDelete)
                    {
                        var effectiveCols = await command.ExecuteNonQueryAsync();
                        result.IsSuccess = true;
                    }
                    else if (isStoreProcedure)
                    {
                        // TODO: Will implement later
                    }
                }
            }

            return result;
        }

        private SqlDbType GetSqlDbType(string paramName, string value, out object castObj)
        {
            var splitted = paramName.Split("|");
            if (splitted.Length == 1)
            {
                castObj = _cSharpMapper.GetCSharpObjectByType(value, MapperConstants.String);
                return _sqlServerMapper.GetSqlDbType(MapperConstants.String);
            }
            else
            {
                castObj = _cSharpMapper.GetCSharpObjectByType(value, splitted[1]);
                return _sqlServerMapper.GetSqlDbType(splitted[1]);
            }
        }

        public Task<ExecuteDynamicResultModel> Execute(List<DatabaseConnection> databaseConnections, DatabaseExecutionChains executionChains, IEnumerable<ExecuteParamModel> parameters)
        {
            throw new System.NotImplementedException();
        }
    }
}
