﻿using System;
using System.Data;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Massive.Tests.Oracle.TableClasses;
using NUnit.Framework;
using SD.Tools.OrmProfiler.Interceptor;

namespace Massive.Tests.Oracle
{
	/// <summary>
	/// Suite of tests for stored procedures, functions and cursors on Oracle database.
	/// </summary>
	/// <remarks>
	/// Runs against functions and procedures which are created by running SPTests.sql script on the test database.
	/// These objects do not conflict with anything in the SCOTT database, and can be added there.
	/// </remarks>
	[TestFixture]
	public class SPTests
	{
		[SetUp]
		public void Setup()
		{
			InterceptorCore.Initialize("Massive Oracle procedure, function & cursor tests");
		}

		[Test]
		public void NormalWhereCall()
		{
			// Check that things are up and running normally before trying the new stuff
			var db = new Department();
			var rows = db.All(where: "LOC = :0", args: "Nowhere");
			Assert.AreEqual(9, rows.ToList().Count);
		}

		[Test]
		public void IntegerOutputParam()
		{
			var db = new SPTestsDatabase();
			dynamic intResult = db.ExecuteWithParams("begin :a := 1; end;", outParams: new { a = 0 });
			Assert.AreEqual(1, intResult.a);
		}

		public class dateNullParam
		{
			public DateTime? d { get; set; }
		}

		[Test]
		public void InitialNullDateOutputParam()
		{
			var db = new SPTestsDatabase();
			dynamic dateResult = db.ExecuteWithParams("begin :d := SYSDATE; end;", outParams: new dateNullParam());
			Assert.AreEqual(typeof(DateTime), dateResult.d.GetType());
		}

		[Test]
		public void InputAndOutputParams()
		{
			var db = new SPTestsDatabase();
			dynamic procResult = db.ExecuteAsProcedure("findMin", inParams: new { x = 1, y = 3 }, outParams: new { z = 0 });
			Assert.AreEqual(1, procResult.z);
		}

		[Test]
		public void InputAndReturnParams()
		{
			var db = new SPTestsDatabase();
			dynamic fnResult = db.ExecuteAsProcedure("findMax", inParams: new { x = 1, y = 3 }, returnParams: new { returnValue = 0 });
			Assert.AreEqual(3, fnResult.returnValue);
		}

		public class intNullParam
		{
			public int? x { get; set; }
		}

		[Test]
		public void InputOutputParam()
		{
			var db = new SPTestsDatabase();
			dynamic squareResult = db.ExecuteAsProcedure("squareNum", ioParams: new { x = 4 });
			Assert.AreEqual(16, squareResult.x);
		}

		[Test]
		public void InitialNullInputOutputParam()
		{
			var db = new SPTestsDatabase();
			dynamic squareResult = db.ExecuteAsProcedure("squareNum", ioParams: new intNullParam());
			Assert.AreEqual(null, squareResult.x);
		}

		[Test]
		public void SingleRowFromTableValuedFunction()
		{
			var db = new SPTestsDatabase();
			var record = db.QueryWithParams("SELECT * FROM table(GET_EMP(:p_EMPNO))", new { p_EMPNO = 7782 }).FirstOrDefault();
			Assert.AreEqual(7782, record.EMPNO);
			Assert.AreEqual("CLARK", record.ENAME);
		}

		[Test]
		public void DereferenceCursorValuedFunction()
		{
			var db = new SPTestsDatabase();
			// Oracle function one cursor return value
			var employees = db.QueryFromProcedure("get_dept_emps", inParams: new { p_DeptNo = 10 }, returnParams: new { v_rc = new Cursor() });
			int count = 0;
			foreach(var employee in employees)
			{
				Console.WriteLine(employee.EMPNO + " " + employee.ENAME);
				count++;
			}
			Assert.AreEqual(3, count);
		}

		[Test]
		public void DereferenceCursorOutputParameter()
		{
			var db = new SPTestsDatabase();
			// Oracle procedure one cursor output variables
			var moreEmployees = db.QueryFromProcedure("myproc", outParams: new { prc = new Cursor() });
			int count = 0;
			foreach(var employee in moreEmployees)
			{
				Console.WriteLine(employee.EMPNO + " " + employee.ENAME);
				count++;
			}
			Assert.AreEqual(14, count);
		}

