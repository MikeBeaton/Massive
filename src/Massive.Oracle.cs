﻿///////////////////////////////////////////////////////////////////////////////////////////////////
// Massive v2.0. Oracle specific code.
///////////////////////////////////////////////////////////////////////////////////////////////////
// Licensed to you under the New BSD License
// http://www.opensource.org/licenses/bsd-license.php
// Massive is copyright (c) 2009-2016 various contributors.
// All rights reserved.
// See for sourcecode, full history and contributors list: https://github.com/FransBouma/Massive
//
// Redistribution and use in source and binary forms, with or without modification, are permitted 
// provided that the following conditions are met:
//
// - Redistributions of source code must retain the above copyright notice, this list of conditions and the 
//   following disclaimer.
// - Redistributions in binary form must reproduce the above copyright notice, this list of conditions and 
//   the following disclaimer in the documentation and/or other materials provided with the distribution.
// - The names of its contributors may not be used to endorse or promote products derived from this software 
//   without specific prior written permission.
// 
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND ANY EXPRESS 
// OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY 
// AND FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR 
// CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL 
// DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, 
// DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, 
// WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY 
// WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
///////////////////////////////////////////////////////////////////////////////////////////////////
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Dynamic;
using System.Linq;
using System.Reflection;

namespace Massive
{
	/// <summary>
	/// Class which provides extension methods for various ADO.NET objects.
	/// </summary>
	public static partial class ObjectExtensions
	{
		/// <summary>
		/// Return data reader, first dereferencing cursors if needed on this provider.
		/// </summary>
		/// <param name="cmd">The command.</param>
		/// <param name="conn">The connection.</param>
		/// <param name="db">The parent DynamicModel (or subclass).</param>
		/// <returns>The reader.</returns>
		internal static IDataReader ExecuteDereferencingReader(this DbCommand cmd, DbConnection conn, DynamicModel db)
		{
			return cmd.ExecuteReader();
		}


		/// <summary>
		/// Returns true if this command requires a wrapping transaction.
		/// </summary>
		/// <param name="cmd">The command.</param>
		/// <param name="db">The dynamic model, to access config params.</param>
		/// <returns>true if it requires a wrapping transaction</returns>
		internal static bool RequiresWrappingTransaction(this DbCommand cmd, DynamicModel db)
		{
			return false;
		}


		/// <summary>
		/// Extension to set the parameter to the DB specific cursor type.
		/// </summary>
		/// <param name="p">The parameter.</param>
		/// <param name="value">Object reference to an existing cursor from a previous output or return direction cursor parameter, or null.</param>
		/// <returns>Returns false if not supported on this provider.</returns>
		private static bool SetCursor(this DbParameter p, object value)
		{
			p.SetRuntimeEnumProperty("OracleDbType", "RefCursor");
			p.Value = value;
			return true;
		}


		/// <summary>
		/// Check whether the parameter is of the DB specific cursor type.
		/// </summary>
		/// <param name="p">The parameter.</param>
		/// <returns>true if this is a cursor parameter.</returns>
		private static bool IsCursor(this DbParameter p)
		{
			return p.GetRuntimeEnumProperty("OracleDbType") == "RefCursor";
		}


		/// <summary>
		/// Extension to set anonymous DbParameter
		/// </summary>
		/// <param name="p">The parameter.</param>
		/// <returns>Returns false if not supported on this provider.</returns>
		private static bool SetAnonymousParameter(this DbParameter p)
		{
			return false;
		}


		/// <summary>
		/// Extension to check whether ADO.NET provider ignores output parameter types (no point requiring user to provide them if it does)
		/// </summary>
		/// <param name="p">The parameter.</param>
		/// <returns>True if output parameter type is ignored when generating output data types.</returns>
		private static bool IgnoresOutputTypes(this DbParameter p)
		{
			return false;
		}


		/// <summary>
		/// Extension to set ParameterDirection for single parameter, correcting for unexpected handling in specific ADO.NET providers.
		/// </summary>
		/// <param name="p">The parameter.</param>
		/// <param name="direction">The direction to set.</param>
		private static void SetDirection(this DbParameter p, ParameterDirection direction)
		{
			p.Direction = direction;
		}


		/// <summary>
		/// Extension to set Value (and implicitly DbType) for single parameter, adding support for provider unsupported types, etc.
		/// </summary>
		/// <param name="p">The parameter.</param>
		/// <param name="value">The non-null value to set. Nulls are handled in shared code.</param>
		private static void SetValue(this DbParameter p, object value)
		{
			if(value is Guid)
			{
				p.Value = value.ToString();
				p.Size = 36;
			}
			else
			{
				p.Value = value;
				var valueAsString = value as string;
				if(valueAsString != null)
				{
					p.Size = valueAsString.Length > 4000 ? -1 : 4000;
				}
			}
		}
	}


