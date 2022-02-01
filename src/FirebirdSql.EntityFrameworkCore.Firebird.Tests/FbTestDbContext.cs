﻿/*
 *    The contents of this file are subject to the Initial
 *    Developer's Public License Version 1.0 (the "License");
 *    you may not use this file except in compliance with the
 *    License. You may obtain a copy of the License at
 *    https://github.com/FirebirdSQL/NETProvider/raw/master/license.txt.
 *
 *    Software distributed under the License is distributed on
 *    an "AS IS" basis, WITHOUT WARRANTY OF ANY KIND, either
 *    express or implied. See the License for the specific
 *    language governing rights and limitations under the License.
 *
 *    All Rights Reserved.
 */

//$Authors = Jiri Cincura (jiri@cincura.net)

using System.Linq;
using Microsoft.EntityFrameworkCore;

namespace FirebirdSql.EntityFrameworkCore.Firebird.Tests;

public class FbTestDbContext : DbContext
{
	readonly string _connectionString;

	public FbTestDbContext(string connectionString)
		: base()
	{
		_connectionString = connectionString;
	}

	protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
	{
		base.OnConfiguring(optionsBuilder);

		optionsBuilder.UseFirebird(_connectionString);
	}

	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		base.OnModelCreating(modelBuilder);
		OnTestModelCreating(modelBuilder);
		AfterModelCreated(modelBuilder);
	}

	protected virtual void OnTestModelCreating(ModelBuilder modelBuilder)
	{ }

	protected virtual void AfterModelCreated(ModelBuilder modelBuilder)
	{
		foreach (var entity in modelBuilder.Model.GetEntityTypes())
		{
			foreach (var property in entity.GetProperties().Where(x => x.ClrType == typeof(string)))
			{
				property.SetMaxLength(100);
			}
		}
	}
}