		[Test]
		public void QueryMultipleFromTwoOutputCursors()
		{
			var db = new SPTestsDatabase();
			// Oracle procedure two cursor output variables
			var twoSets = db.QueryMultipleFromProcedure("tworesults", outParams: new { prc1 = new Cursor(), prc2 = new Cursor() });
			int sets = 0;
			int[] counts = new int[2];
			foreach(var set in twoSets)
			{
				foreach(var item in set)
				{
					counts[sets]++;
					if(sets == 0) Assert.AreEqual(typeof(string), item.ENAME.GetType());
					else Assert.AreEqual(typeof(string), item.DNAME.GetType());
				}
				sets++;
			}
			Assert.AreEqual(2, sets);
			Assert.AreEqual(14, counts[0]);
			Assert.AreEqual(60, counts[1]);
		}

		[Test]
		public void NonQueryWithTwoOutputCursors()
		{
			var db = new SPTestsDatabase();
			var twoSetDirect = db.ExecuteAsProcedure("tworesults", outParams: new { prc1 = new Cursor(), prc2 = new Cursor() });
			Assert.AreEqual("OracleRefCursor", twoSetDirect.prc1.GetType().Name);
			Assert.AreEqual("OracleRefCursor", twoSetDirect.prc2.GetType().Name);
		}

		[Test]
		public void QueryFromMixedCursorOutput()
		{
			var db = new SPTestsDatabase();
			var mixedSets = db.QueryMultipleFromProcedure("mixedresults", outParams: new { prc1 = new Cursor(), prc2 = new Cursor(), num1 = 0, num2 = 0 });
			int sets = 0;
			int[] counts = new int[2];
			foreach(var set in mixedSets)
			{
				foreach(var item in set)
				{
					counts[sets]++;
					if(sets == 0) Assert.AreEqual(typeof(string), item.ENAME.GetType());
					else Assert.AreEqual(typeof(string), item.DNAME.GetType());
				}
				sets++;
			}
			Assert.AreEqual(2, sets);
			Assert.AreEqual(14, counts[0]);
			Assert.AreEqual(60, counts[1]);
		}

		[Test]
		public void NonQueryFromMixedCursorOutput()
		{
			var db = new SPTestsDatabase();
			var mixedDirect = db.ExecuteAsProcedure("mixedresults", outParams: new { prc1 = new Cursor(), prc2 = new Cursor(), num1 = 0, num2 = 0 });
			Assert.AreEqual("OracleRefCursor", mixedDirect.prc1.GetType().Name);
			Assert.AreEqual("OracleRefCursor", mixedDirect.prc2.GetType().Name);
			Assert.AreEqual(1, mixedDirect.num1);
			Assert.AreEqual(2, mixedDirect.num2);
		}

		[Test]
		public void PassingCursorInputParameter()
		{
			var db = new SPTestsDatabase();
			// To share cursors between commands in Oracle the commands must use the same connection
			using(var conn = db.OpenConnection())
			{
				var res1 = db.ExecuteWithParams("begin open :p_rc for select* from emp where deptno = 10; end;", outParams: new { p_rc = new Cursor() }, connectionToUse: conn);
				Assert.AreEqual("OracleRefCursor", res1.p_rc.GetType().Name);
				// TO DO: This Oracle test procedure writes some data into a table; we should produce some output (e.g. a row count) instead
				var res2 = db.ExecuteAsProcedure("cursor_in_out.process_cursor", inParams: new { p_cursor = res1.p_rc }, connectionToUse: conn);
				Assert.AreEqual(0, ((IDictionary<string, object>)res2).Count);
			}
		}

		//[Test]
		//public void ScalarFromProcedure()
		//{
		//	var db = new SPTestDatabase();
		//	// TO DO
		//}

		[TearDown]
		public void CleanUp()
		{
		}
	}
}