	/// <summary>
	/// A class that wraps your database table in Dynamic Funtime
	/// </summary>
	public partial class DynamicModel
	{
		#region Constants
		// Mandatory constants/variables every DB has to define. 
		/// <summary>
		/// The default sequence name for initializing the pk sequence name value in the ctor. 
		/// </summary>
		internal const string _defaultSequenceName = "";
		/// <summary>
		/// Flag to signal whether the sequence retrieval call (if any) is executed before the insert query (true) or after (false). Not a const, to avoid warnings. 
		/// </summary>
		private bool _sequenceValueCallsBeforeMainInsert = true;
		#endregion


		/// <summary>
		/// Partial method which, when implemented offers ways to set DbCommand specific properties, which are specific for a given ADO.NET provider. 
		/// </summary>
		/// <param name="toAlter">the command object to alter the properties of</param>
		partial void SetCommandSpecificProperties(DbCommand toAlter)
		{
			// no need for locking, as the values are always the same so it doesn't matter whether multiple threads set the value multiple times. 
			if(toAlter == null)
			{
				return;
			}
			((dynamic)toAlter).BindByName = true;   // keep true as the default as otherwise ODP.NET won't bind the parameters by name but by location.
			((dynamic)toAlter).InitialLONGFetchSize = -1;   // this is the ideal value, it obtains the LONG value in one go.
		}


		/// <summary>
		/// Gets a default value for the column as defined in the schema.
		/// </summary>
		/// <param name="column">The column.</param>
		/// <returns></returns>
		private dynamic GetDefaultValue(dynamic column)
		{
			string defaultValue = column.COLUMN_DEFAULT;
			if(string.IsNullOrEmpty(defaultValue))
			{
				return null;
			}
			dynamic result;
			switch(defaultValue)
			{
				case "SYSDATE":
				case "(SYSDATE)":
					result = DateTime.Now;
					break;
				default:
					result = defaultValue.Replace("(", "").Replace(")", "");
					break;
			}
			return result;
		}


		/// <summary>
		/// Gets the aggregate function to use in a scalar query for the fragment specified
		/// </summary>
		/// <param name="aggregateCalled">The aggregate called on the dynamicmodel, which should be converted to a DB function. Expected to be lower case</param>
		/// <returns>the aggregate function to use, or null if no aggregate function is supported for aggregateCalled</returns>
		protected virtual string GetAggregateFunction(string aggregateCalled)
		{
			switch(aggregateCalled)
			{
				case "sum":
					return "SUM";
				case "max":
					return "MAX";
				case "min":
					return "MIN";
				case "avg":
					return "AVG";
				default:
					return null;
			}
		}


		/// <summary>
		/// Gets the sql statement to use for obtaining the identity/sequenced value of the last insert.
		/// </summary>
		/// <returns></returns>
		protected virtual string GetIdentityRetrievalScalarStatement()
		{
			return string.IsNullOrEmpty(_primaryKeyFieldSequence) ? string.Empty : string.Format("SELECT {0}.NEXTVAL FROM DUAL", _primaryKeyFieldSequence);
		}


		/// <summary>
		/// Gets the sql statement pattern for a count row query (count(*)). The pattern should include as place holders: {0} for source (FROM clause).
		/// </summary>
		/// <returns></returns>
		protected virtual string GetCountRowQueryPattern()
		{
			return "SELECT COUNT(*) FROM {0}";
		}


		/// <summary>
		/// Gets the name of the parameter with the prefix to use in a query, e.g. @rawName or :rawName
		/// </summary>
		/// <param name="rawName">raw name of the parameter, without parameter prefix</param>
		/// <returns>rawName prefixed with the db specific prefix (if any)</returns>
		/// <remarks>
		/// Prefixing both internally and externally (the previous pattern in Massive) works in all cases
		/// on all providers except for named parameters on Oracle.
		/// (The alternative and new pattern we've chosen to prefer where possible - because it's slightly
		/// cheaper to execute - of never prefixing internally, works for all cases and all providers except
		/// for the Devart provider for MySQL.)
		/// </remarks>
		internal static string PrefixParameterName(string rawName, DbCommand cmd = null)
		{
			return (cmd != null) ? rawName : (":" + rawName);
		}


		/// <summary>
		/// Gets the name of the parameter without the prefix, to use in results
		/// </summary>
		/// <param name="rawName">The name of the parameter, prefixed if we prefixed it above</param>
		/// <returns>raw name</returns>
		internal static string DeprefixParameterName(string dbParamName, DbCommand cmd)
		{
			return dbParamName;
		}


		/// <summary>
		/// Gets the select query pattern, to use for building select queries. The pattern should include as place holders: {0} for project list, {1} for the source (FROM clause).
		/// </summary>
		/// <param name="limit">The limit for the resultset. 0 means no limit.</param>
		/// <param name="whereClause">The where clause. Expected to have a prefix space if not empty</param>
		/// <param name="orderByClause">The order by clause. Expected to have a prefix space if not empty</param>
		/// <returns>
		/// string pattern which is usable to build select queries.
		/// </returns>
		protected virtual string GetSelectQueryPattern(int limit, string whereClause, string orderByClause)
		{
			var normalQuery = string.Format("SELECT {{0}} FROM ({{1}}){0}{1}", whereClause, orderByClause);
			if(limit > 0)
			{
				// we have to wrap the query and then apply the rownum filter, as aggregates etc. otherwise aren't going to contain the right values, as they're then applied to the
				// limited set, not the complete set!
				return string.Format("SELECT * FROM ({0}) WHERE rownum <= {1}", normalQuery, limit);
			}
			return normalQuery;
		}


