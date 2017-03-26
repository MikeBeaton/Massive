﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Massive.Tests.PostgreSql.TableClasses
{
	public class Customer : DynamicModel
	{
		public Customer()
			: this(includeSchema: true)
		{
		}


		public Customer(bool includeSchema) :
			base(TestConstants.ReadWriteTestConnection, includeSchema ? "public.customers" : "customers", "customerid")
		{
		}
	}
}