		/// <summary>
		/// Gets the insert query pattern, to use for building insert queries. The pattern should include as place holders: {0} for target, {1} for field list, {2} for parameter list
		/// </summary>
		/// <returns></returns>
		protected virtual string GetInsertQueryPattern()
		{
			return "INSERT INTO {0} ({1}) VALUES ({2})";
		}


		/// <summary>
		/// Gets the update query pattern, to use for building update queries. The pattern should include as placeholders: {0} for target, {1} for field list with sets. Has to have
		/// trailing space
		/// </summary>
		/// <returns></returns>
		protected virtual string GetUpdateQueryPattern()
		{
			return "UPDATE {0} SET {1} ";
		}


		/// <summary>
		/// Gets the delete query pattern, to use for building delete queries. The pattern should include as placeholders: {0} for the target. Has to have trailing space
		/// </summary>
		/// <returns></returns>
		protected virtual string GetDeleteQueryPattern()
		{
			return "DELETE FROM {0} ";
		}


		/// <summary>
		/// Gets the name of the column using the expando object representing the column from the schema
		/// </summary>
		/// <param name="columnFromSchema">The column from schema in the form of an expando.</param>
		/// <returns>the name of the column as defined in the schema</returns>
		protected virtual string GetColumnName(dynamic columnFromSchema)
		{
			return columnFromSchema.COLUMN_NAME;
		}


		/// <summary>
		/// Post-processes the query used to obtain the meta-data for the schema. If no post-processing is required, simply return a toList 
		/// </summary>
		/// <param name="toPostProcess">To post process.</param>
		/// <returns></returns>
		private IEnumerable<dynamic> PostProcessSchemaQuery(IEnumerable<dynamic> toPostProcess)
		{
			return toPostProcess == null ? new List<dynamic>() : toPostProcess.ToList();
		}


		/// <summary>
		/// Builds a paging query and count query pair. 
		/// </summary>
		/// <param name="sql">The SQL statement to build the query pair for. Can be left empty, in which case the table name from the schema is used</param>
		/// <param name="primaryKeyField">The primary key field. Used for ordering. If left empty the defined PK field is used</param>
		/// <param name="whereClause">The where clause. Default is empty string.</param>
		/// <param name="orderByClause">The order by clause. Default is empty string.</param>
		/// <param name="columns">The columns to use in the project. Default is '*' (all columns, in table defined order).</param>
		/// <param name="pageSize">Size of the page. Default is 20</param>
		/// <param name="currentPage">The current page. 1-based. Default is 1.</param>
		/// <returns>ExpandoObject with two properties: MainQuery for fetching the specified page and CountQuery for determining the total number of rows in the resultset</returns>
		private dynamic BuildPagingQueryPair(string sql = "", string primaryKeyField = "", string whereClause = "", string orderByClause = "", string columns = "*", int pageSize = 20,
											 int currentPage = 1)
		{
			// 1) create the main query,
			// 2) wrap it with the paging query constructs. This is done for both the count and the paging query. 
			var orderByClauseFragment = string.IsNullOrEmpty(orderByClause) ? string.Format(" ORDER BY {0}", string.IsNullOrEmpty(primaryKeyField) ? PrimaryKeyField : primaryKeyField)
																			: ReadifyOrderByClause(orderByClause);
			var coreQuery = string.Format(this.GetSelectQueryPattern(0, ReadifyWhereClause(whereClause), orderByClauseFragment), columns, string.IsNullOrEmpty(sql) ? this.TableName : sql);
			dynamic toReturn = new ExpandoObject();
			toReturn.CountQuery = string.Format("SELECT COUNT(*) FROM ({0})", coreQuery);
			var pageStart = (currentPage - 1) * pageSize;
			// wrap the main query with a full select and the rownum predicates. 
			toReturn.MainQuery = string.Format("SELECT * FROM (SELECT a.*, rownum r___ FROM ({0}) a WHERE rownum <= {1}) WHERE r___ > {2}", coreQuery, (pageStart + pageSize), pageStart);
			return toReturn;
		}


		#region Properties
		/// <summary>
		/// Gets the table schema query to use to obtain meta-data for a given table and schema
		/// </summary>
		protected virtual string TableWithSchemaQuery
		{
			get { return "SELECT * FROM USER_TAB_COLUMNS WHERE TABLE_NAME = :0 AND OWNER = :1"; }
		}

		/// <summary>
		/// Gets the table schema query to use to obtain meta-data for a given table which is specified as the single parameter.
		/// </summary>
		protected virtual string TableWithoutSchemaQuery
		{
			get { return "SELECT * FROM USER_TAB_COLUMNS WHERE TABLE_NAME = :0"; }
		}
		#endregion
	}
}